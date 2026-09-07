// Validate the semantic graph, including nodes without diagram shapes.
export function validateFlowScopes(scopes) {
  const issues = [];
  const error = (code, elementId, message) => issues.push({ code, severity: 'error', elementId, message });
  for (const nodes of scopes) {
    const byId = new Map(nodes.filter(n => n.type !== 'sequenceFlow').map(n => [n.id, n]));
    const edges = new Map([...byId.keys()].map(id => [id, []]));
    const flows = nodes.filter(n => n.type === 'sequenceFlow');
    for (const flow of flows) {
      if (!byId.has(flow.source) || !byId.has(flow.target)) {
        error('DEP-FLOW-ENDPOINT', flow.id, `Sequence flow '${flow.id}' must connect flow nodes in the same scope.`);
      } else edges.get(flow.source).push(flow.target);
    }
    for (const node of byId.values()) {
      if (node.attachedTo && edges.has(node.attachedTo)) edges.get(node.attachedTo).push(node.id);
      if (node.type === 'intermediateThrowEvent' && node.link) {
        for (const target of byId.values()) {
          if (target.type === 'intermediateCatchEvent' && target.link === node.link) edges.get(node.id).push(target.id);
        }
      }
      if (['exclusiveGateway', 'inclusiveGateway'].includes(node.type)) {
        const outgoing = flows.filter(f => f.source === node.id);
        if (!outgoing.length) error('DEP-GATEWAY-OUTGOING', node.id, `Gateway '${node.id}' has no outgoing sequence flow.`);
        if (outgoing.length > 1) for (const flow of outgoing) {
          if (flow.id !== node.default && !flow.condition?.trim())
            error('DEP-GATEWAY-CONDITION', flow.id, `Configure a condition on non-default branch '${flow.id}'.`);
        }
      }
    }
    const starts = [...byId.values()].filter(n => n.type === 'startEvent');
    if (!starts.length) continue; // BPMN scopes can use implicit starts.
    const queue = [...starts, ...[...byId.values()].filter(n => n.independent)].map(n => n.id);
    const reached = new Set();
    for (let index = 0; index < queue.length; index++) {
      const id = queue[index];
      if (reached.has(id)) continue;
      reached.add(id);
      queue.push(...edges.get(id));
    }
    for (const node of byId.values()) {
      if (!reached.has(node.id)) error('DEP-UNREACHABLE-NODE', node.id, `Flow node '${node.id}' is unreachable from the start events of its scope.`);
    }
  }
  return issues;
}

export function moddleFlowScopes(elements) {
  const scopes = new Map();
  const seen = new Set();
  function visit(bo) {
    if (!bo || seen.has(bo)) return;
    seen.add(bo);
    if (bo.$instanceOf?.('bpmn:FlowNode') || bo.$type === 'bpmn:SequenceFlow') {
      const scope = bo.$parent;
      if (!scopes.has(scope)) scopes.set(scope, []);
      scopes.get(scope).push({
        id: bo.id, type: bo.$type.split(':').pop().replace(/^./, c => c.toLowerCase()),
        source: bo.sourceRef?.id, target: bo.targetRef?.id, default: bo.default?.id,
        condition: bo.conditionExpression?.body, attachedTo: bo.attachedToRef?.id,
        independent: bo.isForCompensation || bo.triggeredByEvent,
        link: bo.eventDefinitions?.find(d => d.$type === 'bpmn:LinkEventDefinition')?.name
      });
    }
    for (const child of bo.flowElements || []) visit(child);
    for (const child of bo.rootElements || []) visit(child);
    if (bo.processRef) visit(bo.processRef);
  }
  for (const element of elements) visit(element.businessObject || element);
  return [...scopes.values()];
}
