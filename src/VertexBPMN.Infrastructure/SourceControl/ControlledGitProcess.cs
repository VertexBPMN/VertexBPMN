using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Fixed commands only. Bare repositories avoid untrusted checkout filters and hooks.</summary>
internal sealed class ControlledGitProcess(IOptions<SourceControlOptions> options)
{
    internal async Task<byte[]> VersionAsync(GitWorkspace workspace, CancellationToken cancellationToken)
    {
        var bytes = await RunAsync(workspace, ["--version"], null, options.Value.Limits.ReadTimeout, cancellationToken);
        var match = System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.UTF8.GetString(bytes),
            @"^git version (\d+)\.(\d+)\.(\d+)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success || new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value)) < new Version(2, 56, 0))
            throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable);
        return bytes;
    }

    internal async Task<byte[]> InitializeAsync(GitWorkspace workspace, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(workspace.Directory, "repository.git")))
            throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
        await VersionAsync(workspace, cancellationToken);
        return await RunAsync(workspace, ["init", "--bare", "--template=", Path.Combine(workspace.Directory, "repository.git")],
            null, options.Value.Limits.ReadTimeout, cancellationToken);
    }

    internal async Task FetchAsync(GitWorkspace workspace, Uri remote, string branch, GitHubTokenLease lease,
        CancellationToken cancellationToken)
    {
        SourceControlHttps.ValidateTarget(remote, options.Value.AllowedHosts);
        SourceControlInputPolicy.ValidateBranch(branch);
        await VersionAsync(workspace, cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Value.Limits.WriteTimeout);
        var addresses = await Dns.GetHostAddressesAsync(remote.IdnHost, deadline.Token);
        if (addresses.Length == 0 || addresses.Any(x => !SourceControlHttps.IsPublicAddress(x)))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        await FetchCoreAsync(workspace, remote, branch, lease, addresses, null, deadline.Token);
    }

    internal async Task FetchRevisionAsync(GitWorkspace workspace, Uri remote, GitCommitId revision,
        GitHubTokenLease lease, CancellationToken cancellationToken)
    {
        SourceControlHttps.ValidateTarget(remote, options.Value.AllowedHosts);
        await VersionAsync(workspace, cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Value.Limits.WriteTimeout);
        var addresses = await Dns.GetHostAddressesAsync(remote.IdnHost, deadline.Token);
        if (addresses.Length == 0 || addresses.Any(x => !SourceControlHttps.IsPublicAddress(x)))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        await FetchCoreAsync(workspace, remote, "", lease, addresses, null, deadline.Token, revision);
    }

    // Explicitly isolated acceptance adapter, not a configurable production SSRF bypass.
    internal Task FetchLocalAcceptanceAsync(GitWorkspace workspace, Uri remote, string branch, GitHubTokenLease lease,
        string certificateAuthorityFile, CancellationToken cancellationToken, GitCommitId? revision = null)
    {
        if (!remote.IsAbsoluteUri || remote.Scheme != "https" || remote.Host != "localhost" || remote.Port == 443
            || remote.UserInfo.Length != 0 || remote.Query.Length != 0 || remote.Fragment.Length != 0
            || !Path.IsPathFullyQualified(certificateAuthorityFile) || !File.Exists(certificateAuthorityFile))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        SourceControlInputPolicy.ValidateBranch(branch);
        return FetchCoreAsync(workspace, remote, branch, lease, [IPAddress.Loopback], certificateAuthorityFile, cancellationToken, revision);
    }

    private async Task FetchCoreAsync(GitWorkspace workspace, Uri remote, string branch, GitHubTokenLease lease,
        IPAddress[] addresses, string? certificateAuthorityFile, CancellationToken cancellationToken, GitCommitId? revision = null,
        int depth = 1)
    {
        await RunAuthenticatedRemoteAsync(workspace, remote, lease, addresses, certificateAuthorityFile,
            ["fetch", "--no-recurse-submodules", "--no-tags", "--depth=" + depth.ToString(System.Globalization.CultureInfo.InvariantCulture), "--", remote.AbsoluteUri,
                $"{revision?.Value ?? "refs/heads/" + branch}:refs/heads/vertex-source"],
            options.Value.Limits.WriteTimeout, cancellationToken);
    }

    internal async Task FetchHistoryAsync(GitWorkspace workspace, Uri remote, GitCommitId revision,
        GitHubTokenLease lease, CancellationToken cancellationToken)
    {
        SourceControlHttps.ValidateTarget(remote, options.Value.AllowedHosts);
        GitModelHistory.ValidateLimit(options.Value.Limits);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Value.Limits.WriteTimeout);
        try
        {
            await VersionAsync(workspace, deadline.Token);
            var addresses = await Dns.GetHostAddressesAsync(remote.IdnHost, deadline.Token);
            if (addresses.Length == 0 || addresses.Any(address => !SourceControlHttps.IsPublicAddress(address)))
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
            }
            await FetchCoreAsync(workspace, remote, "", lease, addresses, null, deadline.Token,
                revision, options.Value.Limits.MaxHistoryCommits + 1);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
        }
    }

    internal Task FetchHistoryLocalAcceptanceAsync(GitWorkspace workspace, Uri remote, GitCommitId revision,
        GitHubTokenLease lease, string certificateAuthorityFile, CancellationToken cancellationToken)
    {
        GitModelHistory.ValidateLimit(options.Value.Limits);
        if (!remote.IsAbsoluteUri || remote.Scheme != "https" || remote.Host != "localhost" || remote.Port == 443
            || remote.UserInfo.Length != 0 || remote.Query.Length != 0 || remote.Fragment.Length != 0
            || !Path.IsPathFullyQualified(certificateAuthorityFile) || !File.Exists(certificateAuthorityFile))
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        }
        return FetchCoreAsync(workspace, remote, "", lease, [IPAddress.Loopback], certificateAuthorityFile,
            cancellationToken, revision, options.Value.Limits.MaxHistoryCommits + 1);
    }

    internal async Task<SourceControlPage<string>> ListRemoteBranchesAsync(GitWorkspace workspace,
        RepositoryBinding binding, GitHubTokenLease lease, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        SourceControlHttps.ValidateTarget(binding.Remote, options.Value.AllowedHosts);
        GitRemoteReferences.ValidatePage(pageSize, cursor, options.Value.Limits);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Value.Limits.ReadTimeout);
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(binding.Remote.IdnHost, deadline.Token);
            if (addresses.Length == 0 || addresses.Any(address => !SourceControlHttps.IsPublicAddress(address)))
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
            }
            return await ListRemoteBranchesCoreAsync(workspace, binding, lease, pageSize, cursor, addresses, null, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
        }
    }

    internal Task<SourceControlPage<string>> ListRemoteBranchesLocalAcceptanceAsync(GitWorkspace workspace,
        RepositoryBinding binding, GitHubTokenLease lease, int pageSize, string? cursor,
        string certificateAuthorityFile, CancellationToken cancellationToken)
    {
        if (binding.Remote.Scheme != "https" || binding.Remote.Host != "localhost" || binding.Remote.Port == 443
            || binding.Remote.UserInfo.Length != 0 || binding.Remote.Query.Length != 0 || binding.Remote.Fragment.Length != 0
            || !Path.IsPathFullyQualified(certificateAuthorityFile) || !File.Exists(certificateAuthorityFile))
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        }
        return ListRemoteBranchesCoreAsync(workspace, binding, lease, pageSize, cursor, [IPAddress.Loopback], certificateAuthorityFile, cancellationToken);
    }

    private async Task<SourceControlPage<string>> ListRemoteBranchesCoreAsync(GitWorkspace workspace,
        RepositoryBinding binding, GitHubTokenLease lease, int pageSize, string? cursor, IPAddress[] addresses,
        string? certificateAuthorityFile, CancellationToken cancellationToken)
    {
        var limits = options.Value.Limits;
        GitRemoteReferences.ValidatePage(pageSize, cursor, limits);
        await VersionAsync(workspace, cancellationToken);
        var output = await RunAuthenticatedRemoteAsync(workspace, binding.Remote, lease, addresses, certificateAuthorityFile,
            ["ls-remote", "--quiet", "--branches", "--refs", "--", binding.Remote.AbsoluteUri], limits.ReadTimeout, cancellationToken);
        return GitRemoteReferences.ReadPage(output, binding, pageSize, cursor, limits, cancellationToken);
    }

    private async Task<byte[]> RunAuthenticatedRemoteAsync(GitWorkspace workspace, Uri remote, GitHubTokenLease lease,
        IPAddress[] addresses, string? certificateAuthorityFile, IReadOnlyList<string> command,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        var helper = options.Value.AuthHelperExecutablePath ?? Path.Combine(AppContext.BaseDirectory,
            "source-control-auth", "VertexBPMN.SourceControl.AuthHelper" + (OperatingSystem.IsWindows() ? ".exe" : ""));
        if (string.IsNullOrEmpty(helper) || !Path.IsPathFullyQualified(helper) || !File.Exists(helper)
            || helper.Any(c => c is '\r' or '\n' or '\'' or '"' or '$' or '`' or '\\'))
        {
            // Git's credential helper is intentionally a trusted fixed executable command.
            // Normalize Windows separators before applying the shell quoting boundary.
            helper = helper?.Replace('\\', '/');
            if (string.IsNullOrEmpty(helper) || !Path.IsPathFullyQualified(helper) || !File.Exists(helper)
                || helper.Any(c => c is '\r' or '\n' or '\'' or '"' or '$' or '`'))
                throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable);
        }
        await using var channel = new GitCredentialChannel(remote, lease, cancellationToken);
        var configuration = new Dictionary<string, string>
        {
            ["credential.helper"] = $"!'{helper}' {channel.Name}",
            ["http.curloptResolve"] = $"{remote.IdnHost}:{remote.Port}:{string.Join(',', addresses.Select(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{x}]" : x.ToString()))}"
        };
        if (certificateAuthorityFile is not null)
        {
            configuration["http.sslCAInfo"] = certificateAuthorityFile.Replace('\\', '/');
            configuration["http.schannelUseSSLCAInfo"] = "true";
            if (OperatingSystem.IsWindows()) configuration["http.sslBackend"] = "schannel";
        }
        return await RunAsync(workspace, ["--git-dir=" + Path.Combine(workspace.Directory, "repository.git"), .. command],
            configuration, timeout, cancellationToken);
    }

    // Builds an immutable object only; publication still requires a fenced worker and ref CAS.
    internal async Task<GitCommitId> BuildCommitAsync(GitWorkspace workspace, RepositoryBinding binding,
        CommitCommand command, DateTimeOffset acceptedAt, CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty || command.SessionId == Guid.Empty
            || command.WorkBranch != SourceControlInputPolicy.WorkBranch(command.SessionId)
            || command.WorkBranch == binding.DefaultBranch || command.WorkBranch == binding.ReleaseBranch
            || string.IsNullOrWhiteSpace(command.Message) || command.Message.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))
            || command.Message.Length > options.Value.Limits.MaxCommitMessageCharacters
            || command.Snapshots.Count == 0 || command.Snapshots.Count > options.Value.Limits.MaxCommitFiles)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        var snapshots = command.Snapshots.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
        if (snapshots.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshots.Length)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        foreach (var snapshot in snapshots) SourceControlInputPolicy.DemandSafeBpmn(snapshot, binding, options.Value.Limits);
        await VersionAsync(workspace, cancellationToken);
        var index = Path.Combine(workspace.ControlDirectory, "commit-index-" + Guid.NewGuid().ToString("N"));
        var environment = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = index,
            ["GIT_AUTHOR_NAME"] = "VertexBPMN", ["GIT_COMMITTER_NAME"] = "VertexBPMN",
            ["GIT_AUTHOR_EMAIL"] = "source-control@vertexbpmn.invalid", ["GIT_COMMITTER_EMAIL"] = "source-control@vertexbpmn.invalid",
            ["GIT_AUTHOR_DATE"] = $"@{acceptedAt.ToUnixTimeSeconds()} +0000",
            ["GIT_COMMITTER_DATE"] = $"@{acceptedAt.ToUnixTimeSeconds()} +0000" };
        var prefix = "--git-dir=" + Path.Combine(workspace.Directory, "repository.git");
        Task<byte[]> Run(string[] args, byte[]? input = null) => RunAsync(workspace, [prefix, .. args],
            null, options.Value.Limits.WriteTimeout, cancellationToken, input, environment);
        string Text(byte[] bytes) => System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\r', '\n');
        try
        {
            var entries = System.Text.Encoding.UTF8.GetString(await Run(["ls-tree", "-r", "-t", "-z", command.BaseCommit.Value]))
                .Split('\0', StringSplitOptions.RemoveEmptyEntries);
            var modes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var tab = entry.IndexOf('\t');
                if (tab < 0) throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
                modes.Add(entry[(tab + 1)..], entry[..6]);
            }
            foreach (var snapshot in snapshots)
                foreach (var entry in modes)
                    if ((entry.Key.Equals(snapshot.Path, StringComparison.OrdinalIgnoreCase)
                            && (entry.Key != snapshot.Path || entry.Value is not ("100644" or "100755")))
                        || entry.Key.StartsWith(snapshot.Path + "/", StringComparison.OrdinalIgnoreCase)
                        || (snapshot.Path.StartsWith(entry.Key + "/", StringComparison.OrdinalIgnoreCase)
                            && (entry.Value != "040000" || !snapshot.Path.StartsWith(entry.Key + "/", StringComparison.Ordinal))))
                        throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            await Run(["read-tree", command.BaseCommit.Value]);
            foreach (var snapshot in snapshots)
            {
                var blob = new GitCommitId(Text(await Run(["hash-object", "-w", "--no-filters", "--stdin"], snapshot.CopyContent())));
                await Run(["update-index", "--add", "--cacheinfo", modes.GetValueOrDefault(snapshot.Path, "100644"), blob.Value, snapshot.Path]);
            }
            var tree = new GitCommitId(Text(await Run(["write-tree"])));
            var message = System.Text.Encoding.UTF8.GetBytes(command.Message + "\n\nVertex-Operation: " + command.OperationId.ToString("N") + "\n");
            return new GitCommitId(Text(await Run(["commit-tree", "--no-gpg-sign", tree.Value, "-p", command.BaseCommit.Value], message)));
        }
        finally
        {
            File.Delete(index);
            File.Delete(index + ".lock");
        }
    }

    internal async Task<GitCommitId?> ReadLocalBranchAsync(GitWorkspace workspace, string branch, CancellationToken cancellationToken)
    {
        SourceControlInputPolicy.ValidateBranch(branch);
        var reference = "refs/heads/" + branch;
        var bytes = await RunAsync(workspace, ["--git-dir=" + Path.Combine(workspace.Directory, "repository.git"),
            "for-each-ref", "--format=%(refname) %(objectname) %(symref)", "--", reference], null,
            options.Value.Limits.ReadTimeout, cancellationToken);
        var exact = System.Text.Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.TrimEnd('\r')).Where(x => x.StartsWith(reference + " ", StringComparison.Ordinal)).ToArray();
        if (exact.Length > 1) throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        if (exact.Length == 0) return null;
        var value = exact[0][(reference.Length + 1)..].Split(' ', 2);
        if (value.Length != 2 || value[1].Length != 0)
            throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        return new GitCommitId(value[0]);
    }

    /// <summary>Only publishes a new private work ref; never overwrites a differing local head.</summary>
    internal async Task PublishLocalCommitAsync(GitWorkspace workspace, string branch, GitCommitId commit,
        CancellationToken cancellationToken)
    {
        SourceControlInputPolicy.ValidateBranch(branch);
        if (!branch.StartsWith("vertex/", StringComparison.Ordinal))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        var current = await ReadLocalBranchAsync(workspace, branch, cancellationToken);
        if (current == commit) return; // Receipt reconciliation, no second ref effect.
        if (current is not null) throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
        var prefix = "--git-dir=" + Path.Combine(workspace.Directory, "repository.git");
        await RunAsync(workspace, [prefix, "rev-parse", "--verify", commit.Value + "^{commit}"], null,
            options.Value.Limits.ReadTimeout, cancellationToken);
        try
        {
            await RunAsync(workspace, [prefix, "update-ref", "--no-deref", "refs/heads/" + branch, commit.Value,
                new string('0', commit.Value.Length)], null, options.Value.Limits.ReadTimeout, cancellationToken);
        }
        catch (SourceControlSecurityException exception) when (exception.Code == SourceControlErrorCode.ProviderUnavailable)
        {
            current = await ReadLocalBranchAsync(workspace, branch, cancellationToken);
            if (current == commit) return;
            if (current is not null) throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
            throw;
        }
    }

    internal async Task<ModelSnapshot> ReadModelAsync(GitWorkspace workspace, RepositoryBinding binding,
        FileReadRequest request, Guid documentGeneration, CancellationToken cancellationToken)
    {
        SourceControlInputPolicy.DemandModelPath(request.Path, binding.ModelRoots);
        if (documentGeneration == Guid.Empty)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        var limits = options.Value.Limits;
        if (limits.MaxModelBytes <= 0)
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        var prefix = "--git-dir=" + Path.Combine(workspace.Directory, "repository.git");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(limits.ReadTimeout);
        try
        {
            await VersionAsync(workspace, deadline.Token);
            var resolved = await RunAsync(workspace, [prefix, "rev-parse", "--verify", request.Commit.Value + "^{commit}"],
                null, limits.ReadTimeout, deadline.Token);
            if (System.Text.Encoding.UTF8.GetString(resolved).Trim() != request.Commit.Value)
                throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
            var tree = await RunAsync(workspace, [prefix, "ls-tree", "-r", "-t", "-z", request.Commit.Value],
                null, limits.ReadTimeout, deadline.Token);
            var objectId = FindModelBlob(tree, request.Path);
            var size = await RunAsync(workspace, [prefix, "cat-file", "-s", objectId], null, limits.ReadTimeout, deadline.Token);
            if (!long.TryParse(System.Text.Encoding.UTF8.GetString(size).Trim(),
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var length)
                || length <= 0 || length > limits.MaxModelBytes)
                throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
            var bytes = await RunAsync(workspace, [prefix, "cat-file", "blob", objectId], null,
                limits.ReadTimeout, deadline.Token, outputLimit: limits.MaxModelBytes);
            var snapshot = new ModelSnapshot(request.Path, SourceModelKind.Bpmn, documentGeneration, 0, bytes);
            SourceControlInputPolicy.DemandSafeBpmn(snapshot, binding, limits);
            return snapshot;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
        }
    }

    internal async Task<SourceControlPage<RepositoryFile>> ListModelsAsync(GitWorkspace workspace,
        RepositoryBinding binding, TreeRequest request, CancellationToken cancellationToken)
    {
        SourceControlInputPolicy.ValidateRelativePath(request.Root);
        foreach (var root in binding.ModelRoots)
        {
            SourceControlInputPolicy.ValidateRelativePath(root);
        }
        var limits = options.Value.Limits;
        if (!binding.ModelRoots.Any(root => request.Root == root || request.Root.StartsWith(root + "/", StringComparison.Ordinal))
            || request.PageSize <= 0 || request.PageSize > limits.MaxPageSize)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(limits.ReadTimeout);
        var prefix = "--git-dir=" + Path.Combine(workspace.Directory, "repository.git");
        try
        {
            await VersionAsync(workspace, deadline.Token);
            var resolved = await RunAsync(workspace, [prefix, "rev-parse", "--verify", request.Commit.Value + "^{commit}"],
                null, limits.ReadTimeout, deadline.Token);
            if (System.Text.Encoding.UTF8.GetString(resolved).Trim() != request.Commit.Value)
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
            }
            var tree = await RunAsync(workspace, [prefix, "ls-tree", "-r", "-t", "-l", "-z", request.Commit.Value],
                null, limits.ReadTimeout, deadline.Token);
            return GitModelTree.ReadPage(tree, binding, request, limits, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
        }
    }

    internal async Task<SourceControlPage<CommitSummary>> ReadModelHistoryAsync(GitWorkspace workspace,
        RepositoryBinding binding, HistoryRequest request, CancellationToken cancellationToken)
    {
        var limits = options.Value.Limits;
        GitModelHistory.ValidateLimit(limits);
        GitRemoteReferences.ValidatePage(request.PageSize, request.Cursor, limits);
        SourceControlInputPolicy.DemandModelPath(request.Path, binding.ModelRoots);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(limits.ReadTimeout);
        try
        {
            // Reuse pinned commit/path/mode/content guards before exposing any history.
            await ReadModelAsync(workspace, binding, new(request.Commit, request.Path), Guid.NewGuid(), deadline.Token);
            var prefix = "--git-dir=" + Path.Combine(workspace.Directory, "repository.git");
            var shallow = await RunAsync(workspace, [prefix, "rev-parse", "--is-shallow-repository"],
                null, limits.ReadTimeout, deadline.Token);
            if (System.Text.Encoding.UTF8.GetString(shallow).Trim() != "false")
            {
                // A clipped ancestor graph is not a complete history, even for paths whose
                // visible commits happen to fit in one page. The caller must fetch the bounded graph.
                throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
            }
            var countLimit = (limits.MaxHistoryCommits + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var ancestors = await RunAsync(workspace, [prefix, "rev-list", "--max-count=" + countLimit, request.Commit.Value, "--"],
                null, limits.ReadTimeout, deadline.Token);
            if (System.Text.Encoding.UTF8.GetString(ancestors).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length > limits.MaxHistoryCommits)
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
            }
            var history = await RunAsync(workspace, [prefix, "--literal-pathspecs", "log", "--no-decorate", "--no-show-signature",
                "--no-notes", "--no-color", "--encoding=UTF-8", "--topo-order", "--full-history", "--max-count=" + countLimit,
                "-z", "--format=tformat:%H%x00%ct%x00%s", request.Commit.Value, "--", request.Path],
                null, limits.ReadTimeout, deadline.Token);
            return GitModelHistory.ReadPage(history, binding, request, limits, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
        }
    }

    internal async Task<ModelDiff> CompareModelAsync(GitWorkspace workspace, RepositoryBinding binding,
        DiffRequest request, CancellationToken cancellationToken)
    {
        var limits = options.Value.Limits;
        SourceControlInputPolicy.DemandSafeBpmn(request.Snapshot, binding, limits);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(limits.ReadTimeout);
        try
        {
            var basis = await ReadModelAsync(workspace, binding,
                new FileReadRequest(request.BaseCommit, request.Snapshot.Path), request.Snapshot.DocumentGeneration, deadline.Token);
            if (basis.CopyContent().AsSpan().SequenceEqual(request.Snapshot.CopyContent()))
            {
                return new ModelDiff(request.Snapshot.Path, string.Empty, false);
            }
            var prefix = "--git-dir=" + Path.Combine(workspace.Directory, "repository.git");
            // Only immutable blobs are written in this isolated workspace. No index, ref,
            // checkout, attributes, filters or runtime definition is changed by comparison.
            async Task<string> StoreBlobAsync(byte[] content)
            {
                var bytes = await RunAsync(workspace, [prefix, "hash-object", "-w", "--no-filters", "--stdin"],
                    null, limits.ReadTimeout, deadline.Token, content);
                return new GitCommitId(System.Text.Encoding.UTF8.GetString(bytes).Trim()).Value;
            }
            var before = await StoreBlobAsync(basis.CopyContent());
            var after = await StoreBlobAsync(request.Snapshot.CopyContent());
            var patch = await RunAsync(workspace, [prefix, "diff", "--no-ext-diff", "--no-textconv",
                "--no-color", "--no-renames", "--text", "--unified=3", before, after, "--"],
                null, limits.ReadTimeout, deadline.Token);
            // Oversized patches fail closed through RunAsync; never return a partial diff
            // whose omitted changes could be mistaken for a complete review.
            return new ModelDiff(request.Snapshot.Path, System.Text.Encoding.UTF8.GetString(patch), false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
        }
    }

    private static string FindModelBlob(byte[] tree, string requestedPath)
    {
        string? objectId = null;
        foreach (var entry in System.Text.Encoding.UTF8.GetString(tree).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = entry.IndexOf('\t');
            if (tab < 0)
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            }
            var path = entry[(tab + 1)..];
            var metadata = entry[..tab].Split(' ');
            if (path.Equals(requestedPath, StringComparison.OrdinalIgnoreCase))
            {
                if (path != requestedPath || objectId is not null || metadata.Length != 3
                    || metadata[0] is not ("100644" or "100755") || metadata[1] != "blob")
                {
                    throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
                }
                objectId = new GitCommitId(metadata[2]).Value;
            }
            else if (requestedPath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)
                && (metadata[0] != "040000" || !requestedPath.StartsWith(path + "/", StringComparison.Ordinal)))
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            }
        }
        return objectId ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
    }

    private async Task<byte[]> RunAsync(GitWorkspace workspace, IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? extra, TimeSpan timeout, CancellationToken cancellationToken,
        byte[]? input = null, IReadOnlyDictionary<string, string>? trustedEnvironment = null, int? outputLimit = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executable = options.Value.GitExecutablePath;
        if (!options.Value.Enabled || string.IsNullOrEmpty(executable) || !Path.IsPathFullyQualified(executable)
            || !File.Exists(executable)) throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable);
        _ = SourceControlWorkspace.MeasureBytes(workspace.Directory, options.Value.Limits.MaxRepositoryBytes);
        var start = new ProcessStartInfo(executable) { WorkingDirectory = workspace.Directory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = input is not null };
        start.Environment.Clear();
        // Windows process loading needs these OS paths, not arbitrary inherited settings.
        foreach (var name in new[] { "SystemRoot", "WINDIR" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["PATH"] = Path.GetDirectoryName(executable)!;
        start.Environment["HOME"] = workspace.ControlDirectory;
        start.Environment["XDG_CONFIG_HOME"] = workspace.ControlDirectory;
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        // Object replacements must not substitute content behind a pinned commit ID.
        start.Environment["GIT_NO_REPLACE_OBJECTS"] = "1";
        // Git for Windows rejects NUL as a config file. Use a known empty file
        // in the private control directory instead of an ambient user config.
        var globalConfig = Path.Combine(workspace.ControlDirectory, "empty-global.config");
        using (var empty = new FileStream(globalConfig, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read))
            if (empty.Length != 0) throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        start.Environment["GIT_CONFIG_GLOBAL"] = globalConfig;
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_ASKPASS"] = "";
        start.Environment["GCM_INTERACTIVE"] = "never";
        if (trustedEnvironment is not null)
            foreach (var pair in trustedEnvironment) start.Environment[pair.Key] = pair.Value;
        var config = new List<KeyValuePair<string, string>>
        {
            new("credential.helper", ""), new("credential.useHttpPath", "true"),
            new("core.hooksPath", Path.Combine(workspace.ControlDirectory, "hooks").Replace('\\', '/')),
            new("core.fsmonitor", "false"), new("core.pager", ""), new("protocol.allow", "never"),
            new("protocol.https.allow", "always"), new("http.sslVerify", "true"), new("http.followRedirects", "false"),
            new("http.schannelCheckRevoke", "true"), new("transfer.bundleURI", "false"),
            new("http.proxy", ""), new("submodule.recurse", "false"), new("fetch.recurseSubmodules", "false"),
            new("transfer.fsckObjects", "true"), new("fetch.fsckObjects", "true")
        };
        if (extra is not null) config.AddRange(extra);
        // Fixed non-secret command configuration must also reach Git's HTTPS subprocess.
        // Tokens stay exclusively in IPC; these arguments contain only policies and channel IDs.
        foreach (var setting in config)
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(setting.Key + "=" + setting.Value);
        }
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stop.CancelAfter(timeout);
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable);
            var output = ReadBoundedAsync(process.StandardOutput.BaseStream, outputLimit ?? options.Value.Limits.MaxDiffBytes, stop.Token);
            var errors = ReadBoundedAsync(process.StandardError.BaseStream, 64 * 1024, stop.Token);
            var completed = process.WaitForExitAsync(stop.Token);
            var supplied = input is null ? Task.CompletedTask : SupplyAsync(process.StandardInput.BaseStream, input, stop.Token);
            while (!completed.IsCompleted)
            {
                _ = SourceControlWorkspace.MeasureBytes(workspace.Directory, options.Value.Limits.MaxRepositoryBytes);
                await Task.WhenAny(completed, Task.Delay(50, stop.Token));
                stop.Token.ThrowIfCancellationRequested();
                if (output.IsFaulted || errors.IsFaulted) throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
                if (supplied.IsFaulted) await supplied;
            }
            await completed;
            await supplied;
            var bytes = await output;
            _ = await errors; // Never expose provider stderr; it may contain credentials.
            _ = SourceControlWorkspace.MeasureBytes(workspace.Directory, options.Value.Limits.MaxRepositoryBytes);
            if (process.ExitCode != 0) throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
            return bytes;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut); }
        catch (OperationCanceledException) { throw; }
        catch (SourceControlSecurityException) { throw; }
        catch { throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable); }
        finally
        {
            try { if (process.Id != 0 && !process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
            catch (InvalidOperationException) { }
        }
    }

    private static async Task SupplyAsync(Stream stream, byte[] input, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(input, cancellationToken);
        await stream.DisposeAsync();
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken cancellationToken)
    {
        using var result = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (result.Length + read > limit) throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
            result.Write(buffer, 0, read);
        }
        return result.ToArray();
    }
}
