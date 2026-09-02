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

export const DmnViewerInterop = {
    createViewer: function (containerId, dmnXml) {
        const ctor = getConstructor(['DmnViewer', 'DmnJS', 'DmnModeler']);
        if (!ctor) {
            return fallbackInstance('DMN Viewer', containerId, dmnXml);
        }

        const viewer = new ctor({ container: `#${containerId}` });
        importArtifact(viewer, dmnXml, 'bpmn.io DMN Viewer fallback').catch(err => console.error('DMN viewer import failed', err));
        return viewer;
    },
    loadXml: async function (viewer, dmnXml) {
        await importArtifact(viewer, dmnXml, 'bpmn.io DMN Viewer fallback');
    },
    destroy: function (viewer) {
        destroyInstance(viewer);
    }
};

window.DmnViewerInterop = DmnViewerInterop;
