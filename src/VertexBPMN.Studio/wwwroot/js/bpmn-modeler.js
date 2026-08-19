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
    const position = target.type === "bpmn:SequenceFlow"
        ? { x: (target.waypoints[0].x + target.waypoints[target.waypoints.length - 1].x) / 2, y: (target.waypoints[0].y + target.waypoints[target.waypoints.length - 1].y) / 2 }
        : { x: target.x + target.width + 110, y: target.y + target.height / 2 };
    const created = createTemplateShape(modeler, template, null, position, target.parent || canvas.getRootElement());
    connectQuickInsert(modeler, target, created);
    canvas.scrollToElement(created);
}

function connectQuickInsert(modeler, target, created) {
    if (!target || target.type !== "bpmn:SequenceFlow" || !target.source || !target.target) return;

    const modeling = modeler.get("modeling");
    const source = target.source;
    const destination = target.target;
    modeling.removeConnection(target);
    modeling.connect(source, created, { type: "bpmn:SequenceFlow" });
    modeling.connect(created, destination, { type: "bpmn:SequenceFlow" });
}

function rootProcessKey(modeler) {
    const root = modeler.get("canvas").getRootElement();
    return root && root.businessObject && root.businessObject.id ? root.businessObject.id : "Process_1";
}

function extensionElements(bpmnFactory, values) {
    return bpmnFactory.create("bpmn:ExtensionElements", { values });
}

function lowCodeDefinition(modeler, kind) {
    const bpmnFactory = modeler.get("bpmnFactory");
    const processKey = rootProcessKey(modeler);
    switch (kind) {
        case "webhook":
            return {
                type: "bpmn:StartEvent", name: "Webhook received",
                extensions: [
                    bpmnFactory.create("vertex:Trigger", { type: "webhook", name: "Webhook received", processDefinitionKey: processKey }),
                    bpmnFactory.create("vertex:Webhook", { path: "/webhooks/new", method: "POST", authMode: "trigger-secret" })
                ]
            };
        case "timer":
            return {
                type: "bpmn:StartEvent", name: "Scheduled start",
                eventDefinitions: [bpmnFactory.create("bpmn:TimerEventDefinition", { timeDuration: bpmnFactory.create("bpmn:FormalExpression", { body: "PT5M" }) })],
                extensions: [bpmnFactory.create("vertex:Trigger", { type: "timer", name: "Scheduled start", processDefinitionKey: processKey })]
            };
        case "http":
            return { type: "bpmn:ServiceTask", name: "HTTP request", extensions: [bpmnFactory.create("vertex:Connector", { type: "http", operationId: "http-request" })] };
        case "database":
            return { type: "bpmn:ServiceTask", name: "Database write", extensions: [bpmnFactory.create("vertex:Connector", { type: "database", operationId: "db-upsert" })] };
        case "start":
            return { type: "bpmn:StartEvent", name: "Start" };
        case "end":
            return { type: "bpmn:EndEvent", name: "Done" };
        case "if":
            return { type: "bpmn:ExclusiveGateway", name: "Condition" };
        case "wait":
            return { type: "bpmn:IntermediateCatchEvent", name: "Wait", eventDefinitions: [bpmnFactory.create("bpmn:TimerEventDefinition", { timeDuration: bpmnFactory.create("bpmn:FormalExpression", { body: "PT5M" }) })] };
        case "form":
            return { type: "bpmn:UserTask", name: "User approval", extensions: [bpmnFactory.create("vertex:Form", { formRef: "approval-form" })] };
        case "decision":
            return { type: "bpmn:BusinessRuleTask", name: "Decision", extensions: [bpmnFactory.create("vertex:Decision", { decisionRef: "decision-table" })] };
        case "subworkflow":
            return { type: "bpmn:CallActivity", name: "Call workflow", calledElement: "subworkflow" };
        case "case":
            return { type: "bpmn:CallActivity", name: "Start case", calledElement: "case-model", extensions: [bpmnFactory.create("vertex:Case", { caseRef: "case-model" })] };
        case "batch":
            return { type: "bpmn:ServiceTask", name: "Batch task", loopCharacteristics: bpmnFactory.create("bpmn:MultiInstanceLoopCharacteristics", { isSequential: false }) };
        case "error":
            return { type: "bpmn:SubProcess", name: "Error handler", triggeredByEvent: true };
        default:
            throw new Error(`Unsupported low-code node '${kind}'.`);
    }
}

