namespace VertexBPMN.Tests.Integration.Applications;

internal static class EndpointOperationContent
{
    private static readonly string[] NoParameters = [];
    private static readonly string[] ResultRequired = ["processInstanceId"];

    internal static string Json(string processKey, string processId, string permission = "ReadOnly",
        bool includeEndpoint = true)
    {
        var endpoints = includeEndpoint
            ? new object[]
            {
                new
                {
                    key = "start", method = "POST", route = "/endpoints/start",
                    pathParameters = NoParameters, permission,
                    limits = new { timeoutSeconds = 5, maxRequestBytes = 1024, maxResponseBytes = 4096 },
                    inputSchema = new { type = "object" },
                    outputSchema = new
                    {
                        type = "object",
                        properties = new { processInstanceId = new { type = "string" } },
                        required = ResultRequired, additionalProperties = false
                    },
                    action = new { pageId = "home", type = "start-process", version = "1.0.0", target = processId }
                }
            }
            : Array.Empty<object>();
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            localizations = new { },
            references = new[] { new { type = "process", key = processKey } },
            dataSources = Array.Empty<object>(),
            pages = new[]
            {
                new { id = "home", route = "/home", actions = new[]
                {
                    new { type = "start-process", version = "1.0.0", processDefinitionId = processId }
                } }
            },
            endpoints
        });
    }

    internal static string Bpmn(string processId) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" targetNamespace=\"https://vertexbpmn.dev/test\">" +
        $"<process id=\"{processId}\" isExecutable=\"true\"><startEvent id=\"start\" /></process></definitions>";
}
