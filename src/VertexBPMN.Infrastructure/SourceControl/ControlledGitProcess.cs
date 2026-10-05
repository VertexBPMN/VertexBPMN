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
        await using var channel = new GitCredentialChannel(remote, lease, deadline.Token);
        var configuration = new Dictionary<string, string>
        {
            ["credential.helper"] = $"'{helper}' {channel.Name}",
            ["http.curloptResolve"] = $"{remote.IdnHost}:443:{string.Join(',', addresses.Select(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{x}]" : x.ToString()))}"
        };
        await RunAsync(workspace, ["--git-dir=" + Path.Combine(workspace.Directory, "repository.git"),
            "fetch", "--no-recurse-submodules", "--no-tags", "--depth=1", "--", remote.AbsoluteUri,
            $"refs/heads/{branch}:refs/heads/vertex-source"], configuration, options.Value.Limits.WriteTimeout, deadline.Token);
    }

    private async Task<byte[]> RunAsync(GitWorkspace workspace, IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? extra, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executable = options.Value.GitExecutablePath;
        if (!options.Value.Enabled || string.IsNullOrEmpty(executable) || !Path.IsPathFullyQualified(executable)
            || !File.Exists(executable)) throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable);
        _ = SourceControlWorkspace.MeasureBytes(workspace.Directory, options.Value.Limits.MaxRepositoryBytes);
        var start = new ProcessStartInfo(executable) { WorkingDirectory = workspace.Directory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
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
        var config = new List<KeyValuePair<string, string>>
        {
            new("credential.helper", ""), new("credential.useHttpPath", "true"),
            new("core.hooksPath", Path.Combine(workspace.ControlDirectory, "hooks").Replace('\\', '/')),
            new("core.fsmonitor", "false"), new("core.pager", ""), new("protocol.allow", "never"),
            new("protocol.https.allow", "always"), new("http.sslVerify", "true"), new("http.followRedirects", "false"),
            new("http.proxy", ""), new("submodule.recurse", "false"), new("fetch.recurseSubmodules", "false"),
            new("transfer.fsckObjects", "true"), new("fetch.fsckObjects", "true")
        };
        if (extra is not null) config.AddRange(extra);
        start.Environment["GIT_CONFIG_COUNT"] = config.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < config.Count; i++)
        {
            start.Environment[$"GIT_CONFIG_KEY_{i}"] = config[i].Key;
            start.Environment[$"GIT_CONFIG_VALUE_{i}"] = config[i].Value;
        }
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stop.CancelAfter(timeout);
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable);
            var output = ReadBoundedAsync(process.StandardOutput.BaseStream, options.Value.Limits.MaxDiffBytes, stop.Token);
            var errors = ReadBoundedAsync(process.StandardError.BaseStream, 64 * 1024, stop.Token);
            var completed = process.WaitForExitAsync(stop.Token);
            while (!completed.IsCompleted)
            {
                _ = SourceControlWorkspace.MeasureBytes(workspace.Directory, options.Value.Limits.MaxRepositoryBytes);
                await Task.WhenAny(completed, Task.Delay(50, stop.Token));
                stop.Token.ThrowIfCancellationRequested();
                if (output.IsFaulted || errors.IsFaulted) throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
            }
            await completed;
            var bytes = await output;
            _ = await errors; // Never include stderr in diagnostics or public results.
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
