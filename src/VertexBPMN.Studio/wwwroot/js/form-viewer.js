function getElement(containerId) {
    return document.getElementById(containerId);
}

function parseSchema(formJson) {
    return JSON.parse(formJson || '{}');
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

function fields(schema, inheritedReadOnly = false) {
    return (schema.components || []).flatMap(component => {
        const readOnly = inheritedReadOnly || component.readonly === true || component.readOnly === true || component.disabled === true;
        return [...(component.key ? [{ key: component.key, readOnly }] : []), ...fields(component, readOnly)];
    });
}

function selectData(data, fieldList, editableOnly = false) {
    return Object.fromEntries(fieldList.filter(field => (!editableOnly || !field.readOnly) && Object.hasOwn(data, field.key))
        .map(field => [field.key, data[field.key]]));
}

export const FormViewerInterop = {
    createViewer: async function (containerId, formJson, data = {}) {
        const schema = parseSchema(formJson);
        const ctor = window.FormViewer && window.FormViewer.Form;
        if (typeof ctor !== 'function') {
            renderFormFallback(containerId, 'bpmn.io form-js Viewer fallback', schema);
            return { __vertexFallback: true, containerId, schema };
        }

        const form = new ctor({ container: getElement(containerId) });
        form.__vertexFields = fields(schema);
        await form.importSchema(schema, selectData(data, form.__vertexFields));
        return form;
    },
    loadJson: async function (form, formJson) {
        const schema = parseSchema(formJson);
        if (!form) {
            return;
        }

        if (form.__vertexFallback) {
            form.schema = schema;
            renderFormFallback(form.containerId, 'bpmn.io form-js Viewer fallback', schema);
            return;
        }

        if (typeof form.importSchema === 'function') {
            form.__vertexFields = fields(schema);
            await form.importSchema(schema);
        }
    },
    getData: async function (form) {
        if (!form || form.__vertexFallback) {
            throw new Error('The form renderer is unavailable; completion is blocked.');
        }

        if (typeof form.submit !== 'function') {
            throw new Error('The form viewer does not expose a submit API.');
        }

        const result = form.submit();
        const errors = result && result.errors ? result.errors : {};
        if (Object.keys(errors).length > 0) {
            throw new Error(`Form validation failed: ${JSON.stringify(errors)}`);
        }

        return JSON.stringify(selectData(result && result.data ? result.data : {}, form.__vertexFields || [], true));
    },
    destroy: function (form) {
        destroyForm(form);
    }
};

window.FormViewerInterop = FormViewerInterop;
