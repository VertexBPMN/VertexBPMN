namespace VertexBPMN.Infrastructure.Config;

/// <summary>
/// Explicit operational mode used to refine behavior beyond ASP.NET Core's environment name.
/// If not set, falls back to the host environment (Development, Staging, Production, Test).
/// </summary>
public enum OperationalMode
{
    Production,
    Stage,
    Development,
    Test
}