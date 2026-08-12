using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.ML;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.ML;

/// <summary>
/// Builds predictive models from persisted process-mining events.
/// Models are trained per tenant and process definition and are rebuilt on demand
/// after application restart or when the training endpoint is called.
/// </summary>
public sealed class HistoricalPredictiveAnalyticsService : IPredictiveAnalyticsService
{
    private const int MinimumTrainingSamples = 2;
    private readonly ProcessMiningEventDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<HistoricalPredictiveAnalyticsService> _logger;
    private readonly MLContext _mlContext = new(seed: 42);
    private readonly Dictionary<string, TrainedDurationModel> _models = new(StringComparer.Ordinal);

    public HistoricalPredictiveAnalyticsService(
        ProcessMiningEventDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<HistoricalPredictiveAnalyticsService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task TrainModelsAsync(string? requestedTenantId = null)
    {
        var tenantId = EffectiveTenantId(requestedTenantId);
        var records = await LoadTrainingDataAsync(null, tenantId).ConfigureAwait(false);
        var tenantGroups = records.GroupBy(record => ModelKey(record.TenantId, record.ProcessDefinitionKey));

        _models.Clear();
        foreach (var group in tenantGroups)
        {
            var rows = group.ToList();
            if (rows.Count < MinimumTrainingSamples)
                continue;

            _models[group.Key] = TrainDurationModel(rows);
        }

        _logger.LogInformation(
            "Trained {ModelCount} predictive analytics models from {SampleCount} historical process instances",
            _models.Count,
            records.Count);
    }

    public async Task<ProcessDurationPrediction> PredictProcessDurationAsync(
        string processDefinitionKey,
        Dictionary<string, object> variables,
        string? requestedTenantId = null)
    {
        ValidateProcessDefinitionKey(processDefinitionKey);
        var tenantId = EffectiveTenantId(requestedTenantId);
        var records = await LoadTrainingDataAsync(processDefinitionKey, tenantId).ConfigureAwait(false);
        var modelKey = ModelKey(tenantId, processDefinitionKey);

        if (!_models.TryGetValue(modelKey, out var model) || model.SampleCount != records.Count)
        {
            if (records.Count < MinimumTrainingSamples)
                throw InsufficientData(processDefinitionKey, records.Count);

            model = TrainDurationModel(records);
            _models[modelKey] = model;
        }

        var prediction = _mlContext.Model
            .CreatePredictionEngine<DurationFeatures, DurationPredictionOutput>(model.Model)
            .Predict(new DurationFeatures
            {
                VariableCount = variables?.Count ?? 0,
                StartHour = DateTimeOffset.UtcNow.Hour,
                StartDayOfWeek = (float)DateTimeOffset.UtcNow.DayOfWeek,
                ActivityCount = model.MedianActivityCount
            });

        var estimate = MathF.Max(0.1f, prediction.EstimatedDurationMinutes);
        var margin = MathF.Max((float)model.RootMeanSquaredError, estimate * 0.15f);
        return new ProcessDurationPrediction
        {
            ProcessDefinitionKey = processDefinitionKey,
            EstimatedDurationMinutes = estimate,
            MinDuration = MathF.Max(0.1f, estimate - margin),
            MaxDuration = estimate + margin,
            ConfidenceScore = model.ConfidenceScore,
            InfluencingFactors =
            [
                $"{model.SampleCount} completed historical instances",
                $"variable count: {variables?.Count ?? 0}",
                $"median activity count: {model.MedianActivityCount:0.##}"
            ],
            SuggestedOptimizations = model.RootMeanSquaredError > estimate * 0.35f
                ? ["Collect more completed instances for this process definition."]
                : []
        };
    }

    public async Task<ProcessCompletionPrediction> PredictProcessCompletionAsync(Guid processInstanceId, string? requestedTenantId = null)
    {
        var tenantId = EffectiveTenantId(requestedTenantId);
        var events = await _db.Events.AsNoTracking()
            .Where(evt => evt.ProcessInstanceId == processInstanceId.ToString() && TenantMatches(evt.TenantId, tenantId))
            .OrderBy(evt => evt.Timestamp)
            .ToListAsync()
            .ConfigureAwait(false);

        if (events.Count == 0)
            throw new KeyNotFoundException($"Process instance '{processInstanceId}' was not found for the current tenant.");

        var start = events.FirstOrDefault(IsProcessStart) ?? events[0];
        var end = events.FirstOrDefault(IsProcessEnd);
        var processDefinitionKey = ExtractProcessDefinitionKey(events) ?? "unknown";
        var modelRecords = await LoadTrainingDataAsync(processDefinitionKey, tenantId).ConfigureAwait(false);
        if (modelRecords.Count < MinimumTrainingSamples)
            throw InsufficientData(processDefinitionKey, modelRecords.Count);

        var duration = await PredictProcessDurationAsync(processDefinitionKey, new Dictionary<string, object>(), tenantId)
            .ConfigureAwait(false);
        var now = end?.Timestamp ?? DateTimeOffset.UtcNow;
        var elapsedMinutes = Math.Max(0, (now - start.Timestamp).TotalMinutes);
        var completed = end is not null;
        var probability = completed
            ? 1f
            : Math.Clamp(
                1f - (float)Math.Max(0, elapsedMinutes - duration.EstimatedDurationMinutes)
                    / MathF.Max(duration.MaxDuration, 1f),
                0.05f,
                0.95f);

        return new ProcessCompletionPrediction
        {
            ProcessInstanceId = processInstanceId,
            CompletionProbability = probability,
            EstimatedCompletionTime =
                (start.Timestamp + TimeSpan.FromMinutes(duration.EstimatedDurationMinutes)).UtcDateTime,
            ConfidenceScore = duration.ConfidenceScore,
            RiskFactors = elapsedMinutes > duration.MaxDuration && !completed
                ? ["Instance has exceeded the upper historical duration bound."]
                : [],
            Recommendations = elapsedMinutes > duration.MaxDuration && !completed
                ? ["Inspect active tasks and incidents for this process instance."]
                : []
        };
    }

    public async Task<ProcessBottleneckPrediction> PredictBottlenecksAsync(string processDefinitionKey, string? requestedTenantId = null)
    {
        ValidateProcessDefinitionKey(processDefinitionKey);
        var tenantId = EffectiveTenantId(requestedTenantId);
        var records = await LoadTrainingDataAsync(processDefinitionKey, tenantId).ConfigureAwait(false);
        if (records.Count < MinimumTrainingSamples)
            throw InsufficientData(processDefinitionKey, records.Count);

        var events = await LoadTenantEventsAsync(tenantId).ConfigureAwait(false);
        var relevant = events
            .GroupBy(evt => evt.ProcessInstanceId)
            .Where(group => string.Equals(
                ExtractProcessDefinitionKey(group),
                processDefinitionKey,
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(group => group)
            .ToList();
        var activities = BuildActivityPredictions(relevant);
        var overallRisk = activities.Count == 0
            ? 0f
            : activities.Average(activity => activity.BottleneckProbability);

        return new ProcessBottleneckPrediction
        {
            ProcessDefinitionKey = processDefinitionKey,
            OverallBottleneckRisk = overallRisk,
            ActivityPredictions = activities,
            CriticalPath = activities
                .OrderByDescending(activity => activity.AverageWaitTime)
                .Take(5)
                .Select(activity => activity.ActivityId)
                .ToList(),
            OptimizationPriority = overallRisk >= 0.7f ? "high" : overallRisk >= 0.4f ? "medium" : "low"
        };
    }

    public async Task<ProcessOptimizationSuggestion> GetOptimizationSuggestionsAsync(string processDefinitionKey, string? requestedTenantId = null)
    {
        var bottlenecks = await PredictBottlenecksAsync(processDefinitionKey, requestedTenantId).ConfigureAwait(false);
        var suggestions = bottlenecks.ActivityPredictions
            .Where(activity => activity.BottleneckProbability >= 0.5f)
            .Select(activity => new OptimizationAction
            {
                Type = "bottleneck",
                Priority = activity.BottleneckProbability >= 0.75f ? "high" : "medium",
                Description = $"Activity '{activity.ActivityName}' has elevated waiting time.",
                Recommendation = "Review assignment rules, queue capacity and task completion SLA.",
                ExpectedImpact = $"Reduce average wait time of {activity.AverageWaitTime:0.##} minutes."
            })
            .ToList();

        return new ProcessOptimizationSuggestion
        {
            ProcessDefinitionKey = processDefinitionKey,
            GeneratedAt = DateTime.UtcNow,
            OverallScore = 1f - bottlenecks.OverallBottleneckRisk,
            Suggestions = suggestions,
            ModelConfidence = Math.Clamp(0.5f + suggestions.Count / 10f, 0.5f, 0.95f),
            NextReviewDate = DateTime.UtcNow.AddDays(7)
        };
    }

    public async Task<string> ExportTrainingDataAsync(string? processDefinitionKey = null, string? requestedTenantId = null)
    {
        if (!string.IsNullOrWhiteSpace(processDefinitionKey))
            ValidateProcessDefinitionKey(processDefinitionKey);

        var tenantId = EffectiveTenantId(requestedTenantId);
        var records = await LoadTrainingDataAsync(processDefinitionKey, tenantId).ConfigureAwait(false);
        var builder = new StringBuilder();
        builder.AppendLine(
            "tenantId,processDefinitionKey,processInstanceId,startedAt,endedAt,durationMinutes,variableCount,activityCount,startHour,startDayOfWeek");

        foreach (var record in records.OrderBy(record => record.StartedAt))
        {
            builder.AppendLine(string.Join(',',
                Csv(record.TenantId),
                Csv(record.ProcessDefinitionKey),
                Csv(record.ProcessInstanceId),
                Csv(record.StartedAt.ToString("O", CultureInfo.InvariantCulture)),
                Csv(record.EndedAt.ToString("O", CultureInfo.InvariantCulture)),
                record.DurationMinutes.ToString(CultureInfo.InvariantCulture),
                record.VariableCount,
                record.ActivityCount,
                record.StartHour,
                record.StartDayOfWeek));
        }

        return builder.ToString();
    }

    private async Task<List<TrainingRecord>> LoadTrainingDataAsync(string? processDefinitionKey, string? tenantId)
    {
        var events = await LoadTenantEventsAsync(tenantId).ConfigureAwait(false);
        return events
            .GroupBy(evt => evt.ProcessInstanceId)
            .Select(BuildTrainingRecord)
            .Where(record => record is not null)
            .Select(record => record!)
            .Where(record => string.IsNullOrWhiteSpace(processDefinitionKey)
                || string.Equals(record.ProcessDefinitionKey, processDefinitionKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<List<ProcessMiningEvent>> LoadTenantEventsAsync(string? tenantId)
    {
        return await _db.Events.AsNoTracking()
            .Where(evt => TenantMatches(evt.TenantId, tenantId))
            .OrderBy(evt => evt.Timestamp)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    private TrainedDurationModel TrainDurationModel(IReadOnlyList<TrainingRecord> records)
    {
        var rows = records.Select(record => new DurationFeatures
        {
            VariableCount = record.VariableCount,
            StartHour = record.StartHour,
            StartDayOfWeek = record.StartDayOfWeek,
            ActivityCount = record.ActivityCount,
            Label = record.DurationMinutes
        }).ToList();

        var data = _mlContext.Data.LoadFromEnumerable(rows);
        var pipeline = _mlContext.Transforms
            .Concatenate(
                "Features",
                nameof(DurationFeatures.VariableCount),
                nameof(DurationFeatures.StartHour),
                nameof(DurationFeatures.StartDayOfWeek),
                nameof(DurationFeatures.ActivityCount))
            .Append(_mlContext.Regression.Trainers.Sdca(
                labelColumnName: nameof(DurationFeatures.Label),
                featureColumnName: "Features"));
        var model = pipeline.Fit(data);
        var metrics = _mlContext.Regression.Evaluate(
            model.Transform(data),
            labelColumnName: nameof(DurationFeatures.Label));
        var mean = rows.Average(row => row.Label);

        return new TrainedDurationModel(
            model,
            rows.Count,
            metrics.RootMeanSquaredError,
            Math.Clamp(
                1f - (float)metrics.RootMeanSquaredError / MathF.Max(mean, 1f),
                0.1f,
                0.95f),
            rows.Select(row => row.ActivityCount).OrderBy(value => value).ElementAt(rows.Count / 2));
    }

    private static TrainingRecord? BuildTrainingRecord(IGrouping<string, ProcessMiningEvent> group)
    {
        var events = group.OrderBy(evt => evt.Timestamp).ToList();
        var start = events.FirstOrDefault(IsProcessStart);
        var end = events.FirstOrDefault(IsProcessEnd);
        if (start is null || end is null || end.Timestamp < start.Timestamp)
            return null;

        var key = ExtractProcessDefinitionKey(events) ?? "unknown";
        return new TrainingRecord(
            start.TenantId ?? string.Empty,
            key,
            start.ProcessInstanceId,
            start.Timestamp,
            end.Timestamp,
            (float)(end.Timestamp - start.Timestamp).TotalMinutes,
            ExtractVariables(start.PayloadJson),
            events.Select(evt => evt.ActivityId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Count(),
            start.Timestamp.Hour,
            (int)start.Timestamp.DayOfWeek);
    }

    private static List<ActivityBottleneckPrediction> BuildActivityPredictions(
        IReadOnlyList<ProcessMiningEvent> events)
    {
        var spans = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        foreach (var group in events
            .Where(evt => !string.IsNullOrWhiteSpace(evt.ActivityId))
            .GroupBy(evt => evt.ActivityId!, StringComparer.Ordinal))
        {
            var created = group.Where(IsTaskCreated).OrderBy(evt => evt.Timestamp).ToList();
            var completed = group.Where(IsTaskCompleted).OrderBy(evt => evt.Timestamp).ToList();
            var waits = created
                .Zip(completed, (left, right) => Math.Max(0, (right.Timestamp - left.Timestamp).TotalMinutes))
                .ToList();
            if (waits.Count > 0)
                spans[group.Key] = waits;
        }

        if (spans.Count == 0)
            return [];

        var maxWait = spans.Values.SelectMany(value => value).DefaultIfEmpty(1).Max();
        return spans
            .Select(pair =>
            {
                var average = pair.Value.Average();
                var risk = Math.Clamp((float)(average / Math.Max(maxWait, 1)), 0f, 1f);
                return new ActivityBottleneckPrediction
                {
                    ActivityId = pair.Key,
                    ActivityName = pair.Key,
                    BottleneckProbability = risk,
                    AverageWaitTime = (float)average,
                    ThroughputImpact = risk,
                    RecommendedActions = risk >= 0.5f
                        ? ["Review queue capacity and assignment rules."]
                        : []
                };
            })
            .OrderByDescending(value => value.BottleneckProbability)
            .ToList();
    }

    private string? EffectiveTenantId(string? requestedTenantId)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.IsInRole("Admin") == true)
            return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId;

        var claimTenantId = user?.FindFirstValue("tenant_id");
        if (!string.IsNullOrWhiteSpace(requestedTenantId)
            && !string.Equals(requestedTenantId, claimTenantId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The requested tenant is not available to the current user.");

        return claimTenantId;
    }

    private static bool TenantMatches(string? eventTenantId, string? tenantId)
        => tenantId is null || string.Equals(eventTenantId, tenantId, StringComparison.Ordinal);

    private static string ModelKey(string? tenantId, string processDefinitionKey)
        => $"{tenantId ?? "*"}|{processDefinitionKey}";

    private static void ValidateProcessDefinitionKey(string processDefinitionKey)
    {
        if (string.IsNullOrWhiteSpace(processDefinitionKey))
            throw new ArgumentException("ProcessDefinitionKey is required.", nameof(processDefinitionKey));
    }

    private static NotSupportedException InsufficientData(string key, int count)
        => new($"Predictive analytics requires at least {MinimumTrainingSamples} completed historical instances for '{key}'; found {count}.");

    private static bool IsProcessStart(ProcessMiningEvent evt)
        => evt.EventType.Equals("ProcessStarted", StringComparison.OrdinalIgnoreCase)
            || evt.EventType.Equals("PROCESS_STARTED", StringComparison.OrdinalIgnoreCase);

    private static bool IsProcessEnd(ProcessMiningEvent evt)
        => evt.EventType.Equals("ProcessEnded", StringComparison.OrdinalIgnoreCase)
            || evt.EventType.Equals("ProcessCompleted", StringComparison.OrdinalIgnoreCase)
            || evt.EventType.Equals("PROCESS_ENDED", StringComparison.OrdinalIgnoreCase)
            || evt.EventType.Equals("PROCESS_COMPLETED", StringComparison.OrdinalIgnoreCase);

    private static bool IsTaskCreated(ProcessMiningEvent evt)
        => evt.EventType.Contains("TASK_CREATED", StringComparison.OrdinalIgnoreCase)
            || evt.EventType.Equals("TaskCreated", StringComparison.OrdinalIgnoreCase);

    private static bool IsTaskCompleted(ProcessMiningEvent evt)
        => evt.EventType.Contains("TASK_COMPLETED", StringComparison.OrdinalIgnoreCase)
            || evt.EventType.Equals("TaskCompleted", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractProcessDefinitionKey(IEnumerable<ProcessMiningEvent> events)
    {
        foreach (var evt in events)
        {
            if (string.IsNullOrWhiteSpace(evt.PayloadJson))
                continue;

            try
            {
                using var document = JsonDocument.Parse(evt.PayloadJson);
                foreach (var property in new[] { "processDefinitionKey", "processKey", "processId" })
                {
                    if (document.RootElement.TryGetProperty(property, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                        return value.GetString();
                }
            }
            catch (JsonException)
            {
                // Older events may contain raw variables rather than the envelope.
            }
        }

        return null;
    }

    private static int ExtractVariables(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return 0;

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.TryGetProperty("variables", out var variables)
                && variables.ValueKind == JsonValueKind.Object)
                return variables.EnumerateObject().Count();

            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().Count()
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string Csv(string value)
        => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed class DurationFeatures
    {
        public float VariableCount { get; set; }
        public float StartHour { get; set; }
        public float StartDayOfWeek { get; set; }
        public float ActivityCount { get; set; }
        public float Label { get; set; }
    }

    private sealed record TrainingRecord(
        string TenantId,
        string ProcessDefinitionKey,
        string ProcessInstanceId,
        DateTimeOffset StartedAt,
        DateTimeOffset EndedAt,
        float DurationMinutes,
        int VariableCount,
        int ActivityCount,
        int StartHour,
        int StartDayOfWeek);

    private sealed record TrainedDurationModel(
        ITransformer Model,
        int SampleCount,
        double RootMeanSquaredError,
        float ConfidenceScore,
        float MedianActivityCount);
}
