// VertexBPMN SDK Example (Java)
import io.vertexbpmn.sdk.BpmnClient;

public class Example {
    public static void main(String[] args) {
        BpmnClient client = new BpmnClient("http://localhost:5263/api");
        String processId = client.deployProcess("path/to/model.bpmn");
        ProcessInstance instance = client.startProcess(processId, Map.of("key", "value"));
        String status = client.getProcessStatus(instance.getId());
        System.out.println("Status: " + status);
    }
}
