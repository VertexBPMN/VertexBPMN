function getElement(containerId) {
    return document.getElementById(containerId);
}

function getConstructor(names) {
    for (const name of names) {
        const value = name.split('.').reduce((target, part) => target ? target[part] : undefined, window);
        if (typeof value === 'function') {
            return value;
        }
    }

    return null;
}

function renderFallback(containerId, title, payload) {
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
    note.textContent = 'Toolkit bundle not available in this environment. The Studio shell remains usable and preserves the artifact source.';
    wrapper.appendChild(note);

    const pre = document.createElement('pre');
    pre.textContent = payload || '';
    wrapper.appendChild(pre);

    container.appendChild(wrapper);
}

function fallbackInstance(kind, containerId, payload) {
    renderFallback(containerId, `bpmn.io ${kind} fallback`, payload);
    return {
        __vertexFallback: true,
        kind,
        containerId,
        payload
    };
}

async function importArtifact(instance, payload, fallbackTitle) {
    if (!instance || instance.__vertexFallback) {
        if (instance) {
            instance.payload = payload;
            renderFallback(instance.containerId, fallbackTitle, payload);
        }
        return;
    }

    if (typeof instance.importXML === 'function') {
        await instance.importXML(payload);
    }

    const canvas = typeof instance.get === 'function' ? instance.get('canvas') : null;
    if (canvas && typeof canvas.zoom === 'function') {
        canvas.zoom('fit-viewport');
    }
}

async function exportXml(instance) {
    if (!instance) {
        return '';
    }

    if (instance.__vertexFallback) {
        return instance.payload || '';
    }

    if (typeof instance.saveXML === 'function') {
        const result = await instance.saveXML({ format: true });
        return result.xml || '';
    }

    return '';
}

function destroyInstance(instance) {
    if (instance && !instance.__vertexFallback && typeof instance.destroy === 'function') {
        instance.destroy();
    }
}

export const BpmnModelerInterop = {
    createModeler: function (containerId, bpmnXml, propertiesPanelId) {
        const ctor = getConstructor(['BpmnJS', 'BpmnModeler']);
        if (!ctor) {
            return fallbackInstance('BPMN Modeler', containerId, bpmnXml);
        }

        const options = { container: `#${containerId}` };
        if (window.VertexBpmnModdle) {
            options.moddleExtensions = { vertex: window.VertexBpmnModdle };
        }
        const additionalModules = [];
        if (propertiesPanelId && window.BpmnJSPropertiesPanelModule && window.BpmnJSPropertiesProviderModule) {
            options.propertiesPanel = { parent: `#${propertiesPanelId}` };
            additionalModules.push(window.BpmnJSPropertiesPanelModule, window.BpmnJSPropertiesProviderModule);
        }
        if (window.VertexPropertiesProviderModule) {
            additionalModules.push(window.VertexPropertiesProviderModule);
        }
        if (window.VertexValidationModule) {
            additionalModules.push(window.VertexValidationModule);
        }
        if (additionalModules.length) {
            options.additionalModules = additionalModules;
        }

        const modeler = new ctor(options);
        importArtifact(modeler, bpmnXml, 'bpmn.io BPMN Modeler fallback').catch(err => console.error('BPMN modeler import failed', err));
        return modeler;
    },
    getXml: async function (modeler) {
        return await exportXml(modeler);
    },
    loadXml: async function (modeler, bpmnXml) {
        await importArtifact(modeler, bpmnXml, 'bpmn.io BPMN Modeler fallback');
    },
    destroy: function (modeler) {
        destroyInstance(modeler);
    }
};

window.BpmnModelerInterop = BpmnModelerInterop;
