using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Plugins;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/plugins")]
[Authorize]
public sealed class PluginController(
    IPluginManager pluginManager,
    ILogger<PluginController> logger,
    IConfiguration configuration) : ControllerBase
{
    private readonly string _pluginDirectory = ResolvePluginDirectory(configuration);

    [HttpPost("load")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PluginLoadResult>> LoadPlugin([FromBody] LoadPluginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PluginPath)
                || !string.Equals(Path.GetExtension(request.PluginPath), ".dll", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Only plugin DLLs are allowed." });

            var pluginPath = Path.GetFullPath(request.PluginPath);
            var pluginRoot = _pluginDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!pluginPath.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Plugin path must be inside the configured plugin directory." });

            var result = await pluginManager.LoadPluginAsync(pluginPath);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error loading plugin.");
            return StatusCode(500, new { error = "Failed to load plugin" });
        }
    }

    [HttpPost("unload/{pluginId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> UnloadPlugin(string pluginId)
    {
        try
        {
            var success = await pluginManager.UnloadPluginAsync(pluginId);
            return success ? Ok(new { message = "Plugin unloaded successfully" }) : NotFound(new { error = "Plugin not found" });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error unloading plugin {PluginId}", pluginId);
            return StatusCode(500, new { error = "Failed to unload plugin" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<PluginInfo>>> GetLoadedPlugins() =>
        Ok(await pluginManager.GetLoadedPluginsAsync());

    [HttpGet("{pluginId}")]
    public async Task<ActionResult<PluginInfo>> GetPluginInfo(string pluginId)
    {
        var pluginInfo = await pluginManager.GetPluginInfoAsync(pluginId);
        return pluginInfo is null ? NotFound(new { error = "Plugin not found" }) : Ok(pluginInfo);
    }

    [HttpPost("enable/{pluginId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> EnablePlugin(string pluginId)
    {
        try
        {
            var success = await pluginManager.EnablePluginAsync(pluginId);
            return success ? Ok(new { message = "Plugin enabled successfully" }) : NotFound(new { error = "Plugin not found" });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error enabling plugin {PluginId}", pluginId);
            return StatusCode(500, new { error = "Failed to enable plugin" });
        }
    }

    [HttpPost("disable/{pluginId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> DisablePlugin(string pluginId)
    {
        try
        {
            var success = await pluginManager.DisablePluginAsync(pluginId);
            return success ? Ok(new { message = "Plugin disabled successfully" }) : NotFound(new { error = "Plugin not found" });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error disabling plugin {PluginId}", pluginId);
            return StatusCode(500, new { error = "Failed to disable plugin" });
        }
    }

    [HttpPost("execute/{pluginId}/{methodName}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<PluginExecutionResult>> ExecutePluginMethod(
        string pluginId,
        string methodName,
        [FromBody] ExecuteMethodRequest request)
    {
        try
        {
            var result = await pluginManager.ExecutePluginMethodAsync(pluginId, methodName, request.Parameters);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error executing plugin method {MethodName} in plugin {PluginId}", methodName, pluginId);
            return StatusCode(500, new { error = "Failed to execute plugin method" });
        }
    }

    [HttpGet("extension-points")]
    public async Task<ActionResult<List<PluginExtensionPoint>>> GetAvailableExtensionPoints() =>
        Ok(await pluginManager.GetAvailableExtensionPointsAsync());

    [HttpPost("extension-points")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> RegisterExtensionPoint([FromBody] PluginExtensionPoint extensionPoint)
    {
        try
        {
            var success = await pluginManager.RegisterExtensionPointAsync(extensionPoint);
            return success ? Ok(new { message = "Extension point registered successfully" }) : BadRequest(new { error = "Failed to register extension point" });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error registering extension point {ExtensionPointId}", extensionPoint.Id);
            return StatusCode(500, new { error = "Failed to register extension point" });
        }
    }

    [HttpGet("{pluginId}/service/{serviceType}")]
    public ActionResult GetPluginService(string pluginId, string serviceType) =>
        Ok(new { message = $"Service {serviceType} from plugin {pluginId}", available = true, pluginId, serviceType });

    private static string ResolvePluginDirectory(IConfiguration configuration)
    {
        var configuredDirectory = configuration["Dependencies:Plugins:Directory"] ?? "plugins";
        return Path.GetFullPath(Path.IsPathRooted(configuredDirectory)
            ? configuredDirectory
            : Path.Combine(AppContext.BaseDirectory, configuredDirectory));
    }
}

public sealed class LoadPluginRequest
{
    public string PluginPath { get; set; } = string.Empty;
}

public sealed class ExecuteMethodRequest
{
    public object[] Parameters { get; set; } = Array.Empty<object>();
}
