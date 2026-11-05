using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VertexBPMN.Portal.Client.Bff;

/// <summary>Client-side mirror of the BFF's released-application metadata (no raw source/content).</summary>
public sealed record ReleasedAppMeta(
    [property: JsonPropertyName("applicationId")] string ApplicationId,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("pageCount")] int PageCount,
    [property: JsonPropertyName("pageRoutes")] IReadOnlyList<string> PageRoutes);

/// <summary>Client-side mirror of a BFF data-source read result.</summary>
public sealed record DataSourceReadResult(
    [property: JsonPropertyName("binding")] string Binding,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rowCount")] int RowCount,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);

/// <summary>Client-side mirror of a BFF released-action outcome.</summary>
public sealed record ActionOutcome(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("taskId")] Guid? TaskId,
    [property: JsonPropertyName("status")] string Status);

/// <summary>Client-side mirror of the BFF's projected task status view.</summary>
public sealed record TaskStatusView(
    [property: JsonPropertyName("taskId")] Guid TaskId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("candidateRole")] string? CandidateRole,
    [property: JsonPropertyName("created")] DateTimeOffset? Created,
    [property: JsonPropertyName("completed")] DateTimeOffset? Completed);

/// <summary>Client-side mirror of a personal-inbox task row (controlled projection surface).</summary>
public sealed record TaskInboxItem(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("processInstanceId")] Guid ProcessInstanceId,
    [property: JsonPropertyName("created")] DateTime? Created,
    [property: JsonPropertyName("due")] DateTime? Due,
    [property: JsonPropertyName("completed")] DateTime? Completed,
    [property: JsonPropertyName("formKey")] string? FormKey);

/// <summary>Client-side mirror of the personal-inbox paged result.</summary>
public sealed record TaskInboxPage(
    [property: JsonPropertyName("items")] IReadOnlyList<TaskInboxItem> Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("hasMore")] bool HasMore);
