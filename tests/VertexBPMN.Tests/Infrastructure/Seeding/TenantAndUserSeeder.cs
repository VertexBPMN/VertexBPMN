using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Infrastructure.Seeding;

public sealed class TenantAndUserSeeder : TestDataSeederBase
{
    public override int Order => 10;

    public override async Task SeedAsync(IServiceScope scope, CancellationToken cancellationToken = default)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TenantAndUserSeeder>>();
        var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var bpmnDb   = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();

        // Seed tenant
        if (tenantDb.Model.FindEntityType(typeof(Tenant)) is not null &&
            await IsEmptyAsync(tenantDb.Tenants, cancellationToken))
        {
            tenantDb.Tenants.Add(new Tenant
            {
                Id = Guid.NewGuid().ToString(),
                Name = "default",
                Description = "Default tenant",
                //IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await tenantDb.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded default tenant");
        }

        // Seed user
        if (bpmnDb.Model.FindEntityType(typeof(User)) is not null &&
            await IsEmptyAsync(bpmnDb.Users, cancellationToken))
        {
            bpmnDb.Users.Add(new User
            {
                Id = "demo",
                Username =  "Demo User",
                Email = "demo@example.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await bpmnDb.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded demo user");
        }
    }
}   