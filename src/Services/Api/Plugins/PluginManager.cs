using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace VertexBPMN.Api.Plugins;

/// <summary>
/// Plugin Architecture System
/// Olympic-level feature: Innovation Differentiators - Plugin Extensions
/// </summary>
public interface IPluginManager
{
    Task<PluginLoadResult> LoadPluginAsync(string pluginPath);
    Task<bool> UnloadPluginAsync(string pluginId);
    Task<List<PluginInfo>> GetLoadedPluginsAsync();
    Task<PluginInfo?> GetPluginInfoAsync(string pluginId);
    Task<bool> EnablePluginAsync(string pluginId);
    Task<bool> DisablePluginAsync(string pluginId);
    Task<T?> GetPluginServiceAsync<T>(string pluginId) where T : class;
    Task<PluginExecutionResult> ExecutePluginMethodAsync(string pluginId, string methodName, params object[] parameters);
    Task<List<PluginExtensionPoint>> GetAvailableExtensionPointsAsync();
    Task<bool> RegisterExtensionPointAsync(PluginExtensionPoint extensionPoint);
}

public class PluginManager : IPluginManager, IDisposable
{
    private readonly ILogger<PluginManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, LoadedPlugin> _loadedPlugins = new();
    private readonly ConcurrentDictionary<string, PluginExtensionPoint> _extensionPoints = new();
    private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _assemblyContexts = new();
    private readonly PluginSecurityManager _securityManager;

    public PluginManager(
        ILogger<PluginManager> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _securityManager = new PluginSecurityManager(logger);
        
        // Register default extension points
        InitializeDefaultExtensionPoints();
    }

