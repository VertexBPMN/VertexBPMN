import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const root = new URL('../../', import.meta.url);
const source = path => readFileSync(new URL(path, root), 'utf8');

test('bound task list actions route through form dialog and snapshot takes priority', () => {
    const tasks = source('src/VertexBPMN.Studio/Components/Pages/Tasks.razor');
    const dialog = source('src/VertexBPMN.Studio/Components/TaskDetailsDialog.razor');
    assert.match(source('src/VertexBPMN.Studio/Services/ITaskService.cs'), /StudioUserTask/);
    assert.match(tasks, /Open Task Form/);
    assert.match(tasks, /TaskFormState.IsBound\(context\)/);
    assert.ok(dialog.indexOf('Task.FormSchema!') < dialog.indexOf('File.Exists(path)'));
    assert.match(dialog, /GetVariablesAsync/);
    assert.match(dialog, /Task.FormSchemaHash/);
    assert.match(dialog, /Task.Revision/);
});

test('Studio registry contracts expose immutable revision list and concrete revision loading', () => {
    assert.match(source('src/VertexBPMN.Studio/Services/IFormDefinitionService.cs'), /GetRevisionsAsync/);
    assert.match(source('src/VertexBPMN.Studio/Services/IFormDefinitionService.cs'), /GetRevisionAsync/);
    const page = source('src/VertexBPMN.Studio/Components/Pages/FormBuilderPage.razor');
    assert.match(page, /Published revision/);
    assert.match(page, /Load selected revision/);
    assert.match(page, /GetRevisionAsync/);
});
