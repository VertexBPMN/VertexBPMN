using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Plugins;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Plugin Management Controller
/// Olympic-level feature: Innovation Differentiators - Plugin Architecture
/// </summary>
[ApiController]
[Route("api/plugins")]
public class PluginController : ControllerBase
{
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<PluginController> _logger;

    public PluginController(
        IPluginManager pluginManager,
        ILogger<PluginController> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;
    }

    /// <summary>
    /// Load a plugin from file path
    /// </summary>
    [HttpPost("load")]
    public async Task<ActionResult<PluginLoadResult>> LoadPlugin([FromBody] LoadPluginRequest request)
    {
        try
        {
            var result = await _pluginManager.LoadPluginAsync(request.PluginPath);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin from path {PluginPath}", request.PluginPath);
            return StatusCode(500, new { error = "Failed to load plugin" });
        }
    }

    /// <summary>
    /// Unload a plugin
    /// </summary>
    [HttpPost("unload/{pluginId}")]
    public async Task<ActionResult> UnloadPlugin(string pluginId)
    {
        try
        {
            var success = await _pluginManager.UnloadPluginAsync(pluginId);
            if (success)
            {
                return Ok(new { message = $"Plugin {pluginId} unloaded successfully" });
            }
            return NotFound(new { error = "Plugin not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading plugin {PluginId}", pluginId);
            return StatusCode(500, new { error = "Failed to unload plugin" });
        }
    }

    /// <summary>
    /// Get list of all loaded plugins
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PluginInfo>>> GetLoadedPlugins()
    {
        try
        {
            var plugins = await _pluginManager.GetLoadedPluginsAsync();
            return Ok(plugins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loaded plugins");
            return StatusCode(500, new { error = "Failed to get loaded plugins" });
        }
    }

    /// <summary>
    /// Get information about a specific plugin
    /// </summary>
    [HttpGet("{pluginId}")]
    public async Task<ActionResult<PluginInfo>> GetPluginInfo(string pluginId)
    {
        try
        {
            var pluginInfo = await _pluginManager.GetPluginInfoAsync(pluginId);
            if (pluginInfo == null)
            {
                return NotFound(new { error = "Plugin not found" });
            }
            return Ok(pluginInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plugin info for {PluginId}", pluginId);
            return StatusCode(500, new { error = "Failed to get plugin information" });
        }
    }

    /// <summary>
    /// Enable a plugin
    /// </summary>
    [HttpPost("enable/{pluginId}")]
    public async Task<ActionResult> EnablePlugin(string pluginId)
    {
        try
        {
            var success = await _pluginManager.EnablePluginAsync(pluginId);
            if (success)
            {
                return Ok(new { message = $"Plugin {pluginId} enabled successfully" });
            }
            return NotFound(new { error = "Plugin not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling plugin {PluginId}", pluginId);
            return StatusCode(500, new { error = "Failed to enable plugin" });
        }
    }

    /// <summary>
    /// Disable a plugin
    /// </summary>
    [HttpPost("disable/{pluginId}")]
    public async Task<ActionResult> DisablePlugin(string pluginId)
    {
        try
        {
            var success = await _pluginManager.DisablePluginAsync(pluginId);
            if (success)
            {
                return Ok(new { message = $"Plugin {pluginId} disabled successfully" });
            }
            return NotFound(new { error = "Plugin not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling plugin {PluginId}", pluginId);
            return StatusCode(500, new { error = "Failed to disable plugin" });
        }
    }

    /// <summary>
    /// Execute a method in a plugin
    /// </summary>
    [HttpPost("execute/{pluginId}/{methodName}")]
    public async Task<ActionResult<PluginExecutionResult>> ExecutePluginMethod(
        string pluginId, 
        string methodName, 
        [FromBody] ExecuteMethodRequest request)
    {
        try
        {
            var result = await _pluginManager.ExecutePluginMethodAsync(pluginId, methodName, request.Parameters);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing method {MethodName} in plugin {PluginId}", methodName, pluginId);
            return StatusCode(500, new { error = "Failed to execute plugin method" });
        }
    }

    /// <summary>
    /// Get available extension points
    /// </summary>
    [HttpGet("extension-points")]
    public async Task<ActionResult<List<PluginExtensionPoint>>> GetAvailableExtensionPoints()
    {
        try
        {
            var extensionPoints = await _pluginManager.GetAvailableExtensionPointsAsync();
            return Ok(extensionPoints);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available extension points");
            return StatusCode(500, new { error = "Failed to get extension points" });
        }
    }

    /// <summary>
    /// Register a new extension point
    /// </summary>
    [HttpPost("extension-points")]
    public async Task<ActionResult> RegisterExtensionPoint([FromBody] PluginExtensionPoint extensionPoint)
    {
        try
        {
            var success = await _pluginManager.RegisterExtensionPointAsync(extensionPoint);
            if (success)
            {
                return Ok(new { message = $"Extension point {extensionPoint.Id} registered successfully" });
            }
            return BadRequest(new { error = "Failed to register extension point" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering extension point {ExtensionPointId}", extensionPoint.Id);
            return StatusCode(500, new { error = "Failed to register extension point" });
        }
    }

    /// <summary>
    /// Get a service from a specific plugin
    /// </summary>
    [HttpGet("{pluginId}/service/{serviceType}")]
    public async Task<ActionResult> GetPluginService(string pluginId, string serviceType)
    {
        try
        {
            // This would require runtime type resolution in a real implementation
            // For now, we'll return a generic response
            return Ok(new { 
                message = $"Service {serviceType} from plugin {pluginId}",
                available = true,
                pluginId,
                serviceType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service {ServiceType} from plugin {PluginId}", serviceType, pluginId);
            return StatusCode(500, new { error = "Failed to get plugin service" });
        }
    }
}

public class LoadPluginRequest
{
    public string PluginPath { get; set; } = string.Empty;
}

public class ExecuteMethodRequest
{
    public object[] Parameters { get; set; } = Array.Empty<object>();
}
