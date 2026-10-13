namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Declared authentication method; availability requires a qualified adapter.</summary>
public enum GitAuthenticationKind
{
	Anonymous,
	HttpBasic,
	GitHubApp,
	HttpBearer,
	SshKey
}
