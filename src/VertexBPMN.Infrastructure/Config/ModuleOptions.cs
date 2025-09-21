namespace VertexBPMN.Infrastructure.Config;

/// <summary>
/// Toggle feature modules without recompiling. Bind from configuration section "Modules".
/// </summary>
public sealed class ModuleOptions
{
    public bool Engine { get; set; } = true;
    public bool Persistence { get; set; } = true;
    public bool Grpc { get; set; } = true;
    public bool SignalR { get; set; } = true;
    public bool Telemetry { get; set; } = true;
    public bool Plugins { get; set; } = true;
    public bool BackgroundJobs { get; set; } = true;
    public bool Swagger { get; set; } = true;
    public bool Emails { get; set; }
}