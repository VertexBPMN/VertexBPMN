import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const root = new URL('../../', import.meta.url);
const source = path => readFileSync(new URL(path, root), 'utf8');
const tasksPath = 'src/VertexBPMN.Studio/Components/Pages/Tasks.razor';

// Execute the page's claim-selection expression, not a separately maintained
// resolver. This narrow C# expression adapter is a Node contract check, not a
// compiled Razor or token-validation test.
function pageSubject(user, path = tasksPath) {
    const page = source(path).replaceAll('state.User', 'authenticationState.User');
    const assignment = page.match(/(_currentUser(?:Name|Subject))\s*=\s*([\s\S]*?);/);
    assert.ok(assignment, 'task page must initialize its current-user subject');
    const expression = assignment[2]
        .replaceAll('authenticationState.User.FindFirstValue("sub")', 'user.claims.sub')
        .replaceAll('authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier)', 'user.claims.nameIdentifier')
        .replaceAll('authenticationState.User.Identity?.Name', 'user.identity?.name')
        .replaceAll('authenticationState.User.Identity?.IsAuthenticated == true', 'user.identity?.authenticated === true')
        .replaceAll('authenticationState.User.Identity?.AuthenticationType == UiTestAuthenticationHandler.Scheme', 'user.identity?.authenticationType === "UiTest"');
    return Function('user', `return (${expression});`)(user);
}

function principal(claims, name = 'mutable-login', authenticationType = 'Cookies', authenticated = true) {
    return { claims: { sub: null, nameIdentifier: null, ...claims }, identity: { name, authenticationType, authenticated } };
}

test('claim payload and owned-task actions use the stable subject rather than a username', () => {
    assert.equal(pageSubject(principal({ sub: 'subject-42', nameIdentifier: 'mapped-17' })), 'subject-42');
    assert.equal(pageSubject(principal({ sub: 'subject-42' }, 'renamed-login')), 'subject-42');
    assert.equal(pageSubject(principal({ nameIdentifier: 'mapped-17' })), 'mapped-17');
    assert.equal(pageSubject(principal({ nameIdentifier: 'ui-test-user' }, 'UI Test User', 'UiTest')), 'ui-test-user');
    assert.equal(pageSubject(principal({})), null, 'OIDC display names must not become task subjects');

    const page = source(tasksPath);
    assert.match(page, /ClaimTaskAsync\(taskId, _currentUserSubject, TenantContext.CurrentTenantId\)/);
    assert.match(page, /context.Assignee == _currentUserSubject/);
    assert.match(page, /string.IsNullOrWhiteSpace\(_currentUserSubject\)/);
    assert.doesNotMatch(page, /_currentUserName|preferred_username/);
});

test('dialog permits stable-subject assignee despite renamed display name', () => {
    const dialog = 'src/VertexBPMN.Studio/Components/TaskDetailsDialog.razor';
    assert.equal(pageSubject(principal({ sub: 'subject-42' }, 'different-name'), dialog), 'subject-42');
    assert.equal(pageSubject(principal({}, 'display-only', 'Cookies'), dialog), null);
    assert.equal(pageSubject(principal({}, 'UI Test User', 'UiTest'), dialog), 'UI Test User');
});

test('only an authenticated existing UiTest identity may fall back to its name', () => {
    assert.equal(pageSubject(principal({}, 'UI Test User', 'UiTest')), 'UI Test User');
    assert.equal(pageSubject(principal({}, 'UI Test User', 'UiTest', false)), null);
    assert.equal(pageSubject(principal({}, 'UI Test User', 'Cookies')), null);
    assert.equal(pageSubject(principal({}, 'UI Test User', 'OpenIdConnect')), null);
    assert.equal(pageSubject(principal({ sub: 'subject-42' }, 'UI Test User', 'UiTest')), 'subject-42');
    assert.equal(pageSubject(principal({ nameIdentifier: 'ui-test-user' }, 'UI Test User', 'UiTest')), 'ui-test-user');
});
