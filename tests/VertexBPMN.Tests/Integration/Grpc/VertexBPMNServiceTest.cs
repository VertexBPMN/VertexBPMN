//using VertexBPMN.Api.Grpc.Mcp;

//namespace VertexBPMN.Tests.Grpc;

//using global::Grpc.Net.Client;
//using VertexBPMN.Api.Grpc;
//using VertexBPMN.Api.Grpc.Mcp;
//public class VertexBPMNServiceTest
//{
//    [Fact]
//    public void Test1()
//    {
//        using var channel = GrpcChannel.ForAddress("https://localhost:5001");
//        var client = new VertexBPMNService.VertexBPMNServiceClient(channel);

//        var reg = await client.RegisterCmmnModelAsync(new RegisterCmmnRequest
//        {
//            CaseId = "case-123",
//            CmmnXml = "<definitions>...</definitions>"
//        });

//        var exec = await client.ExecuteCaseAsync(new ExecuteCaseRequest { CaseId = "case-123" });
//        foreach (var line in exec.Trace)
//        {
//            Console.WriteLine(line);
//        }
//    }
//}

//public class VertexBPMNMCPServiceClientTest
//{

//public void Test1()
//{
//    var channel = GrpcChannel.ForAddress("https://localhost:5001");
//    var mcp = new VertexBPMNMCPService.VertexBPMNMCPServiceClient(channel);

//    var exec = await mcp.ExecuteCaseAsync(new VertexBPMN.Api.Grpc.Mcp.ExecuteCaseRequest { CaseId = "case-42" });
//    foreach (var line in exec.Trace)
//        Console.WriteLine(line);

//    var hist = await mcp.GetHistoricalContextAsync(new HistoricalContextRequest { CaseId = "case-42" });
//    Console.WriteLine($"History entries: {hist.HistoricalData.Count}");
//}
//}