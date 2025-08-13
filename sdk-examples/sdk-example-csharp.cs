// VertexBPMN SDK Example (C#)
using VertexBPMN.SDK;

class Example {
    static void Main() {
        var client = new BpmnClient("http://localhost:5263/api");
        var processId = client.DeployProcess("path/to/model.bpmn");
        var instance = client.StartProcess(processId, new { key = "value" });
        var status = client.GetProcessStatus(instance.Id);
        System.Console.WriteLine($"Status: {status}");
    }
}
