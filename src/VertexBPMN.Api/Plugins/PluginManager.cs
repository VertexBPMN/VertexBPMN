using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace VertexBPMN.Api.Plugins;
internal sealed class PluginManager : IPluginManager, IDisposable
{
	private readonly ILogger<PluginManager> _logger;
	private readonly IServiceProvider _serviceProvider;
	private readonly IConfiguration _configuration;
	private readonly ConcurrentDictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, PluginExtensionPoint> _extensionPoints = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _assemblyContexts = new(StringComparer.Ordinal);
	private readonly PluginSecurityManager _securityManager;
	public PluginManager(ILogger<PluginManager> logger, IServiceProvider serviceProvider, IConfiguration configuration)
	{
		_logger = logger;
		_serviceProvider = serviceProvider;
		_configuration = configuration;
		_securityManager = new PluginSecurityManager(logger);
		// Register default extension points
		InitializeDefaultExtensionPoints();
	}

	public int LoadedPluginCount => _loadedPlugins.Count;

	public async Task<PluginLoadResult> LoadPluginAsync(string pluginPath)
	{
		try
		{
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Loading plugin from path: {PluginPath}", pluginPath);
			}
			var (metadata, validationError) = await ValidatePluginForLoadingAsync(pluginPath);
			if (metadata is null)
			{
				return new PluginLoadResult { Success = false, Error = validationError };
			}

			// Create assembly load context
			var assemblyContext = new PluginAssemblyLoadContext(metadata.Id);
			var assembly = assemblyContext.LoadFromAssemblyPath(pluginPath);
			// Load plugin instance
			var pluginInstance = await CreatePluginInstanceAsync(assembly);
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
			var loadedPlugin = CreateLoadedPlugin(metadata, pluginInstance, assembly, assemblyContext, serviceContainer);
			_loadedPlugins[metadata.Id] = loadedPlugin;
			_assemblyContexts[metadata.Id] = assemblyContext;
			// Initialize plugin
			await pluginInstance.InitializeAsync(CreatePluginContext(loadedPlugin));
			// Register extension points
			await RegisterPluginExtensionPointsAsync(loadedPlugin);
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Plugin {PluginId} loaded successfully", metadata.Id);
			}
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

	private static LoadedPlugin CreateLoadedPlugin(PluginMetadata metadata, IPlugin instance, Assembly assembly, PluginAssemblyLoadContext context, PluginServiceContainer services) => new()
	{
		Id = metadata.Id,
		Metadata = metadata,
		Instance = instance,
		Assembly = assembly,
		AssemblyContext = context,
		ServiceContainer = services,
		LoadedAt = DateTime.UtcNow,
		IsEnabled = true,
		Status = PluginStatus.Loaded
	};

