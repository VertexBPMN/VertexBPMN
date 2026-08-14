import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { BpmnModdle } from 'bpmn-moddle';

const here = dirname(fileURLToPath(import.meta.url));
const vertex = JSON.parse(await readFile(join(here, '../src/vertex.json'), 'utf8'));

const xml = `<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0"
                  id="Defs_1" targetNamespace="https://vertexbpmn.io/schema/bpmn/1.0">
  <bpmn:process id="p1" isExecutable="true">
    <bpmn:serviceTask id="Task_CallApi" name="Call API">
      <bpmn:extensionElements>
        <vertex:connector type="http" operationId="http.request" credentialRef="cred-orders-api" timeoutMs="30000" />
        <vertex:ioMapping>
          <vertex:input name="url" expression="${orderApiUrl}" />
          <vertex:output name="response" target="httpResponse" />
        </vertex:ioMapping>
      </bpmn:extensionElements>
    </bpmn:serviceTask>
  </bpmn:process>
</bpmn:definitions>`;

const moddle = BpmnModdle({ vertex });
const { rootElement } = await moddle.fromXML(xml);
const process = rootElement.get('rootElements').find(e => e.id === 'p1');
const task = process.get('flowElements').find(e => e.id === 'Task_CallApi');
const values = task.get('extensionElements').get('values');
const connector = values.find(v => v.$type === 'vertex:Connector');
if (!connector) {
  throw new Error('expected vertex:Connector after import');
}
connector.set('timeoutMs', 15000);
const { xml: out } = await moddle.toXML(rootElement, { format: true });
for (const token of ['vertex:connector', 'operationId="http.request"', 'timeoutMs="15000"', 'vertex:input', 'name="url"', 'https://vertexbpmn.io/schema/bpmn/1.0']) {
  if (!out.includes(token)) {
    throw new Error(`roundtrip XML missing ${token}\n${out}`);
  }
}
console.log('vertex-moddle roundtrip ok');
