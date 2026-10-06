using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record GitWorkspace(string Directory, string ControlDirectory, Guid OperationId, long Fence);

/// <summary>Only dedicated private roots and signed, job-owned directories are created/deleted.</summary>
internal sealed class SourceControlWorkspace(BpmnDbContext db, IDataProtectionProvider protection, IOptions<SourceControlOptions> options)
{
    private static void NoLinks(string path)
    {
        for (var item = new DirectoryInfo(path); item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0) Reject();
    }

    private string Root()
    {
        var configured = options.Value.WorkspaceRoot;
        if (!options.Value.Enabled || string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured)) Reject();
        var root = Path.GetFullPath(configured!);
        if (root.TrimEnd(Path.DirectorySeparatorChar) == Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar)) Reject();
        NoLinks(root);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()
            && !File.Exists(Path.Combine(root, ".vertex-root"))) Reject();
        if (!Directory.Exists(root))
        {
            if (OperatingSystem.IsWindows())
            {
                var security = new DirectorySecurity();
                security.SetAccessRuleProtection(true, false);
                var user = WindowsIdentity.GetCurrent().User!;
                security.SetOwner(user);
                security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                new DirectoryInfo(root).Create(security);
            }
            else Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        if (OperatingSystem.IsWindows())
        {
            var user = WindowsIdentity.GetCurrent().User!;
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            foreach (FileSystemAccessRule rule in new DirectoryInfo(root).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)))
                if (rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference != user
                    && rule.IdentityReference != system && rule.IdentityReference != admins) Reject();
        }
        else if ((File.GetUnixFileMode(root) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0) Reject();
        var marker = Path.Combine(root, ".vertex-root");
        var protector = protection.CreateProtector("VertexBPMN.SourceControl.WorkspaceRoot.v1", root);
        if (!File.Exists(marker))
        {
            using var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(file);
            writer.Write(protector.Protect("VertexBPMN"));
        }
        else
        {
            if ((File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0) Reject();
            try { if (protector.Unprotect(File.ReadAllText(marker)) != "VertexBPMN") Reject(); }
            catch { Reject(); }
        }
        return root;
    }

    internal async Task<GitWorkspace> CreateAsync(SourceControlContext context, Guid id, string worker, long fence,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var owns = await db.SourceControlOperations.AsNoTracking().AnyAsync(x => x.Id == id
            && x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.LeaseOwner == worker
            && x.Fence == fence && x.LeaseUntilUtcTicks > now, cancellationToken);
        if (!owns) throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        var root = Root();
        await using var reservation = await LockRootAsync(root, cancellationToken);
        var path = Path.Combine(root, $"{id:N}-{fence}");
        NoLinks(path);
        if (Directory.Exists(path)) Reject(); // Never reuse an unverified/partial clone.
        var existingBytes = MeasureBytes(root, options.Value.Limits.MaxWorkspaceBytesTotal);
        var reservedBytes = checked((Directory.EnumerateDirectories(root).LongCount() + 1)
            * options.Value.Limits.MaxRepositoryBytes);
        if (reservedBytes > options.Value.Limits.MaxWorkspaceBytesTotal
            || existingBytes + options.Value.Limits.MaxRepositoryBytes > options.Value.Limits.MaxWorkspaceBytesTotal)
            throw new SourceControlSecurityException(SourceControlErrorCode.QuotaExceeded);
        Directory.CreateDirectory(path);
        var control = Path.Combine(path, "control");
        Directory.CreateDirectory(control);
        Directory.CreateDirectory(Path.Combine(control, "hooks"));
        File.WriteAllText(Path.Combine(path, ".vertex-owner"), protection.CreateProtector(
            "VertexBPMN.SourceControl.WorkspaceOwner.v1", root).Protect($"{context.TenantId}|{context.ActorId}|{id:N}|{fence}"));
        return new(path, control, id, fence);
    }

    internal async Task CleanupAsync(SourceControlContext context, Guid id, long fence, CancellationToken cancellationToken)
    {
        var root = Root();
        await using var reservation = await LockRootAsync(root, cancellationToken);
        var path = Path.Combine(root, $"{id:N}-{fence}");
        NoLinks(path);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        if (await db.SourceControlOperations.AnyAsync(x => x.Id == id && x.TenantId == context.TenantId
            && x.LeaseUntilUtcTicks > now, cancellationToken))
            throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
        if (!Directory.Exists(path)) return;
        var marker = Path.Combine(path, ".vertex-owner");
        if (!File.Exists(marker) || (File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0) Reject();
        try
        {
            var owner = protection.CreateProtector("VertexBPMN.SourceControl.WorkspaceOwner.v1", root).Unprotect(File.ReadAllText(marker));
            if (owner != $"{context.TenantId}|{context.ActorId}|{id:N}|{fence}") Reject();
        }
        catch { Reject(); }
        // Enumerate/validate entire tree first. Private ACLs exclude untrusted writers.
        _ = MeasureBytes(path, long.MaxValue);
        // Git's immutable object files may be read-only on Windows. Only change
        // attributes after validating ownership, lease and every path in the tree.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
        }
        Directory.Delete(path, recursive: true);
    }

    /// <summary>Retain unknown effects and unpublished local commits; delete only verified owned terminal workspaces.</summary>
    internal async Task<int> PruneCompletedAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Disabled/unused integration must not create a directory or require Git.
        if (!options.Value.Enabled || !Directory.Exists(options.Value.WorkspaceRoot)) return 0;
        var root = Root();
        var cutoff = now.Subtract(options.Value.Limits.CompletedJobRetention).UtcTicks;
        var count = 0;
        foreach (var path in Directory.EnumerateDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parts = Path.GetFileName(path).Split('-');
            if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var id)
                || !long.TryParse(parts[1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var fence) || fence <= 0)
                continue; // No guessing ownership of unknown directories.
            if (!string.Equals(Path.GetFileName(path), $"{id:N}-{fence}", StringComparison.Ordinal)) continue;
            var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
                && x.UpdatedUtcTicks <= cutoff && x.Fence >= fence
                && (!x.LeaseUntilUtcTicks.HasValue || x.LeaseUntilUtcTicks <= now.UtcTicks)
                && (x.State == (int)SourceControlOperationState.Pushed
                    || x.State == (int)SourceControlOperationState.Succeeded
                    || x.State == (int)SourceControlOperationState.Conflict
                    || x.State == (int)SourceControlOperationState.Failed
                    || x.State == (int)SourceControlOperationState.Cancelled), cancellationToken);
            if (row is null) continue;
            await CleanupAsync(new(row.TenantId, row.ActorId), id, fence, cancellationToken);
            if (++count == 100) break;
        }
        return count;
    }

    internal static long MeasureBytes(string root, long limit)
    {
        NoLinks(root);
        long bytes = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(directory); }
            catch (DirectoryNotFoundException) when (directory != root) { continue; }
            foreach (var entry in entries)
            {
                try
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) Reject();
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    else bytes = checked(bytes + new FileInfo(entry).Length);
                    if (bytes > limit) throw new SourceControlSecurityException(SourceControlErrorCode.QuotaExceeded);
                }
                // Git atomically renames/deletes its own locks/temp objects while we measure.
                // Do not turn normal disappearance into GitUnavailable; security and access errors still fail closed.
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
        return bytes;
    }

    private static async Task<FileStream> LockRootAsync(string root, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var path = Path.Combine(root, ".vertex-quota-lock");
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) Reject();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(50, deadline.Token); }
        }
    }

    private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
}
