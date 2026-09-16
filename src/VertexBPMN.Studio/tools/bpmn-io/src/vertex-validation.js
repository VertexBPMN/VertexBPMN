import { getBusinessObject } from 'bpmn-js/lib/util/ModelUtil';
import { validateFlowScopes, moddleFlowScopes } from './flow-validation.js';

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
  const hasConnector = values.some(value => isVertexType(value, 'Connector'));
  const hasExternalTask = values.some(value => isVertexType(value, 'ExternalTask'));
  if (hasConnector && hasExternalTask) {
    issues.push({ code: 'VEN-EXTERNAL-TASK-MIXED-HANDLER', severity: 'error', elementId: ownerId,
      message: `'${ownerId}' cannot contain both vertex:connector and vertex:externalTask` });
  }
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
    } else if (isVertexType(value, 'ExternalTask')) {
      const topic = attr(value, 'topic');
      const profile = attr(value, 'agentProfileRef');
      const retries = attr(value, 'maxRetries');
      const deadline = attr(value, 'deadlineSeconds');
      if (!/^agent\.[a-z0-9.-]+$/.test(topic)) {
        issues.push({ code: 'VEN-EXTERNAL-TASK-TOPIC', severity: 'error', elementId: ownerId, message: `Agent task '${ownerId}' requires a valid agent.* topic` });
      }
      if (!profile) {
        issues.push({ code: 'VEN-EXTERNAL-TASK-PROFILE', severity: 'error', elementId: ownerId, message: `Agent task '${ownerId}' requires a tenant agent profile` });
      } else if (!(window.VertexBpmnAgentProfiles || []).some(item => item.profileRef === profile && item.topic === topic)) {
        issues.push({ code: 'VEN-EXTERNAL-TASK-PROFILE-UNAVAILABLE', severity: 'error', elementId: ownerId, message: `Agent profile '${profile}' is not available for this tenant and topic` });
      }
      if (!/^[0-9]$/.test(retries)) {
        issues.push({ code: 'VEN-EXTERNAL-TASK-RETRIES', severity: 'error', elementId: ownerId, message: `Agent task '${ownerId}' requires maxRetries between 0 and 9` });
      }
      if (!/^\d+$/.test(deadline) || Number(deadline) < 1 || Number(deadline) > 86400) {
        issues.push({ code: 'VEN-EXTERNAL-TASK-DEADLINE', severity: 'error', elementId: ownerId, message: `Agent task '${ownerId}' requires deadlineSeconds between 1 and 86400` });
      }
      const mapping = values.find(item => isVertexType(item, 'IoMapping'));
      const outputs = mapping?.get('outputs') || [];
      if (outputs.length && (outputs.length !== 1
          || attr(outputs[0], 'name') !== 'result'
          || !/^[A-Za-z0-9_]+$/.test(attr(outputs[0], 'target')))) {
        issues.push({ code: 'VEN-EXTERNAL-TASK-OUTPUT-MAPPING', severity: 'error', elementId: ownerId,
          message: `Agent task '${ownerId}' supports one complete result mapping: result=<processVariable>` });
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
  const issues = validateFlowScopes(moddleFlowScopes(elementRegistry.getAll()));
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
  const bpmnNs = 'http://www.omg.org/spec/BPMN/20100524/MODEL';
  const flowTypes = new Set(['startEvent', 'endEvent', 'intermediateCatchEvent', 'intermediateThrowEvent', 'boundaryEvent',
    'task', 'userTask', 'serviceTask', 'scriptTask', 'manualTask', 'sendTask', 'receiveTask', 'businessRuleTask',
    'callActivity', 'subProcess', 'transaction', 'adHocSubProcess', 'exclusiveGateway', 'inclusiveGateway',
    'parallelGateway', 'complexGateway', 'eventBasedGateway', 'sequenceFlow']);
  const scopes = new Map();
  for (const node of all) {
    if (node.namespaceURI !== bpmnNs || !flowTypes.has(localName(node))) continue;
    if (!scopes.has(node.parentNode)) scopes.set(node.parentNode, []);
    const children = [...node.children];
    scopes.get(node.parentNode).push({
      id: xmlAttr(node, 'id'), type: localName(node), source: xmlAttr(node, 'sourceRef'), target: xmlAttr(node, 'targetRef'),
      default: xmlAttr(node, 'default'), attachedTo: xmlAttr(node, 'attachedToRef'),
      independent: ['true', '1'].includes(xmlAttr(node, 'isForCompensation')) || ['true', '1'].includes(xmlAttr(node, 'triggeredByEvent')),
      condition: children.find(c => c.namespaceURI === bpmnNs && localName(c) === 'conditionExpression')?.textContent,
      link: children.find(c => c.namespaceURI === bpmnNs && localName(c) === 'linkEventDefinition')?.getAttribute('name')
    });
  }
  issues.push(...validateFlowScopes([...scopes.values()]));
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
    } else if (name === 'externalTask') {
      const topic = xmlAttr(node, 'topic');
      const profile = xmlAttr(node, 'agentProfileRef');
      const retries = xmlAttr(node, 'maxRetries');
      const deadline = xmlAttr(node, 'deadlineSeconds');
      if (!/^agent\.[a-z0-9.-]+$/.test(topic)) issues.push({ code: 'VEN-EXTERNAL-TASK-TOPIC', severity: 'error', elementId: ownerId, message: `Agent task '${ownerId}' requires a valid agent.* topic` });
      if (!profile) issues.push({ code: 'VEN-EXTERNAL-TASK-PROFILE', severity: 'error', elementId: ownerId, message: `Agent task '${ownerId}' requires a tenant agent profile` });
      if (!/^[0-9]$/.test(retries)) issues.push({ code: 'VEN-EXTERNAL-TASK-RETRIES', severity: 'error', elementId: ownerId, message: `Agent task '${ownerId}' requires maxRetries between 0 and 9` });
      if (!/^\d+$/.test(deadline) || Number(deadline) < 1 || Number(deadline) > 86400) issues.push({ code: 'VEN-EXTERNAL-TASK-DEADLINE', severity: 'error', elementId: ownerId, message: `Agent task '${ownerId}' requires deadlineSeconds between 1 and 86400` });
      const siblings = [...node.parentNode.children];
      if (siblings.some(sibling => sibling !== node && isVertexNode(sibling) && localName(sibling) === 'connector')) {
        issues.push({ code: 'VEN-EXTERNAL-TASK-MIXED-HANDLER', severity: 'error', elementId: ownerId, message: `'${ownerId}' cannot contain both vertex:connector and vertex:externalTask` });
      }
      const mapping = siblings.find(sibling => isVertexNode(sibling) && localName(sibling) === 'ioMapping');
      const outputs = mapping ? [...mapping.children].filter(child => isVertexNode(child) && localName(child) === 'output') : [];
      if (outputs.length && (outputs.length !== 1
          || xmlAttr(outputs[0], 'name') !== 'result'
          || !/^[A-Za-z0-9_]+$/.test(xmlAttr(outputs[0], 'target')))) {
        issues.push({ code: 'VEN-EXTERNAL-TASK-OUTPUT-MAPPING', severity: 'error', elementId: ownerId,
          message: `Agent task '${ownerId}' supports one complete result mapping: result=<processVariable>` });
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