    public async Task<PluginLoadResult> LoadPluginAsync(string pluginPath)
    {
        try
        {
            _logger.LogInformation("Loading plugin from path: {PluginPath}", pluginPath);

            // Validate plugin file
            if (!File.Exists(pluginPath))
            {
                return new PluginLoadResult
                {
                    Success = false,
                    Error = $"Plugin file not found: {pluginPath}"
                };
            }

            // Security validation
            var securityResult = await _securityManager.ValidatePluginAsync(pluginPath);
            if (!securityResult.IsValid)
            {
                return new PluginLoadResult
                {
                    Success = false,
                    Error = $"Security validation failed: {securityResult.Reason}"
                };
            }

            // Load plugin metadata
            var metadata = await LoadPluginMetadataAsync(pluginPath);
            if (metadata == null)
            {
                return new PluginLoadResult
                {
                    Success = false,
                    Error = "Failed to load plugin metadata"
                };
            }

            // Check if plugin is already loaded
            if (_loadedPlugins.ContainsKey(metadata.Id))
            {
                return new PluginLoadResult
                {
                    Success = false,
                    Error = $"Plugin {metadata.Id} is already loaded"
                };
            }

            // Validate dependencies
            var dependencyResult = await ValidatePluginDependenciesAsync(metadata);
            if (!dependencyResult.IsValid)
            {
                return new PluginLoadResult
                {
                    Success = false,
                    Error = $"Dependency validation failed: {dependencyResult.Reason}"
                };
            }

            // Create assembly load context
            var assemblyContext = new PluginAssemblyLoadContext(metadata.Id);
            var assembly = assemblyContext.LoadFromAssemblyPath(pluginPath);

            // Load plugin instance
            var pluginInstance = await CreatePluginInstanceAsync(assembly, metadata);
            if (pluginInstance == null)
            {
                return new PluginLoadResult
                {
                    Success = false,
                    Error = "Failed to create plugin instance"
                };
            }

            // Register plugin services
            var serviceContainer = new PluginServiceContainer();
            await pluginInstance.RegisterServicesAsync(serviceContainer);

            // Create loaded plugin
            var loadedPlugin = new LoadedPlugin
            {
                Id = metadata.Id,
                Metadata = metadata,
                Instance = pluginInstance,
                Assembly = assembly,
                AssemblyContext = assemblyContext,
                ServiceContainer = serviceContainer,
                LoadedAt = DateTime.UtcNow,
                IsEnabled = true,
                Status = PluginStatus.Loaded
            };

            _loadedPlugins[metadata.Id] = loadedPlugin;
            _assemblyContexts[metadata.Id] = assemblyContext;

            // Initialize plugin
            await pluginInstance.InitializeAsync(CreatePluginContext(loadedPlugin));

            // Register extension points
            await RegisterPluginExtensionPointsAsync(loadedPlugin);

            _logger.LogInformation("Plugin {PluginId} loaded successfully", metadata.Id);

            return new PluginLoadResult
            {
                Success = true,
                PluginId = metadata.Id,
                PluginInfo = CreatePluginInfo(loadedPlugin)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin from path: {PluginPath}", pluginPath);
            return new PluginLoadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        try
        {
            if (!_loadedPlugins.TryRemove(pluginId, out var loadedPlugin))
            {
                _logger.LogWarning("Plugin {PluginId} not found for unloading", pluginId);
                return false;
            }

            _logger.LogInformation("Unloading plugin {PluginId}", pluginId);

            // Shutdown plugin
            await loadedPlugin.Instance.ShutdownAsync();

            // Unregister extension points
            await UnregisterPluginExtensionPointsAsync(loadedPlugin);

            // Dispose resources
            if (loadedPlugin.Instance is IDisposable disposableInstance)
            {
                disposableInstance.Dispose();
            }

            // Unload assembly context
            if (_assemblyContexts.TryRemove(pluginId, out var assemblyContext))
            {
                assemblyContext.Unload();
            }

            _logger.LogInformation("Plugin {PluginId} unloaded successfully", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading plugin {PluginId}", pluginId);
            return false;
        }
    }

    public async Task<List<PluginInfo>> GetLoadedPluginsAsync()
    {
        return await Task.FromResult(_loadedPlugins.Values.Select(CreatePluginInfo).ToList());
    }

    public async Task<PluginInfo?> GetPluginInfoAsync(string pluginId)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            return await Task.FromResult(CreatePluginInfo(loadedPlugin));
        }
        return null;
    }

    public async Task<bool> EnablePluginAsync(string pluginId)
    {
        try
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
            {
                if (!loadedPlugin.IsEnabled)
                {
                    loadedPlugin.IsEnabled = true;
                    loadedPlugin.Status = PluginStatus.Enabled;
                    await loadedPlugin.Instance.EnableAsync();
                    _logger.LogInformation("Plugin {PluginId} enabled", pluginId);
                }
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling plugin {PluginId}", pluginId);
            return false;
        }
    }

    public async Task<bool> DisablePluginAsync(string pluginId)
    {
        try
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
            {
                if (loadedPlugin.IsEnabled)
                {
                    loadedPlugin.IsEnabled = false;
                    loadedPlugin.Status = PluginStatus.Disabled;
                    await loadedPlugin.Instance.DisableAsync();
                    _logger.LogInformation("Plugin {PluginId} disabled", pluginId);
                }
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling plugin {PluginId}", pluginId);
            return false;
        }
    }

    public async Task<T?> GetPluginServiceAsync<T>(string pluginId) where T : class
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin) && loadedPlugin.IsEnabled)
        {
            return await Task.FromResult(loadedPlugin.ServiceContainer.GetService<T>());
        }
        return null;
    }

    public async Task<PluginExecutionResult> ExecutePluginMethodAsync(string pluginId, string methodName, params object[] parameters)
    {
        try
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
            {
                return new PluginExecutionResult
                {
                    Success = false,
                    Error = $"Plugin {pluginId} not found"
                };
            }

            if (!loadedPlugin.IsEnabled)
            {
                return new PluginExecutionResult
                {
                    Success = false,
                    Error = $"Plugin {pluginId} is disabled"
                };
            }

            // Security check
            if (!await _securityManager.CanExecuteMethodAsync(pluginId, methodName))
            {
                return new PluginExecutionResult
                {
                    Success = false,
                    Error = $"Security policy prevents execution of method {methodName}"
                };
            }

            var result = await loadedPlugin.Instance.ExecuteMethodAsync(methodName, parameters);
            
            return new PluginExecutionResult
            {
                Success = true,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing method {MethodName} in plugin {PluginId}", methodName, pluginId);
            return new PluginExecutionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<List<PluginExtensionPoint>> GetAvailableExtensionPointsAsync()
    {
        return await Task.FromResult(_extensionPoints.Values.ToList());
    }

    public async Task<bool> RegisterExtensionPointAsync(PluginExtensionPoint extensionPoint)
    {
        try
        {
            _extensionPoints[extensionPoint.Id] = extensionPoint;
            _logger.LogDebug("Extension point {ExtensionPointId} registered", extensionPoint.Id);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering extension point {ExtensionPointId}", extensionPoint.Id);
            return false;
        }
    }

    // Helper methods
    private void InitializeDefaultExtensionPoints()
    {
        var defaultExtensionPoints = new[]
        {
            new PluginExtensionPoint
            {
                Id = "process-execution-listener",
                Name = "Process Execution Listener",
                Description = "Listen to process execution events",
                InterfaceType = typeof(IProcessExecutionListener).FullName!,
                Parameters = new Dictionary<string, PluginParameter>
                {
                    { "eventType", new PluginParameter { Name = "eventType", Type = "string", Required = true } }
                }
            },
            new PluginExtensionPoint
            {
                Id = "activity-behavior",
                Name = "Custom Activity Behavior",
                Description = "Implement custom activity behaviors",
                InterfaceType = typeof(ICustomActivityBehavior).FullName!,
                Parameters = new Dictionary<string, PluginParameter>
                {
                    { "activityType", new PluginParameter { Name = "activityType", Type = "string", Required = true } }
                }
            },
            new PluginExtensionPoint
            {
                Id = "variable-resolver",
                Name = "Variable Resolver",
                Description = "Resolve custom variable expressions",
                InterfaceType = typeof(IVariableResolver).FullName!,
                Parameters = new Dictionary<string, PluginParameter>()
            },
            new PluginExtensionPoint
            {
                Id = "connector",
                Name = "External System Connector",
                Description = "Connect to external systems",
                InterfaceType = typeof(IExternalConnector).FullName!,
                Parameters = new Dictionary<string, PluginParameter>
                {
                    { "systemType", new PluginParameter { Name = "systemType", Type = "string", Required = true } }
                }
            }
        };

        foreach (var extensionPoint in defaultExtensionPoints)
        {
            _extensionPoints[extensionPoint.Id] = extensionPoint;
        }
    }

    private async Task<PluginMetadata?> LoadPluginMetadataAsync(string pluginPath)
    {
        try
        {
            var pluginDirectory = Path.GetDirectoryName(pluginPath);
            var metadataPath = Path.Combine(pluginDirectory!, "plugin.json");
            
            if (!File.Exists(metadataPath))
            {
                _logger.LogWarning("Plugin metadata file not found: {MetadataPath}", metadataPath);
                return null;
            }

            var metadataJson = await File.ReadAllTextAsync(metadataPath);
            return JsonSerializer.Deserialize<PluginMetadata>(metadataJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin metadata from {PluginPath}", pluginPath);
            return null;
        }
    }

    private async Task<ValidationResult> ValidatePluginDependenciesAsync(PluginMetadata metadata)
    {
        try
        {
            foreach (var dependency in metadata.Dependencies)
            {
                if (dependency.Required)
                {
                    // Check if dependency is available
                    var isDependencyAvailable = await CheckDependencyAvailabilityAsync(dependency);
                    if (!isDependencyAvailable)
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            Reason = $"Required dependency not available: {dependency.Name} {dependency.Version}"
                        };
                    }
                }
            }

            return new ValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating plugin dependencies");
            return new ValidationResult { IsValid = false, Reason = ex.Message };
        }
    }

    private async Task<bool> CheckDependencyAvailabilityAsync(PluginDependency dependency)
    {
        // Simulate dependency checking
        await Task.Delay(10);
        
        // In real implementation, this would check:
        // - Required assemblies
        // - System components
        // - Other plugins
        // - External services
        
        return dependency.Name switch
        {
            "VertexBPMN.Core" => true,
            "Microsoft.Extensions.DependencyInjection" => true,
            "System.Text.Json" => true,
            _ => false
        };
    }

    private async Task<IPlugin?> CreatePluginInstanceAsync(Assembly assembly, PluginMetadata metadata)
    {
        try
        {
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (pluginType == null)
            {
                _logger.LogError("No plugin implementation found in assembly");
                return null;
            }

            var instance = Activator.CreateInstance(pluginType) as IPlugin;
            return await Task.FromResult(instance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating plugin instance");
            return null;
        }
    }

    private PluginContext CreatePluginContext(LoadedPlugin loadedPlugin)
    {
        return new PluginContext
        {
            PluginId = loadedPlugin.Id,
            ServiceProvider = _serviceProvider,
            Configuration = _configuration,
            Logger = _logger,
            ExtensionPoints = _extensionPoints.Values.ToList()
        };
    }

    private async Task RegisterPluginExtensionPointsAsync(LoadedPlugin loadedPlugin)
    {
        try
        {
            var pluginExtensionPoints = await loadedPlugin.Instance.GetExtensionPointsAsync();
            foreach (var extensionPoint in pluginExtensionPoints)
            {
                extensionPoint.ProviderId = loadedPlugin.Id;
                _extensionPoints[extensionPoint.Id] = extensionPoint;
                _logger.LogDebug("Registered extension point {ExtensionPointId} from plugin {PluginId}", 
                    extensionPoint.Id, loadedPlugin.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering extension points for plugin {PluginId}", loadedPlugin.Id);
        }
    }

    private async Task UnregisterPluginExtensionPointsAsync(LoadedPlugin loadedPlugin)
    {
        try
        {
            var pluginExtensionPoints = _extensionPoints.Values
                .Where(ep => ep.ProviderId == loadedPlugin.Id)
                .ToList();

            foreach (var extensionPoint in pluginExtensionPoints)
            {
                _extensionPoints.TryRemove(extensionPoint.Id, out _);
                _logger.LogDebug("Unregistered extension point {ExtensionPointId} from plugin {PluginId}", 
                    extensionPoint.Id, loadedPlugin.Id);
            }
            
            await Task.CompletedTask; // Satisfy async requirement
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering extension points for plugin {PluginId}", loadedPlugin.Id);
        }
    }

    private PluginInfo CreatePluginInfo(LoadedPlugin loadedPlugin)
    {
        return new PluginInfo
        {
            Id = loadedPlugin.Id,
            Name = loadedPlugin.Metadata.Name,
            Version = loadedPlugin.Metadata.Version,
            Description = loadedPlugin.Metadata.Description,
            Author = loadedPlugin.Metadata.Author,
            IsEnabled = loadedPlugin.IsEnabled,
            Status = loadedPlugin.Status,
            LoadedAt = loadedPlugin.LoadedAt,
            Dependencies = loadedPlugin.Metadata.Dependencies,
            ExtensionPoints = _extensionPoints.Values.Where(ep => ep.ProviderId == loadedPlugin.Id).ToList()
        };
    }

    public void Dispose()
    {
        // Unload all plugins
        var pluginIds = _loadedPlugins.Keys.ToList();
        foreach (var pluginId in pluginIds)
        {
            try
            {
                UnloadPluginAsync(pluginId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during plugin cleanup: {PluginId}", pluginId);
            }
        }

        // Dispose assembly contexts
        foreach (var assemblyContext in _assemblyContexts.Values)
        {
            try
            {
                assemblyContext.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing assembly context");
            }
        }
    }
}

// Plugin Security Manager
public class PluginSecurityManager
{
    private readonly ILogger _logger;
    private readonly HashSet<string> _allowedMethods = new()
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

            // Simulate security scanning
            await Task.Delay(100);

            // In real implementation, this would:
            // - Scan for malicious code patterns
            // - Verify digital signatures
            // - Check against known threat databases
            // - Validate assembly integrity

            return new ValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin security validation");
            return new ValidationResult { IsValid = false, Reason = ex.Message };
        }
    }

    public async Task<bool> CanExecuteMethodAsync(string pluginId, string methodName)
    {
        // Check if method is in allowed list
        return await Task.FromResult(_allowedMethods.Contains(methodName));
    }
}

// Plugin Assembly Load Context
public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _pluginId;

    public PluginAssemblyLoadContext(string pluginId) : base($"Plugin_{pluginId}", true)
    {
        _pluginId = pluginId;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Implement custom assembly resolution logic
        return null;
    }
}

// Plugin Service Container
public class PluginServiceContainer
{
    private readonly Dictionary<Type, object> _services = new();

    public void RegisterService<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    public T? GetService<T>() where T : class
    {
        return _services.GetValueOrDefault(typeof(T)) as T;
    }

    public bool HasService<T>()
    {
        return _services.ContainsKey(typeof(T));
    }
}

// Plugin Interfaces
public interface IPlugin
{
    Task InitializeAsync(PluginContext context);
    Task ShutdownAsync();
    Task EnableAsync();
    Task DisableAsync();
    Task RegisterServicesAsync(PluginServiceContainer serviceContainer);
    Task<object?> ExecuteMethodAsync(string methodName, params object[] parameters);
    Task<List<PluginExtensionPoint>> GetExtensionPointsAsync();
}

public interface IProcessExecutionListener
{
    Task OnProcessStartedAsync(Guid processInstanceId, Dictionary<string, object> variables);
    Task OnProcessCompletedAsync(Guid processInstanceId, Dictionary<string, object> variables);
    Task OnActivityStartedAsync(Guid processInstanceId, string activityId, Dictionary<string, object> variables);
    Task OnActivityCompletedAsync(Guid processInstanceId, string activityId, Dictionary<string, object> variables);
}

public interface ICustomActivityBehavior
{
    Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context);
    Task<bool> CanExecuteAsync(ActivityExecutionContext context);
}

