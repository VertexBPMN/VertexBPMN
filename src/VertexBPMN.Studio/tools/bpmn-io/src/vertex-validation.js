import { getBusinessObject } from 'bpmn-js/lib/util/ModelUtil';

const VERTEX_NS = 'https://vertexbpmn.io/schema/bpmn/1.0';
const VERTEX_NS_ALIASES = new Set([
  VERTEX_NS,
  'http://vertexbpmn.io/schema/1.0',
  'http://vertexbpmn.io/schema/1.0/bpmn'
]);

function isVertexType(moddleElement, localName) {
  if (!moddleElement) {
    return false;
  }
  if (typeof moddleElement.$instanceOf === 'function' && moddleElement.$instanceOf(`vertex:${localName}`)) {
    return true;
  }
  const descriptor = moddleElement.$type || '';
  if (descriptor === `vertex:${localName}` || descriptor.split(':').pop() === localName) {
    const ns = moddleElement.$descriptor?.ns?.uri;
    return !ns || VERTEX_NS_ALIASES.has(ns);
  }
  return false;
}

function getExtensions(element) {
  const bo = getBusinessObject(element);
  const extensionElements = bo && bo.get('extensionElements');
  return (extensionElements && extensionElements.get('values')) || [];
}

function attr(element, name) {
  const value = element.get ? element.get(name) : element[name];
  return value == null ? '' : String(value).trim();
}

function collectFromExtensions(ownerId, values) {
  const issues = [];
  for (const value of values) {
    if (isVertexType(value, 'Connector')) {
      if (!attr(value, 'type')) {
        issues.push({
          code: 'VEN-VERTEX-CONNECTOR-TYPE',
          severity: 'error',
          elementId: ownerId,
          message: `vertex:connector on '${ownerId}' is missing required type`
        });
      }
      if (!attr(value, 'operationId')) {
        issues.push({
          code: 'VEN-VERTEX-CONNECTOR-OPERATION',
          severity: 'error',
          elementId: ownerId,
          message: `vertex:connector on '${ownerId}' is missing required operationId`
        });
      }
    } else if (isVertexType(value, 'Webhook')) {
      if (!attr(value, 'path')) {
        issues.push({
          code: 'VEN-VERTEX-WEBHOOK-PATH',
          severity: 'error',
          elementId: ownerId,
          message: `vertex:webhook on '${ownerId}' is missing required path`
        });
      }
    } else if (isVertexType(value, 'Trigger')) {
      if (!attr(value, 'type')) {
        issues.push({
          code: 'VEN-VERTEX-TRIGGER-TYPE',
          severity: 'error',
          elementId: ownerId,
          message: `vertex:trigger on '${ownerId}' is missing required type`
        });
      }
      if (!attr(value, 'processDefinitionKey')) {
        issues.push({
          code: 'VEN-VERTEX-TRIGGER-PROCESS-KEY',
          severity: 'error',
          elementId: ownerId,
          message: `vertex:trigger on '${ownerId}' is missing required processDefinitionKey`
        });
      }
    } else if (isVertexType(value, 'Credential')) {
      if (!attr(value, 'id')) {
        issues.push({
          code: 'VEN-VERTEX-CREDENTIAL-ID',
          severity: 'error',
          elementId: ownerId,
          message: `vertex:credential on '${ownerId}' is missing required id`
        });
      }
      if (!attr(value, 'kind')) {
        issues.push({
          code: 'VEN-VERTEX-CREDENTIAL-KIND',
          severity: 'error',
          elementId: ownerId,
          message: `vertex:credential on '${ownerId}' is missing required kind`
        });
      }
    }
  }
  return issues;
}

function collectFromRegistry(elementRegistry) {
  const issues = [];
  for (const element of elementRegistry.getAll()) {
    if (!element || element.id === '__implicitroot__') {
      continue;
    }
    issues.push(...collectFromExtensions(element.id, getExtensions(element)));
  }
  return issues;
}

function localName(node) {
  return (node.localName || node.nodeName || '').split(':').pop();
}

function xmlAttr(node, name) {
  return (node.getAttribute && (node.getAttribute(name) || '').trim()) || '';
}

