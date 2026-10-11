using System.Text.Json;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Engine.Execution;

public class BpmnEngine(IBpmnParser parser, IDistributedProcessEngine engine, IProcessInstanceStore store, ILogger<BpmnEngine> logger) : IBpmnEngine
{
	private readonly IBpmnParser _parser = parser ?? throw new ArgumentNullException(nameof(parser));
	private readonly IDistributedProcessEngine _engine = engine ?? throw new ArgumentNullException(nameof(engine));
	private readonly IProcessInstanceStore _store = store ?? throw new ArgumentNullException(nameof(store));
	private readonly ILogger<BpmnEngine> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Listet alle registrierten Prozesse auf.
	/// </summary>
	public async Task<IEnumerable<string>> ListProcessesAsync()
	{
		return await _store.ListProcessesAsync();
	}

	/// <summary>
	/// Startet eine neue Prozessinstanz für den angegebenen Prozessschlüssel.
	/// </summary>
	/// <param name="processKey">Der Schlüssel des Prozesses im Registry.</param>
	/// <param name="variables">Initiale Variablen für die Prozessinstanz.</param>
	/// <returns>Die ID der gestarteten Prozessinstanz.</returns>
	/// <exception cref="BpmnEngineException">Wird geworfen, wenn der Prozess nicht gefunden wird oder ein Fehler auftritt.</exception>
	public async Task<string> StartInstanceAsync(string processKey, Dictionary<string, object> variables)
	{
		try
		{
			var bpmnXml = await _store.GetBpmnModelAsync(processKey)
				?? throw new BpmnEngineException($"Process {processKey} not found.");

			var model = await _parser.ParseAsync(bpmnXml);
			if (!(model.Events ?? []).Any(e => e.Type == "startEvent"))
			{
				throw new BpmnEngineException($"No start event found in process {model.Id}");
			}

			var instanceId = Guid.NewGuid().ToString();
			var instance = new ProcessInstance
			{
				InstanceId = instanceId,
				ProcessId = model.Id,
				Status = ProcessInstanceStatus.Running,
				Variables = variables ?? new Dictionary<string, object>(StringComparer.Ordinal),
				CreatedAt = DateTime.UtcNow,
				LastModified = DateTime.UtcNow
			};

			var startEvent = (model.Events ?? []).First(e => e.Type == "startEvent");
			instance.ActiveTokens.Add(startEvent.Id);

			await _engine.ExecuteAsync(model);
			await _store.SaveInstanceAsync(instance);
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Started process instance {InstanceId} for process {ProcessKey}", instanceId, processKey);
			}
			return instanceId;
		}
		catch (BpmnParseException ex)
		{
			_logger.LogError(ex, "Failed to parse process {ProcessKey}", processKey);
			throw new BpmnEngineException($"Failed to parse process {processKey}", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error starting process instance for {ProcessKey}", processKey);
			throw new BpmnEngineException($"Failed to start process instance for {processKey}", ex);
		}
	}

	/// <summary>
	/// Ruft den Zustand einer Prozessinstanz ab.
	/// </summary>
	/// <param name="instanceId">Die ID der Prozessinstanz.</param>
	/// <returns>Der Zustand der Prozessinstanz.</returns>
	public async Task<ProcessInstance?> GetInstanceStateAsync(string instanceId)
	{
		return await _store.GetInstanceAsync(instanceId);
	}

	/// <summary>
	/// Registriert einen neuen Prozess.
	/// </summary>
	/// <param name="key">Der Schlüssel des Prozesses.</param>
	/// <param name="bpmnXml">Der BPMN-XML-Inhalt.</param>
	public async Task RegisterProcessAsync(string key, string bpmnXml)
	{
		try
		{
			// Validierung des XML
			await _parser.ParseAsync(bpmnXml);
			await _store.SaveBpmnModelAsync(key, bpmnXml);
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Registered process {ProcessKey}", key);
			}
		}
		catch (BpmnParseException ex)
		{
			_logger.LogError(ex, "Invalid BPMN XML for process {ProcessKey}", key);
			throw new BpmnEngineException($"Invalid BPMN XML for process {key}", ex);
		}
	}

	/// <summary>
	/// Schließt eine Benutzeraufgabe ab.
	/// </summary>
	/// <param name="instanceId">Die ID der Prozessinstanz.</param>
	/// <param name="taskId">Die ID der Aufgabe.</param>
	/// <param name="variables">Zusätzliche Variablen.</param>
	public async Task CompleteTaskAsync(string instanceId, string taskId, Dictionary<string, object> variables)
	{
		try
		{
			var instance = await _store.GetInstanceAsync(instanceId)
				?? throw new BpmnEngineException($"Instance {instanceId} not found.");

			if (!instance.ActiveTasks.Contains(taskId))
			{
				throw new BpmnEngineException($"Task {taskId} is not active in instance {instanceId}");
			}

			var bpmnXml = await _store.GetBpmnModelAsync(instance.ProcessId)
					?? throw new BpmnEngineException($"Process {instance.ProcessId} not found.");
			var model = await _parser.ParseAsync(bpmnXml);

			var task = (model.Tasks ?? []).FirstOrDefault(t => t.Id == taskId)
				?? throw new BpmnEngineException($"Task {taskId} not found in process {instance.ProcessId}");

			if (task.Type == "userTask")
			{
				await HandleUserTaskCompletionAsync(model, instance, task, variables);
			}

			instance.LastModified = DateTime.UtcNow;
			await _store.SaveInstanceAsync(instance);
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Completed task {TaskId} in instance {InstanceId}", taskId, instanceId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error completing task {TaskId} in instance {InstanceId}", taskId, instanceId);
			throw new BpmnEngineException($"Failed to complete task {taskId}", ex);
		}
	}

	private async Task HandleUserTaskCompletionAsync(BpmnModel model, ProcessInstance instance, BpmnTask task, Dictionary<string, object> variables)
	{
		// Validierung der Formularfelder (falls vorhanden)
		if (task.Attributes?.TryGetValue("camunda:formFields", out var formFieldsJson) == true)
		{
			using var formFields = JsonDocument.Parse(formFieldsJson);
			if (formFields.RootElement.ValueKind != JsonValueKind.Array)
			{
				throw new BpmnEngineException($"Form fields for task {task.Id} must be a JSON array.");
			}

			foreach (var field in formFields.RootElement.EnumerateArray())
			{
				if (!field.TryGetProperty("Id", out var idProperty) && !field.TryGetProperty("id", out idProperty))
				{
					throw new BpmnEngineException($"A form field for task {task.Id} has no id.");
				}

				var fieldId = idProperty.GetString()
								  ?? throw new BpmnEngineException($"A form field id for task {task.Id} is null.");
				if (!variables.ContainsKey(fieldId))
				{
					throw new BpmnEngineException($"Missing required field {fieldId} for task {task.Id}");
				}
			}
		}

		// Aktualisiere Variablen
		foreach (var kvp in variables)
		{
			instance.Variables[kvp.Key] = kvp.Value;
		}

		// Bewege Token
		instance.ActiveTasks.Remove(task.Id);
		var outgoingFlows = (model.SequenceFlows ?? []).Where(f => f.SourceRef == task.Id).Select(f => f.TargetRef).ToList();
		instance.ActiveTokens.AddRange(outgoingFlows);

		// Führe nächste Schritte aus
		await _engine.ExecuteAsync(model);
		if (instance.ActiveTasks.Count == 0 && instance.ActiveTokens.Count == 0)
		{
			instance.Status = ProcessInstanceStatus.Completed;
		}
	}
}

