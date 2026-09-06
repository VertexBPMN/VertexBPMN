using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Shared helpers for the new matrix-driven E2E tests (matrix sections 2.1-2.11).
// All route names below were VERIFIED against the real controllers in VertexBPMN.Api/Controllers:
//   runtime ...  RuntimeController            [Route("api/runtime")]        POST start / GET {id}
//   task ......  TaskController               [Route("api/task")]           GET ?processInstanceId= / POST {id}/complete
//   history ...  HistoryController            [Route("api/history")]        GET by-process-instance/{processInstanceId}
//   message ...  VertexMessageController      [Route("api/vertex/message")] POST (correlate)
//   signal ....  VertexSignalController       [Route("api/vertex/signal")]  POST (broadcast)
// They intentionally differ from the sketched routes in the original plan and are the REAL contract.
public sealed partial class LocalStudioInfrastructureTests
{
    /// <summary>
    /// Starts a process with the given variables via the real API (POST api/runtime/start) and returns
    /// the instance id. Used when execution semantics (not modeler UI) are the test target.
    /// </summary>
    private static async Task<Guid> StartProcessWithVariablesAsync(
        HttpClient apiClient,
        string processKey,
        string? tenantId,
        string businessKey,
        IDictionary<string, object> variables)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "api/runtime/start",
            new { processDefinitionKey = processKey, variables, businessKey, tenantId },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        using var instance = JsonDocument.Parse(body);
        return instance.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Reads the full history event list for an instance (api/history/by-process-instance/{id}).</summary>
    private async Task<JsonElement[]> GetHistoryAsync(Guid instanceId)
    {
        using var client = host.CreateApiClient();
        using var response = await client.GetAsync(
            $"api/history/by-process-instance/{instanceId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<JsonElement[]>(body) ?? [];
    }

    /// <summary>
    /// Polls until a history event whose elementId equals <paramref name="elementId"/> appears,
    /// then returns the full history. Guards against slow async completion without a blind delay.
    /// </summary>
    private async Task<JsonElement[]> WaitForHistoryContainsElementAsync(
        Guid instanceId, string elementId, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTime.UtcNow < deadline)
        {
            var history = await GetHistoryAsync(instanceId);
            if (history.Any(e => e.TryGetProperty("elementId", out var el) && el.GetString() == elementId))
                return history;
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"History for instance {instanceId} never contained element '{elementId}'.");
    }

    /// <summary>Asserts that NO history event carries the given elementId (e.g. a skipped gateway branch).</summary>
    private async Task AssertHistoryDoesNotContainElementAsync(Guid instanceId, string elementId)
    {
        var history = await GetHistoryAsync(instanceId);
        Assert.DoesNotContain(
            history,
            e => e.TryGetProperty("elementId", out var el) && el.GetString() == elementId);
    }

    /// <summary>
    /// Timeout-aware overload of the existing WaitForInstanceStateAsync. The base helper polls up to
    /// ~15s; timer-driven tests (boundary/escalation, timer catch) need a longer, explicit budget.
    /// </summary>
    private async Task WaitForInstanceStateAsync(Guid instanceId, string expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var state = await GetInstanceStateAsync(instanceId);
            if (string.Equals(state, expected, StringComparison.OrdinalIgnoreCase))
                return;
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Instance {instanceId} did not reach state '{expected}' within {timeout}.");
    }

    /// <summary>Returns all currently open user tasks for an instance (api/task?processInstanceId=...).</summary>
    private async Task<JsonElement[]> GetOpenTasksAsync(Guid instanceId)
    {
        using var client = host.CreateApiClient();
        using var response = await client.GetAsync(
            $"api/task?processInstanceId={instanceId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<JsonElement[]>(body) ?? [];
    }

    /// <summary>
    /// Completes a user task via the real API (POST api/task/{id}/complete). Completing does not
    /// require a prior claim (TaskController.Complete calls the task service directly), so the
    /// optional variables are the only body besides an admin-resolved tenant.
    /// </summary>
    private async Task CompleteTaskAsync(Guid taskId, IDictionary<string, object>? variables = null)
    {
        using var client = host.CreateApiClient();
        using var response = await client.PostAsJsonAsync(
            $"api/task/{taskId}/complete",
            new { variables = variables ?? new Dictionary<string, object>() },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
    }

    /// <summary>
    /// Correlates a message via the real API (POST api/vertex/message). When
    /// <paramref name="processInstanceId"/> is provided the message resumes that waiting instance
    /// (intermediate-catch or boundary subscription); when null it may instantiate a message-start
    /// process. Returns the parsed response body (resultType correlates / not_found).
    /// </summary>
    private static async Task<JsonElement> CorrelateMessageAsync(
        HttpClient apiClient,
        string messageName,
        Guid? processInstanceId = null,
        IDictionary<string, object>? variables = null)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "api/vertex/message",
            new
            {
                messageName,
                processInstanceId = processInstanceId is null ? null : processInstanceId.ToString(),
                variables = variables ?? new Dictionary<string, object>(),
                tenantId = (string?)null
            },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>Broadcasts a signal via the real API (POST api/vertex/signal).</summary>
    private static async Task BroadcastSignalAsync(
        HttpClient apiClient,
        string signalName,
        IDictionary<string, object>? variables = null)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "api/vertex/signal",
            new { signalName, variables = variables ?? new Dictionary<string, object>(), tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
    }
}