function isVertexNode(node) {
  const ns = node.namespaceURI || '';
  return VERTEX_NS_ALIASES.has(ns) || (!ns && (node.nodeName || '').startsWith('vertex:'));
}

function collectFromXml(xml) {
  const issues = [];
  const doc = new DOMParser().parseFromString(xml, 'text/xml');
  const parseError = doc.getElementsByTagName('parsererror')[0];
  if (parseError) {
    return [{ code: 'VEN-VERTEX-XML-INVALID', severity: 'error', elementId: null, message: parseError.textContent }];
  }
  const all = doc.getElementsByTagName('*');
  for (const node of all) {
    if (!isVertexNode(node)) {
      continue;
    }
    let owner = node.parentNode;
    while (owner && owner.nodeType === 1 && localName(owner) === 'extensionElements') {
      owner = owner.parentNode;
    }
    const ownerId = owner && owner.getAttribute ? owner.getAttribute('id') : null;
    const name = localName(node);
    if (name === 'connector') {
      if (!xmlAttr(node, 'type')) {
        issues.push({ code: 'VEN-VERTEX-CONNECTOR-TYPE', severity: 'error', elementId: ownerId, message: `vertex:connector on '${ownerId}' is missing required type` });
      }
      if (!xmlAttr(node, 'operationId')) {
        issues.push({ code: 'VEN-VERTEX-CONNECTOR-OPERATION', severity: 'error', elementId: ownerId, message: `vertex:connector on '${ownerId}' is missing required operationId` });
      }
    } else if (name === 'webhook' && !xmlAttr(node, 'path')) {
      issues.push({ code: 'VEN-VERTEX-WEBHOOK-PATH', severity: 'error', elementId: ownerId, message: `vertex:webhook on '${ownerId}' is missing required path` });
    } else if (name === 'trigger') {
      if (!xmlAttr(node, 'type')) {
        issues.push({ code: 'VEN-VERTEX-TRIGGER-TYPE', severity: 'error', elementId: ownerId, message: `vertex:trigger on '${ownerId}' is missing required type` });
      }
      if (!xmlAttr(node, 'processDefinitionKey')) {
        issues.push({ code: 'VEN-VERTEX-TRIGGER-PROCESS-KEY', severity: 'error', elementId: ownerId, message: `vertex:trigger on '${ownerId}' is missing required processDefinitionKey` });
      }
    } else if (name === 'credential') {
      if (!xmlAttr(node, 'id')) {
        issues.push({ code: 'VEN-VERTEX-CREDENTIAL-ID', severity: 'error', elementId: ownerId, message: `vertex:credential on '${ownerId}' is missing required id` });
      }
      if (!xmlAttr(node, 'kind')) {
        issues.push({ code: 'VEN-VERTEX-CREDENTIAL-KIND', severity: 'error', elementId: ownerId, message: `vertex:credential on '${ownerId}' is missing required kind` });
      }
    }
  }
  return issues;
}

function publish(eventBus, issues) {
  window.VertexBpmnValidationIssues = issues;
  if (eventBus) {
    eventBus.fire('vertex.validation.changed', { issues });
  }
  return issues;
}

function VertexValidation(eventBus, elementRegistry) {
  const run = () => publish(eventBus, collectFromRegistry(elementRegistry));
  eventBus.on('import.done', run);
  eventBus.on('commandStack.changed', run);
  window.VertexValidateBpmnModel = run;
}

VertexValidation.$inject = ['eventBus', 'elementRegistry'];

window.VertexValidateBpmn = function VertexValidateBpmn(xmlOrModel) {
  if (typeof xmlOrModel === 'string') {
    const issues = collectFromXml(xmlOrModel);
    window.VertexBpmnValidationIssues = issues;
    return issues;
  }
  if (xmlOrModel && typeof xmlOrModel.get === 'function') {
    try {
      return publish(xmlOrModel.get('eventBus', false), collectFromRegistry(xmlOrModel.get('elementRegistry')));
    } catch (err) {
      return [{ code: 'VEN-VERTEX-VALIDATE-FAILED', severity: 'error', elementId: null, message: String(err) }];
    }
  }
  return window.VertexBpmnValidationIssues || [];
};

export default {
  __init__: ['vertexValidation'],
  vertexValidation: ['type', VertexValidation]
};
