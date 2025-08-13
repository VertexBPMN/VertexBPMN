// VertexBPMN SDK Example (JavaScript)
const { BpmnClient } = require('vertexbpmn-sdk');

const client = new BpmnClient('http://localhost:5263/api');
const processId = client.deployProcess('path/to/model.bpmn');
const instance = client.startProcess(processId, { key: 'value' });
const status = client.getProcessStatus(instance.id);
console.log(`Status: ${status}`);
