using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VertexBPMN.Api.Controllers;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class RequiredRequestFieldTests
{
	[Fact]
	public void MissingRequiredValueFieldsAreRejected()
	{
		var generation = Guid.NewGuid();
		object[] requests =
		[
			new ConnectorController.SetConnectorEnabledRequest(null, false),
			new ExternalTaskController.ExternalTaskHeartbeatRequest(generation, 1),
			new ExternalTaskController.ExternalTaskCompleteRequest(generation, 1, Guid.NewGuid(), JsonSerializer.SerializeToElement(new { accepted = true })),
			new ExternalTaskController.ExternalTaskFailRequest(generation, 1, Guid.NewGuid(), "technical", "timeout"),
			new SourceControlController.GrantsRequest(0, []),
			new SourceControlController.ProfileAssignment(1, generation, 1),
			new SourceControlController.ReadRequest(VertexBPMN.SourceControl.Abstractions.GitReadQueryKind.File,
				"read", "commit", "models/process.bpmn", 0, null, generation, null),
			new SourceControlController.SessionRequest("base", generation),
			new SourceControlController.SnapshotResponse("process.bpmn", generation, 0, [1], "hash"),
			new SourceControlController.SnapshotsRequest(0, []),
			new SourceControlController.CommitRequest("key", generation, 0, "base", "message", []),
			new SourceControlController.PushRequest("key", generation, null),
			new SourceControlController.PullRequestRequest("key", generation, "master", "title", "description"),
			new SourceControlController.AdvanceRequest(generation, 0)
		];

		var checkedFields = 0;
		foreach (var request in requests)
		{
			var type = request.GetType();
			var json = JsonSerializer.SerializeToNode(request, type)!.AsObject();
			Assert.NotNull(JsonSerializer.Deserialize(json.ToJsonString(), type));
			foreach (var property in type.GetProperties().Where(property => property.IsDefined(typeof(JsonRequiredAttribute))))
			{
				var underposted = json.DeepClone().AsObject();
				Assert.True(underposted.Remove(property.Name));
				Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(underposted.ToJsonString(), type));
				checkedFields++;
			}
		}

		Assert.Equal(27, checkedFields);
	}

	[Fact]
	public void ExplicitFalseAndZeroRemainValid()
	{
		var enabled = JsonSerializer.Deserialize<ConnectorController.SetConnectorEnabledRequest>("{\"Enabled\":false}");
		var grants = JsonSerializer.Deserialize<SourceControlController.GrantsRequest>("{\"Revision\":0,\"Grants\":[]}");
		Assert.NotNull(enabled);
		Assert.False(enabled.Enabled);
		Assert.NotNull(grants);
		Assert.Equal(0, grants.Revision);
	}

	[Fact]
	public void OptionalClaimAndHeartbeatLimitsKeepTheirDefaults()
	{
		var claim = JsonSerializer.Deserialize<ExternalTaskController.ExternalTaskClaimRequest>("{}");
		var heartbeat = JsonSerializer.Deserialize<ExternalTaskController.ExternalTaskHeartbeatRequest>($"{{\"LeaseId\":\"{Guid.NewGuid()}\",\"LeaseGeneration\":1}}");
		Assert.NotNull(claim);
		Assert.Equal(1, claim.MaxTasks);
		Assert.Equal(60, claim.LeaseSeconds);
		Assert.NotNull(heartbeat);
		Assert.Equal(60, heartbeat.LeaseSeconds);
	}
}
