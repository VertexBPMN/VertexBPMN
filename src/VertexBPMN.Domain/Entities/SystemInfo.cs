namespace VertexBPMN.Domain.Entities;

public class SystemInfo
{
    public string ApplicationVersion { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime { get; set; }
}