using System.Diagnostics.CodeAnalysis;

namespace VertexBPMN.Portal.Bff;

/// <summary>
/// A released (never latest/draft) application revision as served to the portal
/// client. The version and content are resolved server-side against the release
/// binding; the caller cannot choose a revision.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract record kept adjacent to the other released-runtime contracts.")]
public sealed record ReleasedApplication(string ApplicationId, int Version, string ContentJson);

/// <summary>Released application metadata for the portal client (no draft/source secrets).</summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract record kept adjacent to the other released-runtime contracts.")]
public sealed record ReleasedApplicationDto(string ApplicationId, int Version, int PageCount, IReadOnlyList<string> PageRoutes);

/// <summary>Result of resolving and executing a data-source binding against the released release.</summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract record kept adjacent to the other released-runtime contracts.")]
public sealed record DataSourceResult(string Binding, string Status, int RowCount, DateTimeOffset Timestamp);

/// <summary>Server-computed projection of a task's lifecycle state.</summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract record kept adjacent to the other released-runtime contracts.")]
public sealed record TaskStatusProjection(Guid TaskId, string Status, string? CandidateRole, DateTimeOffset? Created, DateTimeOffset? Completed);

/// <summary>
/// Raw task lifecycle data as retrieved from the Vertex REST API, before the BFF
/// normalizes it into the stable status vocabulary. Kept separate from the projection
/// so the portal never surfaces undocumented, raw backend status strings.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract record kept adjacent to the other released-runtime contracts.")]
public sealed record BffTaskState(Guid TaskId, string? RawStatus, string? CandidateRole, DateTimeOffset? Created, DateTimeOffset? Completed);

/// <summary>Outcome of a released claim/complete/start action.</summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract record kept adjacent to the other released-runtime contracts.")]
public sealed record BffActionResult(string Action, Guid? TaskId, string Status);

/// <summary>
/// An action the released application is allowed to invoke. Only a narrow,
/// server-resolved set of actions is sanctioned; everything else is rejected.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract constant holder kept adjacent to the released-runtime contracts.")]
public static class ReleasedActions
{
    public const string StartProcess = "start-process";
    public const string ClaimTask = "claim-task";
    public const string CompleteTask = "complete-task";
    public const string DataSourceRead = "data-source-read";
}

/// <summary>Rejected data-source/action binding that is not present in the released release.</summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract record kept adjacent to the other released-runtime contracts.")]
public sealed class BffUnsanctionedBindingException(string message) : Exception(message);

/// <summary>Raised when an action is not sanctioned by the released revision.</summary>
[SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed BFF action signatures and controller activation.")]
[SuppressMessage("Design", "MA0048", Justification = "Small BFF contract record kept adjacent to the other released-runtime contracts.")]
public sealed class BffUnsanctionedActionException(string message) : Exception(message);
