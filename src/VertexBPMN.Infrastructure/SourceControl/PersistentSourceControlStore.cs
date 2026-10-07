using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.SourceControl.Abstractions;
using System.Data;
using System.Data.Common;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Relational-only store. Each worker/request uses its own scoped DbContext.</summary>
public sealed partial class PersistentSourceControlStore(BpmnDbContext db, IDataProtectionProvider protection, IOptions<SourceControlOptions> options)
    : ISourceControlAccessStore
{
    private void Relational()
    {
        if (!options.Value.Enabled || !db.Database.IsRelational()) throw new SourceControlSecurityException(SourceControlErrorCode.Disabled);
    }

    public async Task<RepositoryAccessSnapshot> CreateBindingAsync(SourceControlContext context,
        IReadOnlyCollection<string> authenticatedRoles, RepositoryBinding binding, CancellationToken cancellationToken)
    {
        Relational();
        if (!authenticatedRoles.Contains("Admin", StringComparer.Ordinal) || binding.TenantId != context.TenantId)
            throw new SourceControlSecurityException(SourceControlErrorCode.Forbidden);
        if (binding.Id == Guid.Empty || binding.ModelRoots.Count == 0)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        SourceControlHttps.ValidateTarget(binding.Remote, options.Value.AllowedHosts);
        SourceControlInputPolicy.ValidateBranch(binding.DefaultBranch);
        SourceControlInputPolicy.ValidateBranch(binding.ReleaseBranch);
        foreach (var root in binding.ModelRoots) SourceControlInputPolicy.ValidateRelativePath(root);
        var bindingJson = JsonSerializer.Serialize(binding);
        var grants = new[] { new RepositoryGrant(context.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage) };
        db.SourceControlBindings.Add(new() { Id = binding.Id, TenantId = context.TenantId,
            Revision = 1, BindingJson = bindingJson, GrantsJson = JsonSerializer.Serialize(grants) });
        await db.SaveChangesAsync(cancellationToken);
        return new(JsonSerializer.Deserialize<RepositoryBinding>(bindingJson)!, 1, grants);
    }

    public async Task<bool> ReplaceGrantsAsync(SourceControlContext context, Guid repositoryId,
        IReadOnlyCollection<string> roles, long expectedRevision, IReadOnlyList<RepositoryGrant> grants,
        CancellationToken cancellationToken)
    {
        Relational();
        var copiedGrants = grants.ToArray();
        const RepositoryPermission allowed = RepositoryPermission.Read | RepositoryPermission.Manage
            | RepositoryPermission.Commit | RepositoryPermission.Push | RepositoryPermission.PullRequest | RepositoryPermission.Deploy;
        if (copiedGrants.Length > 1000 || copiedGrants.Select(x => x.ActorId).Distinct(StringComparer.Ordinal).Count() != copiedGrants.Length
            || copiedGrants.Any(x => string.IsNullOrWhiteSpace(x.ActorId) || x.ActorId.Length > 512
                || x.ActorId.Any(char.IsControl) || (x.Permissions & ~allowed) != 0))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        var json = JsonSerializer.Serialize(copiedGrants);
        var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, RepositoryPermission.Manage);
        if (access.Revision != expectedRevision) return false;
        return await db.SourceControlBindings.Where(x => x.Id == repositoryId && x.TenantId == context.TenantId
            && x.Revision == expectedRevision).ExecuteUpdateAsync(update => update
                .SetProperty(x => x.GrantsJson, json).SetProperty(x => x.Revision, x => x.Revision + 1), cancellationToken) == 1;
    }

    public async Task<Guid> CreateSessionAsync(SourceControlContext context, Guid repositoryId,
        IReadOnlyCollection<string> roles, GitCommitId baseCommit, Guid generation,
        CancellationToken cancellationToken)
    {
        Relational();
        if (generation == Guid.Empty) throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, RepositoryPermission.Commit);
        var row = new SourceControlSessionRecord { Id = Guid.NewGuid(), TenantId = context.TenantId,
            ActorId = context.ActorId, RepositoryId = repositoryId, BaseCommit = baseCommit.Value,
            DocumentGeneration = generation, Revision = 0,
            ExpiresUtcTicks = DateTimeOffset.UtcNow.Add(options.Value.Limits.SessionIdleRetention).UtcTicks };
        db.SourceControlSessions.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task<bool> SaveSnapshotsAsync(SourceControlContext context, Guid sessionId,
        IReadOnlyCollection<string> roles, long expectedRevision, IReadOnlyList<ModelSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        Relational();
        if (snapshots.Count == 0 || snapshots.Count > options.Value.Limits.MaxCommitFiles)
            throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
        if (snapshots.Any(x => x.ContentLength > options.Value.Limits.MaxModelBytes)
            || snapshots.Sum(x => (long)x.ContentLength) > options.Value.Limits.MaxRepositoryBytes)
            throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
        var copies = snapshots.Select(s => new SavedSnapshot(s.Path, s.Kind, s.DocumentGeneration,
            s.LocalRevision, s.CopyContent())).ToArray();
        if (copies.Sum(x => (long)x.Bytes.Length) > options.Value.Limits.MaxRepositoryBytes)
            throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
        var row = await db.SourceControlSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId
            && x.TenantId == context.TenantId && x.ActorId == context.ActorId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, RepositoryPermission.Commit);
        if (row.Revision != expectedRevision || row.ExpiresUtcTicks <= DateTimeOffset.UtcNow.UtcTicks) return false;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previous = row.ProtectedSnapshots.Length == 0 ? [] :
            JsonSerializer.Deserialize<SavedSnapshot[]>(Unprotect(context, row.RepositoryId, row.ProtectedSnapshots))!;
        foreach (var copy in copies)
        {
            var earlier = previous.SingleOrDefault(x => string.Equals(x.Path, copy.Path, StringComparison.OrdinalIgnoreCase));
            if (copy.Generation != row.DocumentGeneration || !paths.Add(copy.Path)
                || earlier is not null && (copy.Revision < earlier.Revision
                    || copy.Revision == earlier.Revision && !copy.Bytes.AsSpan().SequenceEqual(earlier.Bytes)))
                throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
            SourceControlInputPolicy.DemandSafeBpmn(new(copy.Path, copy.Kind, copy.Generation, copy.Revision, copy.Bytes),
                access.Binding, options.Value.Limits);
        }
        var protectedSnapshots = Protect(context, row.RepositoryId, JsonSerializer.SerializeToUtf8Bytes(copies));
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        var expiresTicks = DateTimeOffset.UtcNow.Add(options.Value.Limits.SessionIdleRetention).UtcTicks;
        return await db.SourceControlSessions.Where(x => x.Id == sessionId && x.TenantId == context.TenantId
            && x.ActorId == context.ActorId && x.Revision == expectedRevision
            && x.ExpiresUtcTicks > nowTicks).ExecuteUpdateAsync(update => update
                .SetProperty(x => x.ProtectedSnapshots, protectedSnapshots)
                .SetProperty(x => x.ExpiresUtcTicks, expiresTicks)
                .SetProperty(x => x.Revision, x => x.Revision + 1), cancellationToken) == 1;
    }

    private sealed record SavedSnapshot(string Path, SourceModelKind Kind, Guid Generation, long Revision, byte[] Bytes);

    public async Task<RepositoryAccessSnapshot?> FindAsync(string tenantId, Guid repositoryId, CancellationToken cancellationToken)
    {
        Relational();
        var row = await db.SourceControlBindings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == repositoryId && x.TenantId == tenantId, cancellationToken);
        if (row is null) return null;
        var binding = JsonSerializer.Deserialize<RepositoryBinding>(row.BindingJson)!;
        if (binding.Id != row.Id || binding.TenantId != row.TenantId)
            throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        return new(binding, row.Revision, JsonSerializer.Deserialize<RepositoryGrant[]>(row.GrantsJson)!);
    }

    /// <summary>Accept only the confirmed persisted session snapshot; later edits cannot change this request.</summary>
    public async Task<Guid> EnqueueCommitAsync(SourceControlContext context, Guid repositoryId,
        IReadOnlyCollection<string> roles, SourceControlIdempotencyKey key, CommitJobSubmission submission,
        CancellationToken cancellationToken)
    {
        Relational();
        if (submission is null || submission.BaseCommit is null || submission.Snapshots is null
            || submission.Snapshots.Any(x => x is null)
            || submission.SessionId == Guid.Empty || submission.SessionRevision < 1
            || string.IsNullOrWhiteSpace(submission.Message)
            || submission.Message.Length > options.Value.Limits.MaxCommitMessageCharacters
            || submission.Message.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))
            || submission.Snapshots.Count == 0 || submission.Snapshots.Count > options.Value.Limits.MaxCommitFiles)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        // Freeze caller-owned collections before the first await. ModelSnapshot already owns its bytes.
        var snapshots = submission.Snapshots.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
        if (snapshots.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshots.Length)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        var copiedRoles = roles.ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        RepositoryAccessPolicy.Demand(context, access.Binding, copiedRoles, access.Grants, RepositoryPermission.Commit);
        foreach (var snapshot in snapshots) SourceControlInputPolicy.DemandSafeBpmn(snapshot, access.Binding, options.Value.Limits);
        if (snapshots.Sum(x => (long)x.ContentLength) > options.Value.Limits.MaxRepositoryBytes)
            throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
        SourceControlInputPolicy.ValidateBranch(submission.WorkBranch);
        if (submission.WorkBranch != SourceControlInputPolicy.WorkBranch(submission.SessionId)
            || submission.WorkBranch == access.Binding.DefaultBranch || submission.WorkBranch == access.Binding.ReleaseBranch)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        var identity = new AcceptedCommit(1, submission.SessionId,
            submission.SessionRevision, submission.BaseCommit.Value, submission.WorkBranch, submission.Message,
            snapshots.Select(x => new AcceptedSnapshot(x.Path, x.Kind, x.DocumentGeneration, x.LocalRevision, x.ContentSha256, x.CopyContent())).ToArray(),
            ActorAuthority: options.Value.IdentityAuthority);
        var identityBytes = JsonSerializer.SerializeToUtf8Bytes(identity);
        var existing = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == context.TenantId
            && x.RepositoryId == repositoryId && x.Kind == (int)SourceControlOperationKind.Commit
            && x.IdempotencyKey == key.Value, cancellationToken);
        if (existing is not null)
        {
            if (existing.ActorId != context.ActorId) throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
            if (existing.RequestHash != RequestHash(access.Revision, identityBytes))
                throw new SourceControlSecurityException(SourceControlErrorCode.IdempotencyConflict);
            await transaction.CommitAsync(cancellationToken);
            return existing.Id; // An identical retry remains valid after the editor advances to N+1.
        }
        var session = await db.SourceControlSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submission.SessionId
            && x.RepositoryId == repositoryId && x.TenantId == context.TenantId && x.ActorId == context.ActorId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        if (session.Revision != submission.SessionRevision || session.BaseCommit != submission.BaseCommit.Value
            || session.ExpiresUtcTicks <= DateTimeOffset.UtcNow.UtcTicks || session.ProtectedSnapshots.Length == 0)
            throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
        var saved = JsonSerializer.Deserialize<SavedSnapshot[]>(Unprotect(context, repositoryId, session.ProtectedSnapshots))!;
        foreach (var snapshot in snapshots)
        {
            var persisted = saved.SingleOrDefault(x => x.Path == snapshot.Path);
            if (persisted is null || persisted.Kind != snapshot.Kind || persisted.Generation != snapshot.DocumentGeneration
                || snapshot.DocumentGeneration != session.DocumentGeneration || persisted.Revision != snapshot.LocalRevision
                || !persisted.Bytes.AsSpan().SequenceEqual(snapshot.CopyContent()))
                throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
        }
        var canonical = JsonSerializer.SerializeToUtf8Bytes(identity with { SchemaVersion = 2,
            AcceptedUtcTicks = DateTimeOffset.UtcNow.UtcTicks, BindingRevision = access.Revision });
        var id = await EnqueueAsync(context, repositoryId, copiedRoles, SourceControlOperationKind.Commit, key,
            canonical, cancellationToken, identityBytes);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    private sealed record AcceptedCommit(int SchemaVersion, Guid SessionId, long SessionRevision,
        string BaseCommit, string WorkBranch, string Message, AcceptedSnapshot[] Snapshots,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long AcceptedUtcTicks = 0,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long BindingRevision = 0,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ActorAuthority = null);
    private sealed record AcceptedSnapshot(string Path, SourceModelKind Kind, Guid Generation,
        long Revision, string ContentSha256, byte[] Bytes);

    private static string RequestHash(long revision, byte[] request) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{revision}:{Convert.ToHexStringLower(SHA256.HashData(request))}")));

    // Internal storage primitive only. Public application callers must use typed validated acceptance.
    internal async Task<Guid> EnqueueAsync(SourceControlContext context, Guid repositoryId,
        IReadOnlyCollection<string> authenticatedRoles, SourceControlOperationKind kind,
        SourceControlIdempotencyKey key, ReadOnlyMemory<byte> canonicalRequest, CancellationToken cancellationToken,
        ReadOnlyMemory<byte>? identityRequest = null)
    {
        Relational();
        var request = canonicalRequest.ToArray();
        // Push copies an already bounded commit request plus its receipt. Do not reject
        // a valid near-limit commit merely because the immutable push envelope is larger.
        var maximumRequestBytes = kind == SourceControlOperationKind.Push ? 6 * 1024 * 1024 : 3 * 1024 * 1024;
        if (request.Length == 0 || request.Length > maximumRequestBytes)
            throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
        var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        var permission = kind switch
        {
            SourceControlOperationKind.Read => RepositoryPermission.Read,
            SourceControlOperationKind.OpenSession or SourceControlOperationKind.Commit => RepositoryPermission.Commit,
            SourceControlOperationKind.Push => RepositoryPermission.Push,
            SourceControlOperationKind.PullRequest => RepositoryPermission.PullRequest,
            SourceControlOperationKind.Deploy => RepositoryPermission.Deploy,
            _ => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput)
        };
        RepositoryAccessPolicy.Demand(context, access.Binding, authenticatedRoles, access.Grants, permission);
        // The application supplies a canonical, validated request; snapshot-copy before any await.
        var hash = RequestHash(access.Revision, identityRequest?.ToArray() ?? request);
        var existing = await LookupAsync();
        if (existing is not null) return Check(existing);
        var row = new SourceControlOperationRecord
        {
            Id = Guid.NewGuid(), RepositoryId = repositoryId, TenantId = context.TenantId,
            ActorId = context.ActorId, Kind = (int)kind, IdempotencyKey = key.Value, RequestHash = hash,
            ProtectedRequest = Protect(context, repositoryId, request),
            State = (int)SourceControlOperationState.Queued, UpdatedUtcTicks = DateTimeOffset.UtcNow.UtcTicks
        };
        db.SourceControlOperations.Add(row);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.Entry(row).State = EntityState.Detached;
            // Unique constraint arbitrates concurrent acceptance. Other DB errors are not hidden.
            existing = await LookupAsync();
            if (existing is null) throw;
            return Check(existing);
        }
        return row.Id;

        Task<SourceControlOperationRecord?> LookupAsync() => db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == context.TenantId && x.RepositoryId == repositoryId
                && x.Kind == (int)kind && x.IdempotencyKey == key.Value, cancellationToken);
        Guid Check(SourceControlOperationRecord existing)
        {
            if (existing.ActorId != context.ActorId)
                throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
            if (existing.RequestHash != hash)
                throw new SourceControlSecurityException(SourceControlErrorCode.IdempotencyConflict);
            return existing.Id;
        }
    }

    public async Task<long?> TryClaimAsync(string tenantId, Guid id, string worker, DateTimeOffset now,
        TimeSpan duration, CancellationToken cancellationToken)
    {
        Relational();
        if (string.IsNullOrWhiteSpace(worker) || worker.Length > 128 || worker.Any(char.IsControl)
            || duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(5))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
        var active = db.SourceControlOperations.AsNoTracking().Where(x => x.LeaseUntilUtcTicks > now.UtcTicks
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling));
        if (await active.CountAsync(cancellationToken) >= options.Value.Limits.MaxConcurrentJobsTotal
            || await active.CountAsync(x => x.TenantId == tenantId, cancellationToken) >= options.Value.Limits.MaxConcurrentJobsPerTenant)
            return null;
        var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (row is null) return null;
        // Expired running jobs are reconciliation work, NEVER blindly queued writes.
        var queued = row.State == (int)SourceControlOperationState.Queued;
        var recoverable = (row.State == (int)SourceControlOperationState.Running
            || row.State == (int)SourceControlOperationState.ResultUnknown
            || row.State == (int)SourceControlOperationState.Reconciling)
            && (!row.LeaseUntilUtcTicks.HasValue || row.LeaseUntilUtcTicks <= now.UtcTicks);
        if (!queued && !recoverable) return null;
        var changed = await db.SourceControlOperations.Where(x => x.Id == id && x.TenantId == tenantId
            && x.Fence == row.Fence && x.State == row.State
            && (!x.LeaseUntilUtcTicks.HasValue || x.LeaseUntilUtcTicks <= now.UtcTicks))
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.State, queued ? (int)SourceControlOperationState.Running : (int)SourceControlOperationState.Reconciling)
                .SetProperty(x => x.Fence, x => x.Fence + 1)
                .SetProperty(x => x.LeaseOwner, worker)
                .SetProperty(x => x.LeaseUntilUtcTicks, now.Add(duration).UtcTicks)
                .SetProperty(x => x.UpdatedUtcTicks, now.UtcTicks), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed == 1 ? row.Fence + 1 : null;
        }
        catch (Exception exception) when (IsClaimContention(exception))
        {
            // Serializable arbitration rejects a contender; leave it queued for a later poll.
            // Other provider failures must not be confused with an unavailable slot.
            return null;
        }
    }

    /// <summary>Execution input comes from the encrypted accepted request, not the current editor session.</summary>
    internal async Task<AcceptedCommitWork> ReadCommitWorkAsync(SourceControlContext context, Guid id,
        string worker, long fence, IReadOnlyCollection<string> currentRoles, CancellationToken cancellationToken)
    {
        Relational();
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
            && x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.LeaseOwner == worker
            && x.Fence == fence && x.LeaseUntilUtcTicks > now && x.Kind == (int)SourceControlOperationKind.Commit
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling), cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        RepositoryAccessPolicy.Demand(context, access.Binding, currentRoles, access.Grants, RepositoryPermission.Commit);
        return DecodeCommitWork(context, row, access);
    }

    private AcceptedCommitWork DecodeCommitWork(SourceControlContext context, SourceControlOperationRecord row,
        RepositoryAccessSnapshot access)
    {
        try
        {
            var accepted = JsonSerializer.Deserialize<AcceptedCommit>(Unprotect(context, row.RepositoryId, row.ProtectedRequest))
                ?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            if (accepted.SchemaVersion != 2 || accepted.AcceptedUtcTicks <= 0 || accepted.BindingRevision <= 0
                || accepted.Snapshots is null || accepted.Snapshots.Length == 0)
                throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            if (accepted.ActorAuthority != options.Value.IdentityAuthority)
                throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
            var identity = accepted with { SchemaVersion = 1, AcceptedUtcTicks = 0, BindingRevision = 0 };
            if (accepted.BindingRevision != access.Revision
                || RequestHash(access.Revision, JsonSerializer.SerializeToUtf8Bytes(identity)) != row.RequestHash)
                throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
            var snapshots = accepted.Snapshots.Select(x => new ModelSnapshot(x.Path, x.Kind, x.Generation, x.Revision, x.Bytes)).ToArray();
            for (var i = 0; i < snapshots.Length; i++)
                if (snapshots[i].ContentSha256 != accepted.Snapshots[i].ContentSha256)
                    throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            return new(access.Binding, access.Revision, new(row.Id, new(row.IdempotencyKey), accepted.SessionId,
                new(accepted.BaseCommit), accepted.WorkBranch, accepted.Message, snapshots),
                new DateTimeOffset(accepted.AcceptedUtcTicks, TimeSpan.Zero),
                (SourceControlOperationState)row.State);
        }
        catch (SourceControlSecurityException) { throw; }
        catch { throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
    }

    // Hold the binding/operation write locks while publishing the private local ref.
    // The intent is already durable; rollback after the Git effect leaves reconciliation evidence.
    internal async Task PublishLocalCommitAsync(SourceControlContext context, AcceptedCommitWork work,
        string worker, long fence, Func<CancellationToken, Task> publish, CancellationToken cancellationToken)
    {
        Relational();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await db.SourceControlBindings.Where(x => x.Id == work.Binding.Id && x.TenantId == context.TenantId
            && x.Revision == work.BindingRevision).ExecuteUpdateAsync(u => u.SetProperty(x => x.Revision, x => x.Revision), cancellationToken) != 1)
            throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
        var now = DateTimeOffset.UtcNow;
        if (await db.SourceControlOperations.Where(x => x.Id == work.Command.OperationId && x.TenantId == context.TenantId
            && x.ActorId == context.ActorId && x.LeaseOwner == worker && x.Fence == fence
            && x.LeaseUntilUtcTicks > now.UtcTicks && x.ProtectedResult != null
            && x.Kind == (int)SourceControlOperationKind.Commit
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling))
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Fence, x => x.Fence), cancellationToken) != 1)
            throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        await publish(cancellationToken);
        if (!await FinishAsync(context.TenantId, work.Command.OperationId, worker, fence,
            SourceControlOperationState.CommittedLocal, DateTimeOffset.UtcNow, cancellationToken))
            throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool IsClaimContention(Exception exception) => exception switch
    {
        Npgsql.PostgresException postgres => postgres.SqlState is "40001" or "40P01",
        Microsoft.Data.Sqlite.SqliteException sqlite => sqlite.SqliteErrorCode is 5 or 6,
        Microsoft.Data.SqlClient.SqlException sql => sql.Number is 1205 or 3960,
        InvalidOperationException wrapped when wrapped.InnerException is DbException inner => IsClaimContention(inner),
        _ => false
    };

    public async Task<bool> RenewLeaseAsync(string tenantId, Guid id, string worker, long fence,
        DateTimeOffset now, TimeSpan duration, CancellationToken cancellationToken)
    {
        Relational();
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(5))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        return await db.SourceControlOperations.Where(x => x.TenantId == tenantId && x.Id == id
            && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now.UtcTicks
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.LeaseUntilUtcTicks, now.Add(duration).UtcTicks)
                .SetProperty(x => x.UpdatedUtcTicks, now.UtcTicks), cancellationToken) == 1;
    }

    /// <summary>Worker-only access to the accepted immutable input, fenced by its current lease.</summary>
    public async Task<byte[]> ReadAcceptedRequestAsync(string tenantId, Guid id, string worker, long fence,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        Relational();
        var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.Id == id && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now.UtcTicks
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling), cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        return Unprotect(new(row.TenantId, row.ActorId), row.RepositoryId, row.ProtectedRequest);
    }

    public async Task<bool> SaveResultAsync(string tenantId, Guid id, string worker, long fence,
        ReadOnlyMemory<byte> canonicalResult, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Relational();
        if (canonicalResult.Length == 0 || canonicalResult.Length > 3 * 1024 * 1024)
            throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
        var result = canonicalResult.ToArray();
        var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.Id == id && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now.UtcTicks, cancellationToken);
        if (row is null) return false;
        var payload = Protect(new(row.TenantId, row.ActorId), row.RepositoryId, result);
        return await db.SourceControlOperations.Where(x => x.TenantId == tenantId && x.Id == id
            && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now.UtcTicks
            && x.ProtectedResult == null
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProtectedResult, payload), cancellationToken) == 1;
    }

    public async Task<byte[]?> ReadSavedResultAsync(string tenantId, Guid id, string worker, long fence,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        Relational();
        var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.Id == id && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now.UtcTicks
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling), cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        return row.ProtectedResult is null ? null : Unprotect(new(row.TenantId, row.ActorId), row.RepositoryId, row.ProtectedResult);
    }

    public async Task<bool> FinishAsync(string tenantId, Guid id, string worker, long fence,
        SourceControlOperationState state, DateTimeOffset now, CancellationToken cancellationToken,
        SourceControlErrorCode? errorCode = null)
    {
        Relational();
        if (errorCode.HasValue && !Enum.IsDefined(errorCode.Value))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        if (state is not (SourceControlOperationState.CommittedLocal or SourceControlOperationState.Pushed
            or SourceControlOperationState.Succeeded or SourceControlOperationState.Conflict
            or SourceControlOperationState.Failed or SourceControlOperationState.Cancelled
            or SourceControlOperationState.ResultUnknown))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        return await db.SourceControlOperations.Where(x => x.TenantId == tenantId && x.Id == id
            && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now.UtcTicks
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling))
            .Where(x => (state != SourceControlOperationState.CommittedLocal || x.Kind == (int)SourceControlOperationKind.Commit)
                && (state != SourceControlOperationState.Pushed || x.Kind == (int)SourceControlOperationKind.Push)
                && (state != SourceControlOperationState.Succeeded || x.Kind != (int)SourceControlOperationKind.Commit && x.Kind != (int)SourceControlOperationKind.Push))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.State, (int)state)
                .SetProperty(x => x.ErrorCode, errorCode.HasValue ? (int?)errorCode.Value : null)
                .SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseUntilUtcTicks, (long?)null)
                .SetProperty(x => x.UpdatedUtcTicks, now.UtcTicks), cancellationToken) == 1;
    }

    internal async Task<bool> ConfirmCompletedCommitAsync(SourceControlContext context, Guid id, long fence,
        CommitReceipt receipt, CancellationToken cancellationToken)
    {
        Relational();
        var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
            && x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.Fence == fence
            && x.Kind == (int)SourceControlOperationKind.Commit && x.State == (int)SourceControlOperationState.CommittedLocal,
            cancellationToken);
        if (row?.ProtectedResult is null) return false;
        var saved = JsonSerializer.Deserialize<StoredLocalCommit>(Unprotect(context, row.RepositoryId, row.ProtectedResult));
        return saved is not null && saved.SchemaVersion == 1 && saved.RepositoryId == row.RepositoryId
            && JsonSerializer.SerializeToUtf8Bytes(saved.Receipt).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(receipt));
    }

    public async Task<IReadOnlyList<ModelSnapshot>> ReadSnapshotsAsync(SourceControlContext context, Guid sessionId,
        IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        Relational();
        var row = await db.SourceControlSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId
            && x.TenantId == context.TenantId && x.ActorId == context.ActorId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, RepositoryPermission.Read);
        if (row.ExpiresUtcTicks <= DateTimeOffset.UtcNow.UtcTicks)
            throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
        if (row.ProtectedSnapshots.Length == 0) return [];
        var bytes = Unprotect(context, row.RepositoryId, row.ProtectedSnapshots);
        var snapshots = JsonSerializer.Deserialize<SavedSnapshot[]>(bytes)!;
        return snapshots.Select(x => new ModelSnapshot(x.Path, x.Kind, x.Generation, x.Revision, x.Bytes)).ToArray();
    }

    public async Task<SourceControlOperation?> GetOperationAsync(SourceControlContext context, Guid id,
        IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        Relational();
        var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
            && x.TenantId == context.TenantId, cancellationToken);
        if (row is null) return null;
        var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken);
        if (access is null) return null;
        RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants,
            row.ActorId == context.ActorId ? RepositoryPermission.Read : RepositoryPermission.Manage);
        return new(row.Id, row.TenantId, row.ActorId, row.RepositoryId, (SourceControlOperationKind)row.Kind,
            (SourceControlOperationState)row.State, new DateTimeOffset(row.UpdatedUtcTicks, TimeSpan.Zero),
            row.ErrorCode.HasValue ? (SourceControlErrorCode)row.ErrorCode : null);
    }

    /// <summary>Crash detection only: uncertain effects require provider reconciliation, never replay.</summary>
    internal async Task<int> DetectExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Relational();
        var candidates = await db.SourceControlOperations.AsNoTracking()
            .Where(x => (x.State == (int)SourceControlOperationState.Running
                || x.State == (int)SourceControlOperationState.Reconciling)
                && (!x.LeaseUntilUtcTicks.HasValue || x.LeaseUntilUtcTicks <= now.UtcTicks))
            .OrderBy(x => x.UpdatedUtcTicks).Take(100).ToArrayAsync(cancellationToken);
        var changed = 0;
        foreach (var row in candidates)
        {
            changed += await db.SourceControlOperations.Where(x => x.Id == row.Id && x.TenantId == row.TenantId
                && x.State == row.State && x.Fence == row.Fence
                && (!x.LeaseUntilUtcTicks.HasValue || x.LeaseUntilUtcTicks <= now.UtcTicks))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.State, (int)SourceControlOperationState.ResultUnknown)
                    .SetProperty(x => x.Fence, x => x.Fence + 1)
                    .SetProperty(x => x.LeaseOwner, (string?)null)
                    .SetProperty(x => x.LeaseUntilUtcTicks, (long?)null)
                    .SetProperty(x => x.UpdatedUtcTicks, now.UtcTicks), cancellationToken);
        }
        return changed;
    }

    private byte[] Unprotect(SourceControlContext context, Guid repositoryId, string payload)
    {
        try
        {
            return protection.CreateProtector("VertexBPMN.SourceControl.Request.v1",
                context.TenantId, repositoryId.ToString("N"), context.ActorId).Unprotect(Convert.FromBase64String(payload));
        }
        catch { throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
    }

    /// <summary>Internal maintenance: durable idempotency hashes and result/provenance are retained.</summary>
    public async Task<int> PruneExpiredDetailsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Relational();
        var cutoff = now.Subtract(options.Value.Limits.CompletedJobRetention).UtcTicks;
        var count = await db.SourceControlSessions.Where(x => x.ExpiresUtcTicks <= now.UtcTicks
            && !db.SourceControlOperations.Any(job => job.RepositoryId == x.RepositoryId && job.TenantId == x.TenantId
                && job.ActorId == x.ActorId && (job.State == (int)SourceControlOperationState.Queued
                    || job.State == (int)SourceControlOperationState.Running || job.State == (int)SourceControlOperationState.Reconciling
                    || job.State == (int)SourceControlOperationState.ResultUnknown)))
            .ExecuteDeleteAsync(cancellationToken);
        count += await db.SourceControlOperations.Where(x => x.UpdatedUtcTicks <= cutoff
            && x.ProtectedRequest != ""
            && (x.State == (int)SourceControlOperationState.Pushed
                || x.State == (int)SourceControlOperationState.Succeeded || x.State == (int)SourceControlOperationState.Conflict
                || x.State == (int)SourceControlOperationState.Failed || x.State == (int)SourceControlOperationState.Cancelled))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProtectedRequest, ""), cancellationToken);
        return count;
    }

    private string Protect(SourceControlContext context, Guid repositoryId, byte[] request) =>
        Convert.ToBase64String(protection.CreateProtector("VertexBPMN.SourceControl.Request.v1",
            context.TenantId, repositoryId.ToString("N"), context.ActorId).Protect(request));
}
