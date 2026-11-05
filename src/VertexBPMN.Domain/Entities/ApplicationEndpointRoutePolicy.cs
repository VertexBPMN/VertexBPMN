using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Domain.Entities;

internal static class ApplicationEndpointRoutePolicy
{
    // Relative application namespace only. This is not an API route registration.
    internal static void Validate(ApplicationEndpointDefinition endpoint)
    {
        if (endpoint.Method is not ("GET" or "POST"))
        { throw new ApplicationValidationException("Endpoint method is not approved."); }
        var route = endpoint.Route;
        var parameters = endpoint.PathParameters;
        if (string.IsNullOrWhiteSpace(endpoint.Key) || parameters is null
            || route?.StartsWith("/endpoints/" + endpoint.Key, StringComparison.Ordinal) != true)
        { throw new ApplicationValidationException("Endpoint route is not approved."); }
        var segments = route.Split('/');
        if (segments.Length < 3 || segments[2] != endpoint.Key
            || !IsIdentifier(endpoint.Key) || segments.Skip(3).Any(segment =>
                !(IsIdentifier(segment) || IsParameter(segment))))
        { throw new ApplicationValidationException("Endpoint route is not approved."); }
        var declared = segments.Skip(3).Where(IsParameter).Select(segment => segment[1..^1]).ToArray();
        if (!declared.SequenceEqual(parameters, StringComparer.Ordinal)
            || declared.Distinct(StringComparer.Ordinal).Count() != declared.Length)
        { throw new ApplicationValidationException("Endpoint path parameters do not match the route."); }
    }

    private static bool IsParameter(string segment) => segment.Length > 2
        && segment[0] == '{' && segment[^1] == '}' && IsIdentifier(segment[1..^1]);
    private static bool IsIdentifier(string value) => value.Length > 0
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
