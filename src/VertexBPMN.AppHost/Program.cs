using VertexBPMN.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
VertexBpmnAppHostTopology.Configure(builder);
await builder.Build().RunAsync();
