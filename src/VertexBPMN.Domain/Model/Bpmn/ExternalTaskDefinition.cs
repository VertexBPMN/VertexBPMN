using System.Globalization;

namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>Immutable external-work configuration; permissions are resolved by the server.</summary>
public sealed record ExternalTaskDefinition(string Topic, string? AgentProfileRef, int MaxRetries, int DeadlineSeconds)
{
    public int MaxAttempts => checked(MaxRetries + 1);

    public static ExternalTaskDefinition? FromAttributes(IReadOnlyDictionary<string, string>? attributes)
    {
        const string prefix = "vertex:externalTask";
        if (attributes is null || !attributes.Keys.Any(key => key == prefix || key.StartsWith(prefix + ".", StringComparison.Ordinal)))
            return null;
        var topic = attributes.GetValueOrDefault(prefix + ".topic") ?? "";
        var profile = attributes.GetValueOrDefault(prefix + ".agentProfileRef");
        if (topic.Length is < 1 or > 128 || topic[0] is < 'a' or > 'z'
            || topic.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-')))
            throw new InvalidOperationException("external_task_invalid_topic");
        if (topic.StartsWith("agent.", StringComparison.Ordinal) != !string.IsNullOrWhiteSpace(profile))
            throw new InvalidOperationException("external_task_invalid_profile");
        if (!int.TryParse(attributes.GetValueOrDefault(prefix + ".maxRetries"), NumberStyles.None, CultureInfo.InvariantCulture, out var retries)
            || retries is < 0 or > 9)
            throw new InvalidOperationException("external_task_invalid_retries");
        if (!int.TryParse(attributes.GetValueOrDefault(prefix + ".deadlineSeconds"), NumberStyles.None, CultureInfo.InvariantCulture, out var deadline)
            || deadline is < 1 or > 86400)
            throw new InvalidOperationException("external_task_invalid_deadline");
        return new(topic, profile, retries, deadline);
    }
}