public interface IVariableResolver
{
    Task<object?> ResolveVariableAsync(string variableName, Dictionary<string, object> context);
    Task<bool> CanResolveAsync(string variableName);
}

public interface IExternalConnector
{
    Task<ConnectorResult> ConnectAsync(Dictionary<string, object> connectionParameters);
    Task<ConnectorResult> ExecuteAsync(string operation, Dictionary<string, object> parameters);
    Task DisconnectAsync();
}

// Data Models
public class PluginLoadResult
{
    public bool Success { get; set; }
    public string? PluginId { get; set; }
    public string? Error { get; set; }
    public PluginInfo? PluginInfo { get; set; }
}

public class PluginExecutionResult
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}

public class LoadedPlugin
{
    public string Id { get; set; } = string.Empty;
    public PluginMetadata Metadata { get; set; } = new();
    public IPlugin Instance { get; set; } = null!;
    public Assembly Assembly { get; set; } = null!;
    public PluginAssemblyLoadContext AssemblyContext { get; set; } = null!;
    public PluginServiceContainer ServiceContainer { get; set; } = new();
    public DateTime LoadedAt { get; set; }
    public bool IsEnabled { get; set; }
    public PluginStatus Status { get; set; }
}

public class PluginMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<PluginDependency> Dependencies { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}

