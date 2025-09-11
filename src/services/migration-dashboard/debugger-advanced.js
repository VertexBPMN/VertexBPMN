// Advanced Visual Debugger Features
// Token visualization, variable inspection, multi-instance support, export

function showTokens(tokens) {
    const tokenDiv = document.getElementById('tokenState');
    if (!tokenDiv) return;
    if (!tokens || tokens.length === 0) {
        tokenDiv.innerHTML = '<em>No active tokens</em>';
        return;
    }
    tokenDiv.innerHTML = '<h3>Active Tokens</h3>' + tokens.map(t => `<div>Activity: <b>${t.ActivityId}</b> (${t.Type})</div>`).join('');
}

function showVariables(variables) {
    const varDiv = document.getElementById('variableState');
    if (!varDiv) return;
    varDiv.innerHTML = `<h3>Variables</h3><pre>${JSON.stringify(variables, null, 2)}</pre>`;
}

function showMultiInstance(instances) {
    const miDiv = document.getElementById('multiInstanceState');
    if (!miDiv) return;
    if (!instances || instances.length === 0) {
        miDiv.innerHTML = '<em>No multi-instance subprocesses</em>';
        return;
    }
    miDiv.innerHTML = '<h3>Multi-Instance Subprocesses</h3>' + instances.map(i => `<div>ID: <b>${i.Id}</b>, State: ${i.State}</div>`).join('');
}

function exportDebuggerState(state) {
    const dataStr = 'data:text/json;charset=utf-8,' + encodeURIComponent(JSON.stringify(state, null, 2));
    const dlAnchor = document.createElement('a');
    dlAnchor.setAttribute('href', dataStr);
    dlAnchor.setAttribute('download', 'debugger-state.json');
    dlAnchor.click();
}

// Usage in debugger.js:
// showTokens(state.Tokens);
// showVariables(state.Variables);
// showMultiInstance(state.MultiInstances);
// exportDebuggerState(state);