	private async Task<(PluginMetadata? Metadata, string? Error)> ValidatePluginForLoadingAsync(string pluginPath)
	{
		if (!File.Exists(pluginPath))
		{
			return (null, $"Plugin file not found: {pluginPath}");
		}

		var securityResult = await _securityManager.ValidatePluginAsync(pluginPath);
		if (!securityResult.IsValid)
		{
			return (null, $"Security validation failed: {securityResult.Reason}");
		}

		var metadata = await LoadPluginMetadataAsync(pluginPath);
		if (metadata is null)
		{
			return (null, "Failed to load plugin metadata");
		}

		if (_loadedPlugins.ContainsKey(metadata.Id))
		{
			return (null, $"Plugin {metadata.Id} is already loaded");
		}

		var dependencies = await ValidatePluginDependenciesAsync(metadata);
		return dependencies.IsValid ? (metadata, null) : (null, $"Dependency validation failed: {dependencies.Reason}");
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

			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Unloading plugin {PluginId}", pluginId);
			}
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

			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Plugin {PluginId} unloaded successfully", pluginId);
			}
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
					if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
					{
						_logger.LogInformation("Plugin {PluginId} enabled", pluginId);
					}
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
					if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
					{
						_logger.LogInformation("Plugin {PluginId} disabled", pluginId);
					}
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

	public async Task<T?> GetPluginServiceAsync<T>(string pluginId)
		where T : class
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
					Error = $"Plugin {pluginId} not found"};
			}

			if (!loadedPlugin.IsEnabled)
			{
				return new PluginExecutionResult
				{
					Success = false,
					Error = $"Plugin {pluginId} is disabled"};
			}

			// Security check
			if (!await _securityManager.CanExecuteMethodAsync(methodName))
			{
				return new PluginExecutionResult
				{
					Success = false,
					Error = $"Security policy prevents execution of method {methodName}"};
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
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
			{
				_logger.LogDebug("Extension point {ExtensionPointId} registered", extensionPoint.Id);
			}
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
		var defaultExtensionPoints = CreateDefaultExtensionPoints();
		foreach (var extensionPoint in defaultExtensionPoints)
		{
			_extensionPoints[extensionPoint.Id] = extensionPoint;
		}
	}

	private static PluginExtensionPoint[] CreateDefaultExtensionPoints() =>
	[
		CreateDefaultExtensionPoint("process-execution-listener", "Process Execution Listener", "Listen to process execution events", typeof(IProcessExecutionListener), "eventType"),
		CreateDefaultExtensionPoint("activity-behavior", "Custom Activity Behavior", "Implement custom activity behaviors", typeof(ICustomActivityBehavior), "activityType"),
		CreateDefaultExtensionPoint("variable-resolver", "Variable Resolver", "Resolve custom variable expressions", typeof(IVariableResolver)),
		CreateDefaultExtensionPoint("connector", "External System Connector", "Connect to external systems", typeof(IExternalConnector), "systemType")
	];

	private static PluginExtensionPoint CreateDefaultExtensionPoint(string id, string name, string description, Type interfaceType, string? parameterName = null)
	{
		var parameters = new Dictionary<string, PluginParameter>(StringComparer.Ordinal);
		if (parameterName is not null)
		{
			parameters.Add(parameterName, new PluginParameter { Name = parameterName, Type = "string", Required = true });
		}

		return new PluginExtensionPoint { Id = id, Name = name, Description = description, InterfaceType = interfaceType.FullName!, Parameters = parameters };
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
							Reason = $"Required dependency not available: {dependency.Name} {dependency.Version}"};
					}
				}
			}

			return new ValidationResult
			{
				IsValid = true
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error validating plugin dependencies");
			return new ValidationResult
			{
				IsValid = false,
				Reason = ex.Message
			};
		}
	}

	private static async Task<bool> CheckDependencyAvailabilityAsync(PluginDependency dependency)
	{
		await Task.CompletedTask;
		var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, dependency.Name, StringComparison.OrdinalIgnoreCase));
		if (assembly is null)
		{
			return false;
		}

		return !Version.TryParse(dependency.Version, out var requiredVersion) || (assembly.GetName().Version is { } loadedVersion && loadedVersion >= requiredVersion);
	}

	private async Task<IPlugin?> CreatePluginInstanceAsync(Assembly assembly)
	{
		try
		{
			var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
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
				if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
				{
					_logger.LogDebug("Registered extension point {ExtensionPointId} from plugin {PluginId}", extensionPoint.Id, loadedPlugin.Id);
				}
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
			var pluginExtensionPoints = _extensionPoints.Values.Where(ep => ep.ProviderId == loadedPlugin.Id).ToList();
			foreach (var extensionPointId in pluginExtensionPoints.Select(extensionPoint => extensionPoint.Id))
			{
				_extensionPoints.TryRemove(extensionPointId, out _);
				if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
				{
					_logger.LogDebug("Unregistered extension point {ExtensionPointId} from plugin {PluginId}", extensionPointId, loadedPlugin.Id);
				}
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
