namespace VertexBPMN.Studio.Options;

public class ApiOptions
{
    public const string SectionName = "Api";
    
    public string BaseUrl { get; set; } = "http://localhost:5074/";
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableRetry { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
}