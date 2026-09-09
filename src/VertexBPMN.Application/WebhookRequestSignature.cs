using System.Globalization;
using System.Text;

namespace VertexBPMN.Application;

/// <summary>Versioned webhook authentication envelope; timestamps are UTC Unix seconds.</summary>
public static class WebhookRequestSignature
{
    public static bool IsFresh(string? timestamp, string? deliveryId, DateTimeOffset now) =>
        !string.IsNullOrWhiteSpace(deliveryId) && deliveryId.Length <= 128
        && deliveryId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        && long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
        && seconds >= now.ToUnixTimeSeconds() - 300 && seconds <= now.ToUnixTimeSeconds() + 30;

    public static byte[] CreatePayload(string method, string path, string timestamp, string deliveryId, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes($"v1\n{method.ToUpperInvariant()}\n{path}\n{timestamp}\n{deliveryId}\n");
        var result = new byte[prefix.Length + body.Length];
        prefix.CopyTo(result, 0);
        body.CopyTo(result.AsSpan(prefix.Length));
        return result;
    }
}
