using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

/// <summary>Opt-in against the dedicated WSLC realm; never runs in ordinary CI.</summary>
public sealed class KeycloakGitLocalAcceptanceTests
{
	[Fact]
	[Trait("Category", "KeycloakGitLocalAcceptance")]
	public async Task Real_keycloak_rechecks_roles_user_state_tenant_and_protected_credentials()
	{
		Assert.SkipUnless(Environment.GetEnvironmentVariable("VERTEXBPMN_KEYCLOAK_GIT_ACCEPTANCE") == "1",
			"Explicit local Keycloak acceptance opt-in required.");
		var token = TestContext.Current.CancellationToken;
		var suffix = Guid.NewGuid().ToString("N");
		var clientName = "git-reader-" + suffix;
		var userName = "git-user-" + suffix;
		var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
		string? clientId = null, userId = null, groupId = null;
		try
		{
			await AdminAsync(token, "create", "clients", "-s", "clientId=" + clientName,
				"-s", "enabled=true", "-s", "publicClient=false", "-s", "serviceAccountsEnabled=true", "-s", "secret=" + secret);
			clientId = await LookupAsync("clients", "clientId=" + clientName, token);
			using var serviceAccount = JsonDocument.Parse(await AdminAsync(token, "get", "clients/" + clientId + "/service-account-user"));
			var serviceUser = serviceAccount.RootElement.GetProperty("username").GetString()!;
			await AdminAsync(token, "add-roles", "--uusername", serviceUser, "--cclientid", "realm-management",
				"--rolename", "view-users", "--rolename", "query-clients", "--rolename", "view-clients");
			await AdminAsync(token, "create", "users", "-s", "username=" + userName,
				"-s", "enabled=true", "-s", "attributes.tenant_id=[\"tenant-a\"]");
			userId = await LookupAsync("users", "username=" + userName, token);
			await AdminAsync(token, "add-roles", "--uusername", userName, "--cclientid", "vertexbpmn-api", "--rolename", "ProcessManager");

			await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite("Data Source=:memory:").Options);
			await db.Database.OpenConnectionAsync(token);
			await db.Database.EnsureCreatedAsync(token);
			await using var auditDb = new ProcessMiningEventDbContext(new DbContextOptionsBuilder<ProcessMiningEventDbContext>().UseSqlite("Data Source=:memory:").Options);
			await auditDb.Database.OpenConnectionAsync(token);
			await auditDb.Database.EnsureCreatedAsync(token);
			var credentials = new PersistentCredentialService(db, new EphemeralDataProtectionProvider(),
				new AuditLogService(auditDb), NullLogger<PersistentCredentialService>.Instance);
			var credential = await credentials.CreateAsync("tenant-a", new CredentialWriteRequest(clientName, "KeycloakAdmin", null,
				new Dictionary<string, string> { ["clientId"] = clientName, ["clientSecret"] = secret }), token);
			var authority = "http://localhost:58080/realms/vertexbpmn";
			var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Jwt:Authority"] = authority,
				["Jwt:Audience"] = "vertexbpmn-api",
				["OperationalMode"] = "OidcTest"
			}).Build();
			var resolver = new KeycloakSourceControlActorResolver(credentials, configuration, Options.Create(new SourceControlOptions
			{
				Enabled = true,
				IdentityAuthority = authority,
				IdentityCredentialReference = credential.Id
			}));
			var actor = new SourceControlContext("tenant-a", userId);
			Assert.Contains("ProcessManager", await resolver.ResolveAsync(actor, token));
			Assert.DoesNotContain(secret, (await db.Credentials.SingleAsync(token)).ProtectedValues);
			await AdminAsync(token, "add-roles", "--uusername", userName, "--cclientid", "vertexbpmn-api", "--rolename", "Admin");
			var protection = new EphemeralDataProtectionProvider();
			var workspaceRoot = Path.Combine(Path.GetTempPath(), "vertex-keycloak-git-" + suffix);
			var gitOptions = Options.Create(new SourceControlOptions
			{
				Enabled = true,
				IdentityAuthority = authority,
				AllowedHosts = ["github.com"],
				WorkspaceRoot = workspaceRoot
			});
			var store = new PersistentSourceControlStore(db, protection, gitOptions);
			var roles = await resolver.ResolveAsync(actor, token);
			var repositoryId = Guid.NewGuid();
			var access = await store.CreateBindingAsync(actor, roles, new(repositoryId, actor.TenantId,
				new("https://github.com/acceptance-only/not-contacted.git"), null, "master", "release", ["models"]), token);
			Assert.True(await store.ReplaceGrantsAsync(actor, repositoryId, roles, access.Revision,
				[new(actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Commit)], token));
			var revision = new GitCommitId(new string('a', 40));
			var generation = Guid.NewGuid();
			var session = await store.CreateSessionAsync(actor, repositoryId, roles, revision, generation, token);
			var snapshot = new ModelSnapshot("models/test.bpmn", SourceModelKind.Bpmn, generation, 1,
				System.Text.Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><process id=\"acceptance\"/></definitions>"));
			Assert.True(await store.SaveSnapshotsAsync(actor, session, roles, 0, [snapshot], token));
			var job = await store.EnqueueCommitAsync(actor, repositoryId, roles, new(suffix),
				new(session, 1, revision, SourceControlInputPolicy.WorkBranch(session), "Local authorization acceptance", [snapshot]), token);
			Assert.Equal(1L, await store.TryClaimAsync(actor.TenantId, job, "acceptance", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), token));
			await AdminAsync(token, "remove-roles", "--uusername", userName, "--cclientid", "vertexbpmn-api", "--rolename", "Admin");
			await AdminAsync(token, "remove-roles", "--uusername", userName, "--cclientid", "vertexbpmn-api", "--rolename", "ProcessManager");
			Assert.Empty(await resolver.ResolveAsync(actor, token));
			var executor = new SourceControlCommitExecutor(store, new(db, protection, gitOptions), new(gitOptions),
				new(new SourceControlCredentialResolver(store, credentials), gitOptions));
			var rejectedJob = await Assert.ThrowsAsync<SourceControlSecurityException>(() => executor.ExecuteAsync(actor, job,
				"acceptance", 1, cancellation => resolver.ResolveAsync(actor, cancellation), token));
			// Without Read, repository existence is deliberately concealed.
			Assert.Equal(SourceControlErrorCode.NotFound, rejectedJob.Code);
			await AdminAsync(token, "add-roles", "--uusername", userName, "--cclientid", "vertexbpmn-api", "--rolename", "ReadOnly");
			var readOnlyJob = await Assert.ThrowsAsync<SourceControlSecurityException>(() => executor.ExecuteAsync(actor, job,
				"acceptance", 1, cancellation => resolver.ResolveAsync(actor, cancellation), token));
			Assert.Equal(SourceControlErrorCode.Forbidden, readOnlyJob.Code);
			await AdminAsync(token, "remove-roles", "--uusername", userName, "--cclientid", "vertexbpmn-api", "--rolename", "ReadOnly");
			Assert.False(Directory.Exists(workspaceRoot));
			Assert.Null((await db.SourceControlOperations.SingleAsync(token)).ProtectedResult);

			await AdminAsync(token, "create", "groups", "-s", "name=" + userName);
			groupId = await LookupAsync("groups", "search=" + userName, token);
			await AdminAsync(token, "add-roles", "--gid", groupId, "--cclientid", "vertexbpmn-api", "--rolename", "ProcessManager");
			await AdminAsync(token, "update", "users/" + userId + "/groups/" + groupId);
			Assert.Contains("ProcessManager", await resolver.ResolveAsync(actor, token));

			await AdminAsync(token, "update", "users/" + userId, "-s", "enabled=false");
			await RejectAsync(resolver, actor, SourceControlErrorCode.Forbidden, token);
			await AdminAsync(token, "update", "users/" + userId, "-s", "enabled=true", "-s", "attributes.tenant_id=[\"tenant-b\"]");
			await RejectAsync(resolver, actor, SourceControlErrorCode.NotFound, token);
			await AdminAsync(token, "update", "users/" + userId, "-s", "attributes.tenant_id=[\"tenant-a\"]");
			await credentials.RotateSecretAsync("tenant-a", credential.Id, new("clientSecret", "invalid-fixture-secret"), token);
			await RejectAsync(resolver, actor, SourceControlErrorCode.ProviderUnavailable, token);
			await credentials.RotateSecretAsync("tenant-a", credential.Id, new("clientSecret", secret), token);
			Assert.Contains("ProcessManager", await resolver.ResolveAsync(actor, token));
			var rotatedSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
			await AdminAsync(token, "update", "clients/" + clientId, "-s", "secret=" + rotatedSecret);
			await RejectAsync(resolver, actor, SourceControlErrorCode.ProviderUnavailable, token);
			Assert.True(await credentials.RotateSecretAsync("tenant-a", credential.Id, new("clientSecret", rotatedSecret), token));
			Assert.Contains("ProcessManager", await resolver.ResolveAsync(actor, token));
			await RejectAsync(resolver, new("tenant-b", userId), SourceControlErrorCode.CredentialUnavailable, token);
			await AdminAsync(token, "delete", "users/" + userId);
			userId = null;
			await RejectAsync(resolver, actor, SourceControlErrorCode.ProviderUnavailable, token);
		}
		finally
		{
			using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(2));
			if (userId is not null)
			{
				await AdminAsync(cleanup.Token, "delete", "users/" + userId);
			}
			if (groupId is not null)
			{
				await AdminAsync(cleanup.Token, "delete", "groups/" + groupId);
			}
			if (clientId is not null)
			{
				await AdminAsync(cleanup.Token, "delete", "clients/" + clientId);
			}
		}
	}

	private static async Task RejectAsync(KeycloakSourceControlActorResolver resolver, SourceControlContext actor,
		SourceControlErrorCode code, CancellationToken token)
	{
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => resolver.ResolveAsync(actor, token));
		Assert.Equal(code, error.Code);
	}

	private static async Task<string> LookupAsync(string resource, string query, CancellationToken token)
	{
		using var json = JsonDocument.Parse(await AdminAsync(token, "get", resource, "-q", query));
		Assert.Equal(1, json.RootElement.GetArrayLength());
		return json.RootElement[0].GetProperty("id").GetString()!;
	}

	private static async Task<string> AdminAsync(CancellationToken token, params string[] arguments)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
		deadline.CancelAfter(TimeSpan.FromSeconds(45));
		var start = new ProcessStartInfo("wslc.exe")
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		foreach (var argument in new[] { "exec", "vertexbpmn-keycloak-test", "/opt/keycloak/bin/kcadm.sh" }.Concat(arguments).Concat(["-r", "vertexbpmn"]))
		{
			start.ArgumentList.Add(argument);
		}
		using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start local acceptance administration.");
		try
		{
			var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
			var error = process.StandardError.ReadToEndAsync(deadline.Token);
			await process.WaitForExitAsync(deadline.Token);
			await error;
			Assert.True(process.ExitCode == 0, "Local Keycloak fixture administration failed; command details are redacted.");
			return await output;
		}
		finally
		{
			if (!process.HasExited)
			{
				process.Kill(true);
				await process.WaitForExitAsync(CancellationToken.None);
			}
		}
	}
}
