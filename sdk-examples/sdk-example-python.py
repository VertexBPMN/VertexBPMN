# VertexBPMN SDK Example (Python)
import vertexbpmn_sdk

client = vertexbpmn_sdk.BpmnClient("http://localhost:5263/api")
process_id = client.deploy_process("path/to/model.bpmn")
instance = client.start_process(process_id, {"key": "value"})
status = client.get_process_status(instance.id)
print(f"Status: {status}")
