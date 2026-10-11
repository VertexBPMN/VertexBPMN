using Microsoft.Extensions.Options;

namespace VertexBPMN.AgentWorker;

internal sealed class ContractReviewerOptionsValidator : IValidateOptions<ContractReviewerOptions>
{
	public ValidateOptionsResult Validate(string? name, ContractReviewerOptions options)
	{
		if (!options.Enabled)
		{
			return ValidateOptionsResult.Success;
		}

		if (!string.Equals(options.Profile, "contract-reviewer.v1", StringComparison.Ordinal) || !string.Equals(options.DataClassification, "local-sensitive", StringComparison.Ordinal) || !string.Equals(options.Adapter, "ollama", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(options.Model) || options.Model.Length > 128 || !IsExactLoopbackEndpoint(options.Endpoint) || options.MaximumDocumentBytes is < 1_024 or > 131_072 || options.MaximumSectionCharacters is < 256 or > 8_192 || options.MaximumSections is < 1 or > 128 || options.MaximumSearchHits is < 1 or > 20 || options.MaximumToolCalls is < 1 or > 16 || options.MaximumRuntimeCalls < 2 || options.MaximumRuntimeCalls > options.MaximumToolCalls + 2 || options.MaximumOutputTokensPerCall is < 64 or > 2_048 || options.TotalTokenBudget < options.MaximumOutputTokensPerCall || options.TotalTokenBudget > 32_768 || options.MaximumResponseBytes is < 1_024 or > 65_536 || options.MaximumRuntimeSeconds is < 5 or > 300)
		{
			return ValidateOptionsResult.Fail("ContractReviewer configuration is invalid or unsafe.");
		}

		return ValidateOptionsResult.Success;
	}

	internal static bool IsExactLoopbackEndpoint(string value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || !uri.IsLoopback || uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
		{
			return false;
		}

		return uri.AbsolutePath == "/";
	}
}
