namespace VertexBPMN.Api.Plugins;

internal class PluginSecurityManager
{
    private readonly ILogger _logger;
    private readonly HashSet<string> _allowedMethods = new(StringComparer.Ordinal)
    {
        "ExecuteAsync", "ProcessAsync", "ValidateAsync", "InitializeAsync", "ShutdownAsync"
    };

    public PluginSecurityManager(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<ValidationResult> ValidatePluginAsync(string pluginPath)
    {
        try
        {
            // Basic file validation
            var fileInfo = new FileInfo(pluginPath);
            if (fileInfo.Length > 50 * 1024 * 1024) // 50MB limit
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Reason = "Plugin file size exceeds maximum allowed size (50MB)"
                };
            }

            await Task.CompletedTask;
            return new ValidationResult
            {
                IsValid = false,
                Reason = "No signature, integrity, or malware scanner is configured; plugin activation is blocked."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin security validation");
            return new ValidationResult { IsValid = false, Reason = ex.Message };
        }
    }

    public Task<bool> CanExecuteMethodAsync(string methodName)
    {
        // Check if method is in allowed list
        return Task.FromResult(_allowedMethods.Contains(methodName));
    }
}
