namespace VertexBPMN.Tests.Integration.Applications;

internal static class EndpointRuntimeContent
{
    internal const string Json = """
        {"dataSources":[{"key":"invoices","bindingVersion":2,"query":"personal-task-inbox"}],
         "pages":[{"id":"home","route":"/home","actions":[
           {"type":"data-source-read","version":"1.0.0","dataSource":"invoices"}]}],
         "endpoints":[{"key":"search","method":"GET","route":"/endpoints/search/{search}",
           "pathParameters":["search"],"permission":"ReadOnly",
           "limits":{"timeoutSeconds":5,"maxRequestBytes":1024,"maxResponseBytes":4096},
           "inputSchema":{"type":"object","properties":{"search":{"type":"string","maxLength":32}},"required":["search"],"additionalProperties":false},
           "outputSchema":{"type":"object","properties":{"items":{"type":"array","items":{"type":"object","properties":{"id":{"type":"string"},"name":{"type":"string"}},"required":["id","name"],"additionalProperties":false}}},"required":["items"],"additionalProperties":false},
           "action":{"pageId":"home","type":"data-source-read","version":"1.0.0","target":"invoices"}}]}
        """;
}
