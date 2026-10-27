using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

internal sealed class EndpointCheckpointStore : IAsyncDisposable
{
    private static readonly string[] PathParameters = ["customerId"];
    private readonly string _path;
    internal EndpointCheckpointStore(string directory = "/home/azureuser/.hermes/cache/scratch/lc09b-s1")
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, $"endpoint-{Guid.NewGuid():N}.db");
    }
    internal BpmnDbContext Open() => new(new DbContextOptionsBuilder<BpmnDbContext>()
        .UseSqlite($"Data Source={_path};Pooling=False").Options);
    internal static PersistentApplicationDefinitionService Service(BpmnDbContext db) => new(db,
        Mock.Of<IAuditLogService>(), access: new ApplicationAccessContext("editor",
            new HashSet<string>(["application.design", "application.publish"], StringComparer.Ordinal)));
    internal static string Content(string actionType = "data-source-read", string target = "customers",
        string method = "GET", string route = "/endpoints/customers/{customerId}", string page = "home") =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            dataSources = new[] { new { key = "customers", bindingVersion = 2 } },
            pages = new[] { new { id = "home", route = "/home", actions = new[]
                { new { type = "data-source-read", version = "1.0.0", dataSource = "customers" } } } },
            endpoints = new[] { new
            {
                key = "customers", method, route, pathParameters = PathParameters,
                inputSchema = new { type = "object" }, outputSchema = new { type = "object" },
                permission = "ReadOnly", limits = new { timeoutSeconds = 5, maxRequestBytes = 1024, maxResponseBytes = 4096 },
                action = new { pageId = page, type = actionType, version = "1.0.0", target }
            } }
        });
    public ValueTask DisposeAsync()
    {
        File.Delete(_path);
        File.Delete(_path + "-wal");
        File.Delete(_path + "-shm");
        return ValueTask.CompletedTask;
    }
}
