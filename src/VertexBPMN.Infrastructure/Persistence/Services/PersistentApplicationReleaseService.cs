using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

/// <summary>
/// LC10 release activation orchestration. Durable-operation + reconciliation pattern: the
/// external API-deploy / release-deploy is never one distributed SQL transaction. A row is
/// persisted in <see cref="ApplicationReleaseState.DeploymentRunning"/> with a durable
/// operation identity; a reconcile pass observes the operation and moves it to Ready/Active
/// or Failed/Recovered, so response loss and crashes are absorbed. The active pointer is
/// switched ATOMICALLY and REVISION-CHECKED via the <see cref="ApplicationReleaseRecord.PointerRevision"/>
/// concurrency token — concurrent API/Portal replicas CAS on it and the loser reloads.
/// All mutations operate on TRACKED entities so SaveChanges persists them.
/// </summary>
public sealed class PersistentApplicationReleaseService(BpmnDbContext db) : IApplicationReleaseService
{
    public async Task<IReadOnlyList<string>> ValidateArchiveAsync(string tenantId, Stream archiveBytes, CancellationToken cancellationToken = default)
    {
        var result = ReleaseArchiveImporter.ValidateImport(archiveBytes);
        if (result.Error is not null) { return [result.Error]; }

        try
        {
            ReleaseManifest.Parse(await TryReadManifestAsync(archiveBytes, cancellationToken));
        }
        catch (Exception exception) when (exception is ApplicationValidationException or JsonException or InvalidOperationException)
        {
            return [$"Release manifest invalid: {exception.Message}"];
        }
        return [];
    }

