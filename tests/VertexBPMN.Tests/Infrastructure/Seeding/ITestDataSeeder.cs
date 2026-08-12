using Microsoft.Extensions.DependencyInjection;

namespace VertexBPMN.Tests.Infrastructure.Seeding;

public interface ITestDataSeeder
{
    /// <summary>
    /// Order controls execution sequence (lower first).
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Seed data. Must be idempotent.
    /// </summary>
    Task SeedAsync(IServiceScope scope, CancellationToken cancellationToken = default);
}