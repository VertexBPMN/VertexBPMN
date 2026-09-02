function getElement(containerId) {
    return document.getElementById(containerId);
}

async function addHumanTask(modeler, name) {
    const xml = await exportXml(modeler);
    const document = new DOMParser().parseFromString(xml, 'application/xml');
    const plan = document.getElementsByTagNameNS('*', 'casePlanModel')[0];
    if (!plan) {
        throw new Error('The CMMN artifact does not contain a case plan model.');
    }

    const namespace = plan.namespaceURI;
    const suffix = crypto.randomUUID().replaceAll('-', '');
    const definitionId = `HumanTask_${suffix}`;
    const planItem = document.createElementNS(namespace, 'cmmn:planItem');
    planItem.setAttribute('id', `PlanItem_${suffix}`);
    planItem.setAttribute('definitionRef', definitionId);
    const humanTask = document.createElementNS(namespace, 'cmmn:humanTask');
    humanTask.setAttribute('id', definitionId);
    humanTask.setAttribute('name', name);
    plan.append(planItem, humanTask);
    await importArtifact(modeler, new XMLSerializer().serializeToString(document), 'bpmn.io CMMN Modeler fallback');
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
        if (instance.importXML.length >= 2) {
            await new Promise((resolve, reject) => {
                instance.importXML(payload, (error, warnings) => {
                    if (error) {
                        reject(error);
                        return;
                    }
                    resolve({ warnings: warnings || [] });
                });
            });
        } else {
            await instance.importXML(payload);
        }
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
        if (instance.saveXML.length >= 2) {
            return await new Promise((resolve, reject) => {
                instance.saveXML({ format: true }, (error, xml) => {
                    if (error) {
                        reject(error);
                        return;
                    }
                    resolve(xml || '');
                });
            });
        }

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

export const CmmnModelerInterop = {
    createModeler: async function (containerId, cmmnXml) {
        const ctor = getConstructor(['CmmnJS', 'CmmnModeler']);
        if (!ctor) {
            const fallback = fallbackInstance('CMMN Modeler', containerId, cmmnXml);
            getElement(containerId)?.setAttribute('data-modeler-ready', 'true');
            return fallback;
        }

        const modeler = new ctor({ container: `#${containerId}` });
        await importArtifact(modeler, cmmnXml, 'bpmn.io CMMN Modeler fallback');
        getElement(containerId)?.setAttribute('data-modeler-ready', 'true');
        return modeler;
    },
    getXml: async function (modeler) {
        return await exportXml(modeler);
    },
    loadXml: async function (modeler, cmmnXml) {
        await importArtifact(modeler, cmmnXml, 'bpmn.io CMMN Modeler fallback');
    },
    addHumanTask: async function (modeler, name) {
        await addHumanTask(modeler, name);
    },
    destroy: function (modeler) {
        destroyInstance(modeler);
    }
};

window.CmmnModelerInterop = CmmnModelerInterop;
