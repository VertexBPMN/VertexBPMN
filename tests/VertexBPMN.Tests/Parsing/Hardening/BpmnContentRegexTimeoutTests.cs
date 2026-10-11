using System.Text.RegularExpressions;
using VertexBPMN.Domain.Model.Security;
using VertexBPMN.Engine.Security;

namespace VertexBPMN.Tests.Parsing.Hardening;

public sealed class BpmnContentRegexTimeoutTests
{
	[Fact]
	public void SanitizationRejectsCatastrophicBacktrackingInsteadOfReturningUncheckedContent()
	{
		var validation = new ContentValidationResult { IsSecure = false };
		validation.Threats.Add(new SecurityThreat
		{
			Type = ThreatType.MaliciousContent,
			Pattern = "(a+)+$"
		});
		var input = new string('a', 20_000) + "!";

		Assert.Throws<RegexMatchTimeoutException>(
			() => new BpmnContentValidator().SanitizeContent(input, validation));
	}
}