function insertLowCodeNode(modeler, kind, target, positionOverride) {
    if (!modeler || modeler.__vertexFallback) {
        throw new Error("Low-code nodes require the bpmn.io modeler bundle.");
    }

    const elementFactory = modeler.get("elementFactory");
    const modeling = modeler.get("modeling");
    const canvas = modeler.get("canvas");
    const bpmnFactory = modeler.get("bpmnFactory");
    const definition = lowCodeDefinition(modeler, kind);
    const businessObject = bpmnFactory.create(definition.type, {
        name: definition.name,
        calledElement: definition.calledElement,
        eventDefinitions: definition.eventDefinitions || [],
        loopCharacteristics: definition.loopCharacteristics,
        triggeredByEvent: definition.triggeredByEvent || false
    });
    if (definition.extensions && definition.extensions.length) {
        businessObject.extensionElements = extensionElements(bpmnFactory, definition.extensions);
    }

    const viewbox = canvas.viewbox();
    const position = positionOverride || (target && target.type === "bpmn:SequenceFlow"
        ? { x: (target.waypoints[0].x + target.waypoints[target.waypoints.length - 1].x) / 2, y: (target.waypoints[0].y + target.waypoints[target.waypoints.length - 1].y) / 2 }
        : { x: viewbox.x + viewbox.width / 2, y: viewbox.y + viewbox.height / 2 });
    const shape = elementFactory.createShape({ type: definition.type, businessObject });
    const created = modeling.createShape(shape, position, target && target.parent ? target.parent : canvas.getRootElement());
    connectQuickInsert(modeler, target, created);
    canvas.scrollToElement(created);

    if (kind === "error") {
        const errorStart = elementFactory.createShape({
            type: "bpmn:StartEvent",
            businessObject: bpmnFactory.create("bpmn:StartEvent", { name: "Error", eventDefinitions: [bpmnFactory.create("bpmn:ErrorEventDefinition")] })
        });
        const handled = elementFactory.createShape({ type: "bpmn:EndEvent", businessObject: bpmnFactory.create("bpmn:EndEvent", { name: "Handled" }) });
        const start = modeling.createShape(errorStart, { x: 40, y: 60 }, created);
        const end = modeling.createShape(handled, { x: 170, y: 60 }, created);
        modeling.connect(start, end, { type: "bpmn:SequenceFlow" });
    }

    return created;
}

function addRetryPolicy(modeler, element) {
    const bpmnFactory = modeler.get("bpmnFactory");
    const modeling = modeler.get("modeling");
    const commandStack = modeler.get("commandStack");
    const businessObject = element.businessObject;
    const extension = businessObject.extensionElements || extensionElements(bpmnFactory, []);
    const values = (extension.values || []).concat([
        bpmnFactory.create("vertex:RetryPolicy", { maxAttempts: 3, strategy: "exponential", baseDelayMs: 1000, retryOn: "5xx,timeout" })
    ]);
    modeling.updateProperties(element, { extensionElements: extension });
    commandStack.execute("element.updateModdleProperties", { element, moddleElement: extension, properties: { values } });
}

function insertLowCodePattern(modeler, patternId) {
    const patterns = {
        "http-retry": ["start", "http", "end"],
        "webhook-if-http": ["webhook", "if", "http", "end"],
        "cron-batch-db": ["timer", "batch", "database", "end"],
        "user-approval": ["start", "form", "end"],
        "decision-routing": ["start", "decision", "if", "end"],
        "case-start": ["start", "case", "end"]
    };
    const kinds = patterns[patternId];
    if (!kinds) throw new Error(`Unsupported low-code pattern '${patternId}'.`);

    const canvas = modeler.get("canvas");
    const modeling = modeler.get("modeling");
    const viewbox = canvas.viewbox();
    const y = viewbox.y + viewbox.height * 0.72;
    let previous = null;
    let retryTarget = null;
    kinds.forEach((kind, index) => {
        const created = insertLowCodeNode(modeler, kind, null, { x: viewbox.x + 90 + index * 180, y });
        if (previous) modeling.connect(previous, created, { type: "bpmn:SequenceFlow" });
        previous = created;
        if (kind === "http" && patternId === "http-retry") retryTarget = created;
    });
    if (retryTarget) addRetryPolicy(modeler, retryTarget);
    if (previous) canvas.scrollToElement(previous);
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
        if (!element || !element.businessObject || element.businessObject.$type === "bpmn:Process") {
            return;
        }

        const container = document.createElement("div");
        container.className = "vertex-quick-insert";
        const label = document.createElement("span");
        label.textContent = element.type === "bpmn:SequenceFlow" ? "+ Insert" : "+ Connector";
        container.appendChild(label);
        state.templates.filter(template => element.type !== "bpmn:SequenceFlow" || templateElementType(template) !== "bpmn:StartEvent").forEach(template => {
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
        [["http", "HTTP"], ["if", "IF"], ["wait", "Wait"], ["form", "Form"], ["decision", "Decision"], ["subworkflow", "Subworkflow"], ["case", "Case"], ["batch", "Batch"]].forEach(([kind, name]) => {
            const button = document.createElement("button");
            button.type = "button";
            button.textContent = name;
            button.title = "Insert " + name;
            button.onclick = event => {
                event.preventDefault();
                event.stopPropagation();
                insertLowCodeNode(modeler, kind, element);
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
    getValidationIssues: function (modeler) {
        return typeof window.VertexValidateBpmn === "function" ? window.VertexValidateBpmn(modeler) || [] : [];
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
    insertLowCodeNode: function (modeler, nodeKind) {
        insertLowCodeNode(modeler, nodeKind);
    },
    insertLowCodePattern: function (modeler, patternId) {
        insertLowCodePattern(modeler, patternId);
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
