using System.Text;

namespace VertexBPMN.SourceControl.Security;

internal static class BoundedProtocol
{
    internal static async Task<string?> ReadLineAsync(TextReader reader, int limit, CancellationToken cancellationToken)
    {
        var result = new StringBuilder(Math.Min(limit, 256));
        var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) != 0)
        {
            if (buffer[0] == '\n') return result.ToString().TrimEnd('\r');
            if (result.Length >= limit) throw new InvalidDataException("Protocol field exceeds limit.");
            result.Append(buffer[0]);
        }
        return result.Length == 0 ? null : result.ToString();
    }
}
