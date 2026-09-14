using Microsoft.Extensions.Options;

namespace VertexBPMN.AgentWorker;

public sealed class ExternalTaskWorkerOptions
{
    public const string SectionName = "ExternalTaskWorker";
    public bool Enabled { get; set; }
    public string BaseAddress { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string[] Topics { get; set; } = [];
    public int MaxConcurrency { get; set; } = 1;
    public int MaxTasksPerClaim { get; set; } = 1;
    public int LeaseSeconds { get; set; } = 60;
    public int HeartbeatSeconds { get; set; } = 20;
    public int PollMilliseconds { get; set; } = 1000;
    public int MaximumBackoffSeconds { get; set; } = 60;
}

public sealed class ExternalTaskWorkerOptionsValidator : IValidateOptions<ExternalTaskWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, ExternalTaskWorkerOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        if (!SafeEndpoint(options.BaseAddress) || !SafeEndpoint(options.TokenEndpoint)
            || string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret)
            || options.Topics.Length is < 1 or > 16 || options.Topics.Any(string.IsNullOrWhiteSpace)
            || options.Topics.Distinct(StringComparer.Ordinal).Count() != options.Topics.Length
            || options.MaxConcurrency is < 1 or > 32 || options.MaxTasksPerClaim is < 1 or > 10
            || options.MaxTasksPerClaim > options.MaxConcurrency || options.LeaseSeconds is < 10 or > 120
            || options.HeartbeatSeconds < 1 || options.HeartbeatSeconds * 2 >= options.LeaseSeconds
            || options.PollMilliseconds is < 100 or > 60_000 || options.MaximumBackoffSeconds is < 1 or > 60)
            return ValidateOptionsResult.Fail("ExternalTaskWorker configuration is invalid or unsafe.");
        return ValidateOptionsResult.Success;
    }

    private static bool SafeEndpoint(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
}
