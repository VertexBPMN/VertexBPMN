using System.IO.Pipes;
using System.Text;
using VertexBPMN.SourceControl.Security;

// Fixed-purpose Git helper. No token in command line, environment or filesystem.
if (args.Length != 2 || !args[0].StartsWith("vertex-source-control-", StringComparison.Ordinal)
    || args[0].Length > 100 || args[0].Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return 2;
if (args[1] is "store" or "erase") return 0; // No credential persistence.
if (args[1] != "get") return 2;
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
    var fields = new Dictionary<string, string>(StringComparer.Ordinal);
    var total = 0;
    while (await BoundedProtocol.ReadLineAsync(Console.In, 4096, timeout.Token) is { Length: > 0 } line)
    {
        total += line.Length;
        if (total > 16 * 1024) return 2;
        var separator = line.IndexOf('=');
        if (separator <= 0 || !fields.TryAdd(line[..separator], line[(separator + 1)..])) return 2;
    }
    if (!fields.TryGetValue("protocol", out var protocol) || protocol != "https"
        || !fields.TryGetValue("host", out var host) || !fields.TryGetValue("path", out var path)) return 2;
    if (fields.ContainsKey("password") || fields.TryGetValue("username", out var suppliedUser) && suppliedUser != "x-access-token") return 2;
    using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    await pipe.ConnectAsync(timeout.Token);
    using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
    using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
    await writer.WriteLineAsync($"{protocol}\n{host}\n{path}".AsMemory(), timeout.Token);
    var username = await BoundedProtocol.ReadLineAsync(reader, 128, timeout.Token);
    var password = await BoundedProtocol.ReadLineAsync(reader, 16 * 1024, timeout.Token);
    if (username != "x-access-token" || string.IsNullOrEmpty(password) || password.Any(char.IsControl)) return 2;
    await Console.Out.WriteAsync($"username={username}\npassword={password}\n\n");
    return 0;
}
catch { return 2; } // Deliberately no secret-bearing exception/diagnostic output.
