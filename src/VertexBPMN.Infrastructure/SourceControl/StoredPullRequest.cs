using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record StoredPullRequest(int SchemaVersion, Guid RepositoryId, PullRequestCommand Command,
    PullRequestReceipt? Receipt,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] RepositoryHostingTarget? HostingTarget = null);
