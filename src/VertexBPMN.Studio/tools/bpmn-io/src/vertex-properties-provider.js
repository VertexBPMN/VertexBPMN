import { is, getBusinessObject } from 'bpmn-js/lib/util/ModelUtil';
import { isTextFieldEntryEdited, TextFieldEntry } from '@bpmn-io/properties-panel';
import { useService } from 'bpmn-js-properties-panel';

const CONNECTOR_HOSTS = [
  'bpmn:ServiceTask',
  'bpmn:SendTask',
  'bpmn:ReceiveTask',
  'bpmn:CallActivity'
];

function isConnectorHost(element) {
  return CONNECTOR_HOSTS.some(type => is(element, type));
}

function isStartEvent(element) {
  return is(element, 'bpmn:StartEvent');
}

function createModdleElement(type, properties, parent, factory) {
  const element = factory.create(type, properties);
  if (parent) {
    element.$parent = parent;
  }
  return element;
}

function getExtensionElements(businessObject) {
  return businessObject.get('extensionElements');
}

function getExtension(element, type) {
  const bo = getBusinessObject(element);
  const extensionElements = getExtensionElements(bo);
  if (!extensionElements) {
    return null;
  }
  const values = extensionElements.get('values') || [];
  return values.find(value => is(value, type)) || null;
}

function getOrCreateExtensionElements(element, bpmnFactory, commandStack) {
  const bo = getBusinessObject(element);
  let extensionElements = getExtensionElements(bo);
  if (!extensionElements) {
    extensionElements = createModdleElement('bpmn:ExtensionElements', { values: [] }, bo, bpmnFactory);
    commandStack.execute('element.updateModdleProperties', {
      element,
      moddleElement: bo,
      properties: { extensionElements }
    });
  }
  return extensionElements;
}

function getOrCreateExtension(element, type, bpmnFactory, commandStack, defaults = {}) {
  const existing = getExtension(element, type);
  if (existing) {
    return existing;
  }
  const extensionElements = getOrCreateExtensionElements(element, bpmnFactory, commandStack);
  const created = createModdleElement(type, defaults, extensionElements, bpmnFactory);
  const values = (extensionElements.get('values') || []).concat([created]);
  commandStack.execute('element.updateModdleProperties', {
    element,
    moddleElement: extensionElements,
    properties: { values }
  });
  return created;
}

function setExtensionProperty(element, type, property, value, bpmnFactory, commandStack) {
  const next = value === '' || value === undefined || value === null ? undefined : value;
  const current = getExtension(element, type);
  if (!current && next === undefined) {
    return;
  }
  const target = getOrCreateExtension(element, type, bpmnFactory, commandStack);
  commandStack.execute('element.updateModdleProperties', {
    element,
    moddleElement: target,
    properties: { [property]: next }
  });
}

function textEntry(id, label, type, property) {
  return {
    id,
    component: function VertexTextEntry(props) {
      const { element } = props;
      const debounce = useService('debounceInput');
      const bpmnFactory = useService('bpmnFactory');
      const commandStack = useService('commandStack');

      const getValue = () => {
        const ext = getExtension(element, type);
        const value = ext && ext.get(property);
        return value == null ? '' : String(value);
      };

      const setValue = (value) => {
        setExtensionProperty(element, type, property, value, bpmnFactory, commandStack);
      };

      return TextFieldEntry({
        element,
        id: props.id,
        label,
        getValue,
        setValue,
        debounce
      });
    },
    isEdited: isTextFieldEntryEdited
  };
}

function parsePairs(text) {
  return String(text || '')
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => {
      const idx = line.indexOf('=');
      if (idx < 0) {
        return { left: line, right: '' };
      }
      return { left: line.slice(0, idx).trim(), right: line.slice(idx + 1).trim() };
    })
    .filter(pair => pair.left);
}

function formatPairs(items, leftName, rightName) {
  if (!items || !items.length) {
    return '';
  }
  return items
    .map(item => `${item.get(leftName) || ''}=${item.get(rightName) || ''}`)
    .join('\n');
}

