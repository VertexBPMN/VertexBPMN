//using System.Threading.Tasks;
//using Microsoft.Data.Sqlite;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using VertexBPMN.Domain.Entities;
//using VertexBPMN.Infrastructure.Persistence;
//using Xunit;

//namespace VertexBPMN.Tests.Infrastructure;

//public class SchemaVerificationTests : IClassFixture<CustomWebApplicationFactory>
//{
//    private readonly CustomWebApplicationFactory _factory;

//    public SchemaVerificationTests(CustomWebApplicationFactory factory) => _factory = factory;

//    [Fact]
//    public async Task Jobs_Table_Exists_In_Test_Database()
//    {
//        using var scope = _factory.Services.CreateScope();
//        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();

//         await db.Database.MigrateAsync(); // or EnsureCreatedAsync()

//        var exists = await db.Database.ExecuteSqlRawAsync(
//            "SELECT 1 FROM sqlite_master WHERE type='table' AND name='Jobs';") == 1;

//        Assert.True(exists, "Jobs table was not found in sqlite_master.");

//        var colCount = await db.Database
//            .ExecuteSqlRawAsync("PRAGMA table_info('Jobs');");
//        Assert.True(colCount > 0, "PRAGMA returned no columns for Jobs (table missing).");

//        await Assert
//            .ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Set<Job>().CountAsync()); // if you expect failure
//        // or if schema initialized:
//        var any = await db.Set<Job>().AnyAsync();

//        var applied = await db.Database.GetAppliedMigrationsAsync();
//        Assert.Contains(applied, m => m.Contains("Jobs", StringComparison.OrdinalIgnoreCase));
//    }
//}