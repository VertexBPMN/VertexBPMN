function getElement(containerId) {
    return document.getElementById(containerId);
}

async function addDecisionRule(modeler) {
    const xml = await exportXml(modeler);
    const document = new DOMParser().parseFromString(xml, 'application/xml');
    const table = document.getElementsByTagNameNS('*', 'decisionTable')[0];
    if (!table) {
        throw new Error('The DMN artifact does not contain a decision table.');
    }

    const namespace = table.namespaceURI;
    const rule = document.createElementNS(namespace, 'rule');
    rule.setAttribute('id', `Rule_${crypto.randomUUID().replaceAll('-', '')}`);
    const inputEntry = document.createElementNS(namespace, 'inputEntry');
    const inputText = document.createElementNS(namespace, 'text');
    inputText.textContent = '< -1000000';
    inputEntry.appendChild(inputText);
    const outputEntry = document.createElementNS(namespace, 'outputEntry');
    const outputText = document.createElementNS(namespace, 'text');
    outputText.textContent = '"never"';
    outputEntry.appendChild(outputText);
    rule.append(inputEntry, outputEntry);
    table.appendChild(rule);
    await importArtifact(modeler, new XMLSerializer().serializeToString(document), 'bpmn.io DMN Modeler fallback');
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

export const DmnModelerInterop = {
    createModeler: function (containerId, dmnXml) {
        const ctor = getConstructor(['DmnJS', 'DmnModeler']);
        if (!ctor) {
            const fallback = fallbackInstance('DMN Modeler', containerId, dmnXml);
            getElement(containerId)?.setAttribute('data-modeler-ready', 'true');
            return fallback;
        }

        const modeler = new ctor({ container: `#${containerId}` });
        getElement(containerId)?.setAttribute('data-modeler-ready', 'true');
        importArtifact(modeler, dmnXml, 'bpmn.io DMN Modeler fallback')
            .catch(err => console.error('DMN modeler import failed', err));
        return modeler;
    },
    getXml: async function (modeler) {
        return await exportXml(modeler);
    },
    loadXml: async function (modeler, dmnXml) {
        await importArtifact(modeler, dmnXml, 'bpmn.io DMN Modeler fallback');
    },
    addDecisionRule: async function (modeler) {
        await addDecisionRule(modeler);
    },
    destroy: function (modeler) {
        destroyInstance(modeler);
    }
};

window.DmnModelerInterop = DmnModelerInterop;
