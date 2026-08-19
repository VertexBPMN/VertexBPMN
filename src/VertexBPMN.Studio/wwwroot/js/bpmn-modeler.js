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

function templateElementType(template) {
    const appliesTo = template.appliesTo || [];
    const supported = ["bpmn:StartEvent", "bpmn:ServiceTask", "bpmn:BusinessRuleTask", "bpmn:UserTask", "bpmn:CallActivity"];
    const explicit = appliesTo.find(type => supported.includes(type));
    if (explicit) return explicit;
    const category = (template.category || "").toLowerCase();
    if (category.includes("trigger")) return "bpmn:StartEvent";
    if (category.includes("decision")) return "bpmn:BusinessRuleTask";
    if (category.includes("form")) return "bpmn:UserTask";
    if (category.includes("case")) return "bpmn:CallActivity";
    return "bpmn:ServiceTask";
}

function createTemplateShape(modeler, template, values, position, parent) {
    const elementFactory = modeler.get("elementFactory");
    const modeling = modeler.get("modeling");
    const bpmnFactory = modeler.get("bpmnFactory");
    const type = templateElementType(template);
    const fields = Object.fromEntries((template.properties || []).map(property => [property.key, values && values[property.key] !== undefined ? values[property.key] : property.defaultValue || ""]));
    const inputs = Object.entries(fields)
        .filter(([key, value]) => value && !["credentialRef", "timeoutMs", "decisionRef", "binding", "version", "formRef", "formVersion", "caseRef", "path", "method", "secretRef"].includes(key))
        .map(([name, expression]) => bpmnFactory.create("vertex:Input", { name, expression }));
    const extensions = [];
    if (type === "bpmn:ServiceTask") {
        extensions.push(bpmnFactory.create("vertex:Connector", { type: template.runtime, operationId: template.id, credentialRef: fields.credentialRef || undefined, timeoutMs: fields.timeoutMs ? Number(fields.timeoutMs) : undefined }));
    }
    if (type === "bpmn:StartEvent") {
        extensions.push(bpmnFactory.create("vertex:Trigger", { type: template.runtime, name: template.name, processDefinitionKey: fields.processDefinitionKey || undefined }));
        if (fields.path || fields.method || fields.secretRef) extensions.push(bpmnFactory.create("vertex:Webhook", { path: fields.path || undefined, method: fields.method || undefined, secretRef: fields.secretRef || undefined }));
    }
    if (type === "bpmn:BusinessRuleTask") extensions.push(bpmnFactory.create("vertex:Decision", { decisionRef: fields.decisionRef || template.id, binding: fields.binding || undefined, version: fields.version || undefined }));
    if (type === "bpmn:UserTask") extensions.push(bpmnFactory.create("vertex:Form", { formRef: fields.formRef || template.id, formVersion: fields.formVersion || undefined }));
    if (type === "bpmn:CallActivity") extensions.push(bpmnFactory.create("vertex:Case", { caseRef: fields.caseRef || template.id }));
    if (inputs.length) extensions.push(bpmnFactory.create("vertex:IoMapping", { inputs, outputs: [] }));
    const businessObject = bpmnFactory.create(type, { name: template.name, calledElement: type === "bpmn:CallActivity" ? (fields.caseRef || template.id) : undefined });
    const shape = elementFactory.createShape({ type, businessObject });
    const created = modeling.createShape(shape, position, parent);
    if (extensions.length) modeling.updateProperties(created, { name: template.name, extensionElements: bpmnFactory.create("bpmn:ExtensionElements", { values: extensions }) });
    return created;
}

function insertQuickTemplate(modeler, template, target) {
    const canvas = modeler.get("canvas");
    const created = createTemplateShape(modeler, template, null, { x: target.x + target.width + 110, y: target.y + target.height / 2 }, target.parent || canvas.getRootElement());
    canvas.scrollToElement(created);
}

function configureQuickInsert(modeler, templates) {
    if (!modeler || modeler.__vertexFallback) {
        return;
    }

    const state = modeler.__vertexQuickInsert || { templates: [], bound: false };
    state.templates = templates || [];
    modeler.__vertexQuickInsert = state;
    const overlays = modeler.get("overlays");

    const render = element => {
        overlays.remove({ type: "vertex-quick-insert" });
        if (!element || !state.templates.length || !element.businessObject || element.businessObject.$type === "bpmn:Process") {
            return;
        }

        const container = document.createElement("div");
        container.className = "vertex-quick-insert";
        const label = document.createElement("span");
        label.textContent = "+ Connector";
        container.appendChild(label);
        state.templates.forEach(template => {
            const button = document.createElement("button");
            button.type = "button";
            button.textContent = template.name;
            button.title = "Insert " + template.name;
            button.onclick = event => {
                event.preventDefault();
                event.stopPropagation();
                insertQuickTemplate(modeler, template, element);
                render(null);
            };
            container.appendChild(button);
        });
        overlays.add(element, "vertex-quick-insert", { position: { bottom: -18, right: -18 }, html: container });
    };

    if (!state.bound) {
        modeler.get("eventBus").on("selection.changed", 250, event => render(event.newSelection && event.newSelection[0]));
        state.bound = true;
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
    insertConnectorTemplate: async function (modeler, template, values) {
        if (!modeler || modeler.__vertexFallback) {
            throw new Error("Connector templates require the bpmn.io modeler bundle.");
        }
        const canvas = modeler.get("canvas");
        const viewbox = canvas.viewbox();
        const created = createTemplateShape(modeler, template, values, { x: viewbox.x + viewbox.width / 2, y: viewbox.y + viewbox.height / 2 }, canvas.getRootElement());
        canvas.scrollToElement(created);
    },
    configureQuickInsert: function (modeler, templates) {
        configureQuickInsert(modeler, templates);
    },
    configureDecisionPicker: function (decisions) {
        window.VertexBpmnDecisionOptions = (decisions || [])
            .filter(decision => decision && decision.key)
            .map(decision => ({ key: decision.key, name: decision.name || decision.key }));
    },
    destroy: function (modeler) {
        destroyInstance(modeler);
    }
};

window.BpmnModelerInterop = BpmnModelerInterop;
