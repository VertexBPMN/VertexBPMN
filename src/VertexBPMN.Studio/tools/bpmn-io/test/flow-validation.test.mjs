import { test } from 'node:test';
import assert from 'node:assert/strict';
import { BpmnModdle } from 'bpmn-moddle';
import { readFile } from 'node:fs/promises';
import { moddleFlowScopes, validateFlowScopes } from '../src/flow-validation.js';

const sharedCases = JSON.parse(await readFile(new URL('../../../../../tests/VertexBPMN.Tests/TestData/editor-validation-cases.json', import.meta.url), 'utf8'));
for (const item of sharedCases) test(`shared client/server corpus: ${item.name}`, async () => {
  const { rootElement } = await BpmnModdle().fromXML(item.xml);
  assert.deepEqual(validateFlowScopes(moddleFlowScopes([rootElement])).map(i => i.code).sort(), item.codes.sort());
});

async function validate(body) {
  const { rootElement } = await BpmnModdle().fromXML(`<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="urn:test"><process id="p">${body}</process></definitions>`);
  return validateFlowScopes(moddleFlowScopes([rootElement]));
}
const base = '<startEvent id="s"/><endEvent id="e"/><sequenceFlow id="f" sourceRef="s" targetRef="e"/>';
test('rejects a user task omitted from the executable path, even without DI', async () => {
  assert.deepEqual((await validate(base + '<userTask id="orphan"/>')).map(i => i.elementId), ['orphan']);
});
test('accepts a connected user approval task', async () => {
  assert.deepEqual(await validate('<startEvent id="s"/><userTask id="u"/><endEvent id="e"/><sequenceFlow id="a" sourceRef="s" targetRef="u"/><sequenceFlow id="b" sourceRef="u" targetRef="e"/>'), []);
});
test('rejects an unconfigured IF branch but accepts a condition and a default', async () => {
  const body = '<startEvent id="s"/><exclusiveGateway id="g" default="no"/><endEvent id="e"/><sequenceFlow id="a" sourceRef="s" targetRef="g"/><sequenceFlow id="no" sourceRef="g" targetRef="e"/><sequenceFlow id="yes" sourceRef="g" targetRef="e">CONDITION</sequenceFlow>';
  assert.equal((await validate(body.replace('CONDITION', '')))[0].code, 'DEP-GATEWAY-CONDITION');
  assert.deepEqual(await validate(body.replace('CONDITION', '<conditionExpression>approved = true</conditionExpression>')), []);
});
test('uses independent scopes and accepts compensation and event subprocesses', async () => {
  assert.deepEqual(await validate(base + '<userTask id="compensate" isForCompensation="true"/><subProcess id="handler" triggeredByEvent="true"><startEvent id="hs"><errorEventDefinition/></startEvent><endEvent id="he"/><sequenceFlow id="hf" sourceRef="hs" targetRef="he"/></subProcess>'), []);
});
test('follows boundary events and link jumps', async () => {
  assert.deepEqual(await validate('<startEvent id="s"/><task id="t"/><boundaryEvent id="b" attachedToRef="t"><timerEventDefinition/></boundaryEvent><intermediateThrowEvent id="lt"><linkEventDefinition name="jump"/></intermediateThrowEvent><intermediateCatchEvent id="lc"><linkEventDefinition name="jump"/></intermediateCatchEvent><endEvent id="e"/><sequenceFlow id="a" sourceRef="s" targetRef="t"/><sequenceFlow id="c" sourceRef="t" targetRef="lt"/><sequenceFlow id="d" sourceRef="lc" targetRef="e"/><sequenceFlow id="h" sourceRef="b" targetRef="e"/>'), []);
});
