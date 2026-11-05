import test from 'node:test';
import assert from 'node:assert/strict';
globalThis.window = {};
const { FormViewerInterop } = await import('../../src/VertexBPMN.Studio/wwwroot/js/form-viewer.js');

test('prefill is passed to viewer; read-only and unknown fields are excluded from completion', async () => {
    let imported;
    globalThis.window = { FormViewer: { Form: class {
        async importSchema(schema, data) { imported = { schema, data }; }
        submit() { return { data: { note: 'Edited', invoice: 'Tampered', secret: 'Hidden' } }; }
    } } };
    globalThis.document = { getElementById: () => ({}) };
    const schema = { components: [{ key: 'note', type: 'textfield' }, { key: 'invoice', type: 'textfield', readonly: true }] };
    const form = await FormViewerInterop.createViewer('form', JSON.stringify(schema), { note: 'Prefilled', invoice: 'INV-001', secret: 'Hidden' });
    assert.deepEqual(imported.data, { note: 'Prefilled', invoice: 'INV-001' });
    assert.deepEqual(JSON.parse(await FormViewerInterop.getData(form)), { note: 'Edited' });
});
test('loading a new schema replaces the editable output whitelist', async () => {
    globalThis.window = { FormViewer: { Form: class {
        async importSchema() {}
        submit() { return { data: { old: 'stale', current: 'fresh' } }; }
    } } };
    const form = await FormViewerInterop.createViewer('form', JSON.stringify({ components: [{ key: 'old' }] }));
    await FormViewerInterop.loadJson(form, JSON.stringify({ components: [{ key: 'current' }] }));
    assert.deepEqual(JSON.parse(await FormViewerInterop.getData(form)), { current: 'fresh' });
});

test('missing renderer cannot silently complete a bound form', async () => {
    await assert.rejects(FormViewerInterop.getData({ __vertexFallback: true }), /unavailable/);
});

test('invalid schema is rejected instead of replaced by an empty form', async () => {
    await assert.rejects(FormViewerInterop.createViewer('form', '{invalid'), /JSON/);
});
