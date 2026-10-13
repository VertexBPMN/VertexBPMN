using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class SourceControlAuditMigrationSqlTests
{
	[Theory]
	[InlineData("postgresql", "boolean", "uuid")]
	[InlineData("sqlserver", "bit", "uniqueidentifier")]
	public void Audit_migrations_use_native_provider_types_without_connecting(string provider, string booleanType, string identifierType)
	{
		var bpmnOptions = Configure(new DbContextOptionsBuilder<BpmnDbContext>(), provider);
		using var bpmn = new BpmnDbContext(bpmnOptions.Options);
		var bpmnMigrations = bpmn.Database.GetMigrations().ToArray();
		var bpmnScript = bpmn.GetService<IMigrator>().GenerateScript(bpmnMigrations[^2], bpmnMigrations[^1]);
		Assert.Contains(booleanType, bpmnScript, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("AuditPending\" INTEGER", bpmnScript, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("AuditPending] INTEGER", bpmnScript, StringComparison.OrdinalIgnoreCase);
		var centralOptions = Configure(new DbContextOptionsBuilder<ProcessMiningEventDbContext>(), provider);
		using var central = new ProcessMiningEventDbContext(centralOptions.Options);
		var centralMigrations = central.Database.GetMigrations().ToArray();
		var centralScript = central.GetService<IMigrator>().GenerateScript(centralMigrations[^2], centralMigrations[^1]);
		Assert.Contains(identifierType, centralScript, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("512", centralScript, StringComparison.Ordinal);
		Assert.DoesNotContain("SourceControlEventId\" TEXT", centralScript, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("SourceControlEventId] TEXT", centralScript, StringComparison.OrdinalIgnoreCase);
	}

	private static DbContextOptionsBuilder<T> Configure<T>(DbContextOptionsBuilder<T> options, string provider) where T : DbContext =>
		provider == "postgresql" ? options.UseNpgsql("Host=example.invalid;Database=unused")
			: options.UseSqlServer("Server=example.invalid;Database=unused;Integrated Security=true");
}