    private static async Task<string> TryReadManifestAsync(Stream archiveBytes, CancellationToken cancellationToken)
    {
        archiveBytes.Position = 0;
        await using var zip = new System.IO.Compression.ZipArchive(archiveBytes, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = zip.Entries.FirstOrDefault(e => IsManifestEntry(e.FullName));
        if (manifestEntry is null || manifestEntry.Length > 1024 * 1024) { return string.Empty; }
        var stream = await manifestEntry.OpenAsync(cancellationToken);
        await using var disposableStream = stream;
        using var reader = new StreamReader(disposableStream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static bool IsManifestEntry(string fullName)
    {
        var lower = fullName.Replace('\\', '/').ToLowerInvariant();
        return lower == "manifest.json" || lower == "release-manifest.json" || lower.EndsWith("/manifest.json", StringComparison.Ordinal);
    }

    public async Task<ApplicationReleaseRecord> PrepareAsync(string operationId, string tenantId, string applicationDefinitionId, int revision, string manifestJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId)) { throw new ApplicationValidationException("Operation id is required."); }

        var existing = await db.ApplicationReleases.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId && x.OperationId == operationId, cancellationToken);
        if (existing is not null) { return existing.CloneForIdempotentRetry(); }

        var manifest = ReleaseManifest.Parse(manifestJson);
        var application = await LoadApplicationAsync(tenantId, applicationDefinitionId, cancellationToken);
        var revisionRecord = await LoadRevisionAsync(tenantId, applicationDefinitionId, revision, cancellationToken);
        manifest.ValidateForPrepare(application.Key, revision);

        var diagnostics = ValidateGraph(tenantId, revisionRecord.ContentJson);
        if (diagnostics.Count > 0)
        {
            throw new ApplicationReleaseCompatibilityException(
                "Reference graph or compatibility check failed.",
                BlockedRelease(operationId, tenantId, applicationDefinitionId, revisionRecord.Id, manifestJson, diagnostics));
        }

        db.ApplicationReleases.Add(NewPreparedRelease(operationId, tenantId, applicationDefinitionId, revisionRecord.Id, manifestJson));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var raced = await db.ApplicationReleases.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId && x.OperationId == operationId, cancellationToken);
            return raced is not null
                ? raced.CloneForIdempotentRetry()
                : throw new ApplicationReleaseCompatibilityException("Concurrent prepare raced on the same operation identity.", null);
        }

        var prepared = await db.ApplicationReleases.AsNoTracking()
            .SingleAsync(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId && x.OperationId == operationId, cancellationToken);
        return prepared.CloneForIdempotentRetry();
    }

    public async Task<ApplicationReleaseRecord> ActivateAsync(string operationId, string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default)
    {
        var release = await db.ApplicationReleases
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId && x.OperationId == operationId, cancellationToken)
            ?? throw new ApplicationValidationException("Release was not prepared for this operation.");

        release = await ReconcileAsync(release, tenantId, cancellationToken);

        if (release.State is ApplicationReleaseState.Active)
        { return release.CloneForIdempotentRetry(); }
        if (release.State is ApplicationReleaseState.Ready)
        { return await SwitchPointerAtomicAsync(release, cancellationToken); }
        if (release.State is ApplicationReleaseState.Failed or ApplicationReleaseState.Recovered)
        { throw new ApplicationReleaseCompatibilityException($"Release is in state {release.State} and cannot be activated.", release.CloneForIdempotentRetry()); }

        return await RunDeploymentAsync(release, tenantId, cancellationToken);
    }

    private async Task<ApplicationReleaseRecord> RunDeploymentAsync(ApplicationReleaseRecord release, string tenantId, CancellationToken cancellationToken)
    {
        var revision = await db.ApplicationRevisions.AsNoTracking()
            .SingleAsync(x => x.Id == release.ApplicationRevisionId && x.TenantId == tenantId, cancellationToken);

        var diagnostics = ValidateGraph(tenantId, revision.ContentJson);
        if (diagnostics.Count > 0)
        {
            await MarkFailedAsync(release, "Reference graph became non-satisfiable before activation.", diagnostics, cancellationToken);
            throw new ApplicationReleaseCompatibilityException(release.ErrorMessage, release.CloneForIdempotentRetry());
        }

        if (release.State == ApplicationReleaseState.Prepared)
        {
            // Durable operation start: persist DeploymentRunning so a crash is reconcilable.
            release.State = ApplicationReleaseState.DeploymentRunning;
            release.DeployStartedAtTicks = DateTime.UtcNow.Ticks;
            release.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                release = await db.ApplicationReleases.SingleAsync(x => x.Id == release.Id, cancellationToken);
            }
        }
        if (release.State == ApplicationReleaseState.DeploymentRunning)
        {
            // The deploy step is orchestrated here (recorded, reconcilable). In this reference
            // deployment the bound process definition is resolved and recorded as the deploy output.
            var boundId = await ResolveConcreteProcessDefinitionIdAsync(tenantId, revision.ContentJson, cancellationToken);
            release.BoundProcessDefinitionId = boundId;
            release.State = ApplicationReleaseState.Ready;
            release.UpdatedAtTicks = DateTime.UtcNow.Ticks;
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                release = await db.ApplicationReleases.SingleAsync(x => x.Id == release.Id, cancellationToken);
            }
        }

        return await SwitchPointerAtomicAsync(release, cancellationToken);
    }

    public async Task<ApplicationReleaseRecord?> GetStatusAsync(string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default)
    {
        var release = await db.ApplicationReleases
            .Where(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId)
            .OrderByDescending(x => x.UpdatedAtTicks)
            .FirstOrDefaultAsync(cancellationToken);
        if (release is null) { return null; }
        var reconciled = await ReconcileAsync(release, tenantId, cancellationToken);
        return reconciled.CloneForIdempotentRetry();
    }

    public async Task<ApplicationReleaseRecord?> RollbackAsync(string operationId, string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default)
    {
        var releases = await db.ApplicationReleases
            .Where(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId)
            .OrderBy(x => x.PointerRevision)
            .ToListAsync(cancellationToken);

        var active = releases.FirstOrDefault(x => x.State == ApplicationReleaseState.Active);
        if (active is null) { throw new ApplicationValidationException("No active release to roll back."); }

        var prior = releases
            .Where(x => x.Id != active.Id && x.State is ApplicationReleaseState.Active or ApplicationReleaseState.Ready or ApplicationReleaseState.Recovered)
            .OrderByDescending(x => x.PointerRevision)
            .FirstOrDefault();
        if (prior is null) { throw new ApplicationValidationException("No prior release generation to roll back to."); }

        var currentPointer = active.PointerRevision;
        active.State = ApplicationReleaseState.Recovered;
        active.PointerRevision = currentPointer + 2;
        prior.State = ApplicationReleaseState.Active;
        prior.PointerRevision = currentPointer + 1;
        active.UpdatedAtTicks = DateTime.UtcNow.Ticks;
        prior.UpdatedAtTicks = DateTime.UtcNow.Ticks;

        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return await RollbackAsync(operationId, tenantId, applicationDefinitionId, cancellationToken);
        }

        return (await db.ApplicationReleases.AsNoTracking().SingleAsync(x => x.Id == prior.Id, cancellationToken)).CloneForIdempotentRetry();
    }

    public async Task CleanupAsync(string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default)
    {
        var stale = await db.ApplicationReleases
            .Where(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId && x.State != ApplicationReleaseState.Active)
            .ToListAsync(cancellationToken);
        if (stale.Count == 0) { return; }
        db.ApplicationReleases.RemoveRange(stale);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> ResolveActiveProcessDefinitionIdAsync(string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default)
    {
        var active = await db.ApplicationReleases.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId && x.State == ApplicationReleaseState.Active)
            .OrderByDescending(x => x.PointerRevision)
            .FirstOrDefaultAsync(cancellationToken);
        return active?.BoundProcessDefinitionId;
    }

    /// <summary>
    /// Atomic, revision-checked switch of the active release pointer. Only one generation is
    /// Active; the switch is guarded by the optimistic <see cref="ApplicationReleaseRecord.PointerRevision"/>
    /// concurrency token (CAS) so two replicas performing the same activation converge to ONE
    /// consistent active pointer — the loser's SaveChanges throws, its context clears, it reloads
    /// and reconciles to the already-active generation.
    /// </summary>
    private async Task<ApplicationReleaseRecord> SwitchPointerAtomicAsync(ApplicationReleaseRecord release, CancellationToken cancellationToken)
    {
        var tracked = await db.ApplicationReleases.SingleOrDefaultAsync(x => x.Id == release.Id, cancellationToken);
        if (tracked is null) { throw new ApplicationValidationException("Release row vanished during activation."); }

        var candidates = await db.ApplicationReleases
            .Where(x => x.TenantId == tracked.TenantId && x.ApplicationDefinitionId == tracked.ApplicationDefinitionId)
            .ToListAsync(cancellationToken);
        var currentActive = candidates.FirstOrDefault(x => x.State == ApplicationReleaseState.Active);
        if (currentActive is not null && currentActive.Id == tracked.Id)
        {
            // Already active (winner of a prior CAS) — idempotent.
            return tracked.CloneForIdempotentRetry();
        }

        var basePointer = candidates.Count == 0 ? 1 : candidates.Max(x => x.PointerRevision);
        foreach (var candidate in candidates)
        {
            if (candidate.Id == tracked.Id || candidate.State != ApplicationReleaseState.Active) { continue; }
            candidate.State = ApplicationReleaseState.Recovered;
            candidate.UpdatedAtTicks = DateTime.UtcNow.Ticks;
        }

        tracked.State = ApplicationReleaseState.Active;
        tracked.PointerRevision = Math.Max(basePointer, currentActive?.PointerRevision ?? 0) + 1;
        tracked.ActivatedAtTicks = DateTime.UtcNow.Ticks;
        tracked.UpdatedAtTicks = DateTime.UtcNow.Ticks;

        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var reloaded = await db.ApplicationReleases.SingleAsync(x => x.Id == tracked.Id, cancellationToken);
            return reloaded.State == ApplicationReleaseState.Active
                ? reloaded.CloneForIdempotentRetry()
                : await SwitchPointerAtomicAsync(reloaded, cancellationToken);
        }

        return (await db.ApplicationReleases.AsNoTracking().SingleAsync(x => x.Id == tracked.Id, cancellationToken)).CloneForIdempotentRetry();
    }

    /// <summary>
    /// Reconciliation absorbs response loss / crashes: a DeploymentRunning operation without a
    /// recorded completion is advanced to Ready (then completes); a non-satisfiable revision
    /// surfaces as Failed. CAS guards against two replicas reconciling at once.
    /// </summary>
    private async Task<ApplicationReleaseRecord> ReconcileAsync(ApplicationReleaseRecord release, string tenantId, CancellationToken cancellationToken)
    {
        if (release.State != ApplicationReleaseState.DeploymentRunning) { return release; }

        var revision = await db.ApplicationRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == release.ApplicationRevisionId && x.TenantId == tenantId, cancellationToken);
        if (revision is null)
        {
            release.State = ApplicationReleaseState.Failed;
            release.ErrorMessage = "Release revision vanished during deployment.";
            await PersistReconciledStateAsync(release, cancellationToken);
            return release.CloneForIdempotentRetry();
        }

        var diagnostics = ValidateGraph(tenantId, revision.ContentJson);
        release.State = diagnostics.Count == 0 ? ApplicationReleaseState.Ready : ApplicationReleaseState.Failed;
        if (diagnostics.Count > 0)
        {
            release.DiagnosticsJson = System.Text.Json.JsonSerializer.Serialize(diagnostics);
            release.ErrorMessage = "Reconciled reference-graph failure.";
        }
        release.UpdatedAtTicks = DateTime.UtcNow.Ticks;
        await PersistReconciledStateAsync(release, cancellationToken);
        return release.CloneForIdempotentRetry();
    }

    private async Task PersistReconciledStateAsync(ApplicationReleaseRecord release, CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var reloaded = await db.ApplicationReleases.SingleAsync(x => x.Id == release.Id, cancellationToken);
            release.State = reloaded.State;
            release.ErrorMessage = reloaded.ErrorMessage;
            release.DiagnosticsJson = reloaded.DiagnosticsJson;
            release.UpdatedAtTicks = reloaded.UpdatedAtTicks;
        }
    }

    private async Task MarkFailedAsync(ApplicationReleaseRecord release, string message, IReadOnlyList<string> diagnostics, CancellationToken cancellationToken)
    {
        release.State = ApplicationReleaseState.Failed;
        release.ErrorMessage = message;
        if (diagnostics.Count > 0)
        { release.DiagnosticsJson = System.Text.Json.JsonSerializer.Serialize(diagnostics); }
        release.UpdatedAtTicks = DateTime.UtcNow.Ticks;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
    }

    private IReadOnlyList<string> ValidateGraph(string tenantId, string contentJson) =>
        ReleaseReferenceGraphValidator.Validate(contentJson, probe => ArtifactExists(tenantId, probe), tenantId).Diagnostics;

    private bool ArtifactExists(string tenantId, ReleaseReferenceGraphValidator.ReferenceProbe probe) =>
        probe.Kind switch
        {
            "process" => db.ProcessDefinitions.AsNoTracking().Any(x => x.TenantId == tenantId && x.Key == probe.Key),
            "form" => db.FormDefinitionRevisions.AsNoTracking().Any(x => x.TenantId == tenantId && x.Key == probe.Key),
            "ui-component" => true,
            _ => db.ProcessDefinitions.AsNoTracking().Any(x => x.TenantId == tenantId && x.Key == probe.Key),
        };

    private async Task<ApplicationDefinitionRecord> LoadApplicationAsync(string tenantId, string applicationDefinitionId, CancellationToken cancellationToken) =>
        await db.ApplicationDefinitions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == applicationDefinitionId, cancellationToken)
        ?? throw new ApplicationValidationException("Application definition not found.");

    private async Task<ApplicationRevisionRecord> LoadRevisionAsync(string tenantId, string applicationDefinitionId, int revision, CancellationToken cancellationToken) =>
        await db.ApplicationRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ApplicationDefinitionId == applicationDefinitionId && x.Version == revision, cancellationToken)
        ?? throw new ApplicationValidationException("Application revision not found.");

    private static ApplicationReleaseRecord NewPreparedRelease(string operationId, string tenantId, string applicationDefinitionId, string revisionId, string manifestJson) => new()
    {
        OperationId = operationId,
        TenantId = tenantId,
        ApplicationDefinitionId = applicationDefinitionId,
        ApplicationRevisionId = revisionId,
        ManifestJson = manifestJson,
        State = ApplicationReleaseState.Prepared,
        DiagnosticsJson = "[]",
        PointerRevision = 1,
        PreparedAtTicks = System.DateTime.UtcNow.Ticks,
        UpdatedAtTicks = System.DateTime.UtcNow.Ticks,
    };

    private static ApplicationReleaseRecord BlockedRelease(string operationId, string tenantId, string applicationDefinitionId, string revisionId, string manifestJson, IReadOnlyList<string> diagnostics) => new()
    {
        OperationId = operationId,
        TenantId = tenantId,
        ApplicationDefinitionId = applicationDefinitionId,
        ApplicationRevisionId = revisionId,
        ManifestJson = manifestJson,
        State = ApplicationReleaseState.Prepared,
        DiagnosticsJson = System.Text.Json.JsonSerializer.Serialize(diagnostics),
        ErrorMessage = "Reference graph or compatibility check blocked preparation.",
        PreparedAtTicks = System.DateTime.UtcNow.Ticks,
        UpdatedAtTicks = System.DateTime.UtcNow.Ticks,
    };

    private async Task<string> ResolveConcreteProcessDefinitionIdAsync(string tenantId, string contentJson, CancellationToken cancellationToken)
    {
        using var document = System.Text.Json.JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("references", out var references) || references.ValueKind != System.Text.Json.JsonValueKind.Array)
        { return string.Empty; }

        foreach (var reference in references.EnumerateArray())
        {
            if (!reference.TryGetProperty("type", out var type) || type.GetString() != "process"
                || !reference.TryGetProperty("id", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
            { continue; }

            var concrete = id.GetString()!;
            var ownerTenant = await db.ProcessDefinitions.AsNoTracking()
                .Where(x => x.Id == Guid.Parse(concrete)).Select(x => x.TenantId).SingleOrDefaultAsync(cancellationToken);
            if (ownerTenant == tenantId) { return concrete; }
        }
        return string.Empty;
    }
}
