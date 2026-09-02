function getElement(containerId) {
    return document.getElementById(containerId);
}

function parseSchema(formJson) {
    try {
        return JSON.parse(formJson || '{}');
    } catch (err) {
        console.error('Form schema parse failed', err);
        return { type: 'default', components: [] };
    }
}

function renderFormFallback(containerId, title, schema) {
    const container = getElement(containerId);
    if (!container) {
        return;
    }

    container.innerHTML = '';
    const wrapper = document.createElement('div');
    wrapper.className = 'bpmn-io-fallback';

    const heading = document.createElement('strong');
    heading.textContent = title;
    wrapper.appendChild(heading);

    const note = document.createElement('p');
    note.textContent = 'form-js bundle not available in this environment. The schema is still loaded and preserved.';
    wrapper.appendChild(note);

    const pre = document.createElement('pre');
    pre.textContent = JSON.stringify(schema, null, 2);
    wrapper.appendChild(pre);

    container.appendChild(wrapper);
}

function destroyForm(form) {
    if (form && !form.__vertexFallback && typeof form.destroy === 'function') {
        form.destroy();
    }
}

export const FormBuilderInterop = {
    createForm: function (containerId, formJson) {
        const schema = parseSchema(formJson);
        const ctor = window.FormEditor && window.FormEditor.FormEditor;
        if (typeof ctor !== 'function') {
            renderFormFallback(containerId, 'bpmn.io form-js Builder fallback', schema);
            getElement(containerId)?.setAttribute('data-modeler-ready', 'true');
            return { __vertexFallback: true, containerId, schema };
        }

        const form = new ctor({
            container: getElement(containerId),
            schema
        });
        getElement(containerId)?.setAttribute('data-modeler-ready', 'true');
        return form;
    },
    getJson: async function (form) {
        if (!form) {
            return '{}';
        }

        if (form.__vertexFallback) {
            return JSON.stringify(form.schema || {}, null, 2);
        }

        if (typeof form.getSchema === 'function') {
            return JSON.stringify(form.getSchema(), null, 2);
        }

        return '{}';
    },
    loadJson: async function (form, formJson) {
        const schema = parseSchema(formJson);
        if (!form) {
            return;
        }

        if (form.__vertexFallback) {
            form.schema = schema;
            renderFormFallback(form.containerId, 'bpmn.io form-js Builder fallback', schema);
            return;
        }

        if (typeof form.importSchema === 'function') {
            await form.importSchema(schema);
        }
    },
    addTextField: async function (form, key, label) {
        const schema = parseSchema(await FormBuilderInterop.getJson(form));
        schema.components = Array.isArray(schema.components) ? schema.components : [];
        if (schema.components.some(component => component.key === key)) {
            throw new Error(`A form field with key '${key}' already exists.`);
        }
        schema.components.push({
            type: 'textfield',
            key,
            label,
            id: `Field_${crypto.randomUUID().replaceAll('-', '')}`
        });
        await FormBuilderInterop.loadJson(form, JSON.stringify(schema));
    },
    destroy: function (form) {
        destroyForm(form);
    }
};

window.FormBuilderInterop = FormBuilderInterop;
