using System.Globalization;

namespace VertexBPMN.Infrastructure.Operational;

// Versioned position bound to a subscription. Not a credential: ownership is checked first.
public static class EventStreamCursor
{
    public static string Encode(Guid subscriptionId, long position) =>
        string.Create(CultureInfo.InvariantCulture, $"v1:{subscriptionId:N}:{position}");

    public static string Encode(Guid subscriptionId, long position, Guid? definitionId) =>
        definitionId is null ? Encode(subscriptionId, position) :
        string.Create(CultureInfo.InvariantCulture, $"v2:{subscriptionId:N}:{position}:{definitionId:N}");

    public static bool TryDecode(string value, Guid subscriptionId, Guid? definitionId, out long position)
    {
        if (definitionId is null)
        {
            return TryDecode(value, subscriptionId, out position);
        }
        position = 0;
        var parts = value.Split(':');
        return parts.Length == 4 && parts[0] == "v2"
            && Guid.TryParseExact(parts[3], "N", out var filter) && filter == definitionId
            && TryDecode($"v1:{parts[1]}:{parts[2]}", subscriptionId, out position);
    }

    public static bool TryDecode(string value, Guid subscriptionId, out long position)
    {
        position = 0;
        var parts = value.Split(':');
        return parts.Length == 3 && parts[0] == "v1"
            && Guid.TryParseExact(parts[1], "N", out var owner) && owner == subscriptionId
            && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out position)
            && position > 0;
    }
}
