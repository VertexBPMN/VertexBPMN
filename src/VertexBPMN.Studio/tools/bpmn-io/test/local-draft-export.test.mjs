import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

test('local draft export uses the current browser model without server calls', async () => {
    const elements = [];
    function element() {
        const e = { append() {}, before() {}, remove() {}, setAttribute() {},
            addEventListener(name, callback) { this[name] = callback; } };
        elements.push(e);
        return e;
    }
    const canvas = element();
    let snapshot = '<definitions name="unsaved draft" />';
    let blob;
    let finishedEditing = false;
    class Modeler {
        async importXML() {}
        get(name) {
            if (name === 'directEditing') return { complete() { finishedEditing = true; } };
            return { zoom() {} };
        }
        async saveXML() { return { xml: snapshot }; }
    }
    const context = vm.createContext({
        window: { BpmnJS: Modeler }, HTMLElement: class {}, Blob,
        document: { getElementById: () => canvas, createElement: element, body: { append() {} } },
        URL: { createObjectURL(value) { blob = value; return 'blob:local'; }, revokeObjectURL() {} },
        setTimeout() {}
    });
    const source = readFileSync(new URL('../../../wwwroot/js/bpmn-modeler.js', import.meta.url), 'utf8');
    vm.runInContext(source.replace('export const BpmnModelerInterop', 'const BpmnModelerInterop'), context);
    const modeler = await context.window.BpmnModelerInterop.createModeler('canvas', '<old />');
    const button = elements.find(e => e.textContent === 'Export local BPMN draft');
    await button.click();
    assert.equal(await blob.text(), snapshot);
    assert.equal(finishedEditing, true);
    assert.equal(button.disabled, false);
    modeler.saveXML = async () => { throw new Error('serialization failed'); };
    await button.click();
    assert.ok(elements.some(e => e.textContent?.startsWith('Local export failed')));
    assert.equal(button.disabled, false);
});