function ioMappingEntry(id, label, collectionName, childType, leftName, rightName) {
  return {
    id,
    component: function VertexIoMappingEntry(props) {
      const { element } = props;
      const debounce = useService('debounceInput');
      const bpmnFactory = useService('bpmnFactory');
      const commandStack = useService('commandStack');

      const getValue = () => {
        const mapping = getExtension(element, 'vertex:IoMapping');
        if (!mapping) {
          return '';
        }
        return formatPairs(mapping.get(collectionName), leftName, rightName);
      };

      const setValue = (value) => {
        const mapping = getOrCreateExtension(element, 'vertex:IoMapping', bpmnFactory, commandStack, {
          inputs: [],
          outputs: []
        });
        const children = parsePairs(value).map(pair =>
          createModdleElement(childType, { [leftName]: pair.left, [rightName]: pair.right }, mapping, bpmnFactory)
        );
        commandStack.execute('element.updateModdleProperties', {
          element,
          moddleElement: mapping,
          properties: { [collectionName]: children }
        });
      };

      return TextFieldEntry({
        element,
        id: props.id,
        label,
        description: 'One mapping per line: name=value',
        getValue,
        setValue,
        debounce
      });
    },
    isEdited: isTextFieldEntryEdited
  };
}

function connectorEntries() {
  return [
    textEntry('vertex-connector-type', 'Connector type', 'vertex:Connector', 'type'),
    textEntry('vertex-connector-operation', 'Operation ID', 'vertex:Connector', 'operationId'),
    textEntry('vertex-connector-credential', 'Credential ref', 'vertex:Connector', 'credentialRef'),
    textEntry('vertex-connector-timeout', 'Timeout (ms)', 'vertex:Connector', 'timeoutMs'),
    textEntry('vertex-retry-max-attempts', 'Retry max attempts', 'vertex:RetryPolicy', 'maxAttempts'),
    textEntry('vertex-retry-strategy', 'Retry strategy', 'vertex:RetryPolicy', 'strategy'),
    textEntry('vertex-retry-base-delay', 'Retry base delay (ms)', 'vertex:RetryPolicy', 'baseDelayMs'),
    textEntry('vertex-retry-on', 'Retry on', 'vertex:RetryPolicy', 'retryOn'),
    ioMappingEntry('vertex-io-inputs', 'IO inputs', 'inputs', 'vertex:Input', 'name', 'expression'),
    ioMappingEntry('vertex-io-outputs', 'IO outputs', 'outputs', 'vertex:Output', 'name', 'target'),
    textEntry('vertex-credential-id', 'Credential id', 'vertex:Credential', 'id'),
    textEntry('vertex-credential-kind', 'Credential kind', 'vertex:Credential', 'kind')
  ];
}

function startEventEntries() {
  return [
    textEntry('vertex-webhook-path', 'Webhook path', 'vertex:Webhook', 'path'),
    textEntry('vertex-webhook-method', 'Webhook method', 'vertex:Webhook', 'method'),
    textEntry('vertex-webhook-secret', 'Webhook secret ref', 'vertex:Webhook', 'secretRef'),
    textEntry('vertex-webhook-credential', 'HMAC credential ref', 'vertex:Webhook', 'credentialRef'),
    textEntry('vertex-webhook-secret-key', 'Credential secret key', 'vertex:Webhook', 'secretKey'),
    textEntry('vertex-webhook-auth-mode', 'Auth mode', 'vertex:Webhook', 'authMode'),
    textEntry('vertex-webhook-payload-schema', 'Payload schema', 'vertex:Webhook', 'payloadSchema'),
    textEntry('vertex-webhook-correlation-key', 'Correlation key', 'vertex:Webhook', 'correlationKey'),
    textEntry('vertex-trigger-type', 'Trigger type', 'vertex:Trigger', 'type'),
    textEntry('vertex-trigger-name', 'Trigger name', 'vertex:Trigger', 'name'),
    textEntry('vertex-trigger-process-key', 'Process definition key', 'vertex:Trigger', 'processDefinitionKey')
  ];
}

function createVertexGroup(element) {
  const entries = [];
  if (isConnectorHost(element)) {
    entries.push(...connectorEntries());
  }
  if (isStartEvent(element)) {
    entries.push(...startEventEntries());
  }
  if (!entries.length) {
    return null;
  }
  return {
    id: 'vertex',
    label: 'Vertex',
    entries
  };
}

function VertexPropertiesProvider(propertiesPanel) {
  this.getGroups = function (element) {
    return function (groups) {
      const group = createVertexGroup(element);
      if (group) {
        groups.push(group);
      }
      return groups;
    };
  };

  propertiesPanel.registerProvider(500, this);
}

VertexPropertiesProvider.$inject = ['propertiesPanel'];

export default {
  __init__: ['vertexPropertiesProvider'],
  vertexPropertiesProvider: ['type', VertexPropertiesProvider]
};
