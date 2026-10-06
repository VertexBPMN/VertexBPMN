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

    // Explicitly isolated acceptance adapter, not a configurable production SSRF bypass.
    internal Task FetchLocalAcceptanceAsync(GitWorkspace workspace, Uri remote, string branch, GitHubTokenLease lease,
        string certificateAuthorityFile, CancellationToken cancellationToken)
    {
        if (!remote.IsAbsoluteUri || remote.Scheme != "https" || remote.Host != "localhost" || remote.Port == 443
            || remote.UserInfo.Length != 0 || remote.Query.Length != 0 || remote.Fragment.Length != 0
            || !Path.IsPathFullyQualified(certificateAuthorityFile) || !File.Exists(certificateAuthorityFile))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        SourceControlInputPolicy.ValidateBranch(branch);
        return FetchCoreAsync(workspace, remote, branch, lease, [IPAddress.Loopback], certificateAuthorityFile, cancellationToken);
    }

    private async Task FetchCoreAsync(GitWorkspace workspace, Uri remote, string branch, GitHubTokenLease lease,
        IPAddress[] addresses, string? certificateAuthorityFile, CancellationToken cancellationToken)
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
        await RunAsync(workspace, ["--git-dir=" + Path.Combine(workspace.Directory, "repository.git"),
            "fetch", "--no-recurse-submodules", "--no-tags", "--depth=1", "--", remote.AbsoluteUri,
            $"refs/heads/{branch}:refs/heads/vertex-source"], configuration, options.Value.Limits.WriteTimeout, cancellationToken);
    }

    // Builds an immutable object only; publication still requires a fenced worker and ref CAS.
    internal async Task<GitCommitId> BuildCommitAsync(GitWorkspace workspace, RepositoryBinding binding,
        CommitCommand command, DateTimeOffset acceptedAt, CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty || command.SessionId == Guid.Empty
            || command.WorkBranch != SourceControlInputPolicy.WorkBranch(command.SessionId)
            || command.WorkBranch == binding.DefaultBranch || command.WorkBranch == binding.ReleaseBranch
            || string.IsNullOrWhiteSpace(command.Message) || command.Message.Any(char.IsControl)
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

    private async Task<byte[]> RunAsync(GitWorkspace workspace, IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? extra, TimeSpan timeout, CancellationToken cancellationToken,
        byte[]? input = null, IReadOnlyDictionary<string, string>? trustedEnvironment = null)
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
            var output = ReadBoundedAsync(process.StandardOutput.BaseStream, options.Value.Limits.MaxDiffBytes, stop.Token);
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
