using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace VertexBPMN.Tests.Infrastructure.Seeding;

public abstract class TestDataSeederBase : ITestDataSeeder
{
    public virtual int Order => 0;

    public abstract Task SeedAsync(IServiceScope scope, CancellationToken cancellationToken = default);

    protected static async Task<bool> IsEmptyAsync<TEntity>(DbSet<TEntity> set, CancellationToken ct)
        where TEntity : class => !await set.AnyAsync(ct);
}