public class PluginDependency
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool Required { get; set; }
}

public class PluginInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public PluginStatus Status { get; set; }
    public DateTime LoadedAt { get; set; }
    public List<PluginDependency> Dependencies { get; set; } = new();
    public List<PluginExtensionPoint> ExtensionPoints { get; set; } = new();
}

public enum PluginStatus
{
    Loading,
    Loaded,
    Enabled,
    Disabled,
    Error,
    Unloading
}

public class PluginExtensionPoint
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = string.Empty;
    public string? ProviderId { get; set; }
    public Dictionary<string, PluginParameter> Parameters { get; set; } = new();
}

public class PluginParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public string? Description { get; set; }
}

public class PluginContext
{
    public string PluginId { get; set; } = string.Empty;
    public IServiceProvider ServiceProvider { get; set; } = null!;
    public IConfiguration Configuration { get; set; } = null!;
    public ILogger Logger { get; set; } = null!;
    public List<PluginExtensionPoint> ExtensionPoints { get; set; } = new();
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ActivityExecutionContext
{
    public Guid ProcessInstanceId { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}

public class ActivityExecutionResult
{
    public bool Success { get; set; }
    public Dictionary<string, object> OutputVariables { get; set; } = new();
    public string? Error { get; set; }
}

public class ConnectorResult
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
}
