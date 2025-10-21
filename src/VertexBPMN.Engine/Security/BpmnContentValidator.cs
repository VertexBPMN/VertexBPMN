using System.Text.RegularExpressions;
using VertexBPMN.Domain.Model.Security;

namespace VertexBPMN.Engine.Security;

/// <summary>
/// Content validation and sanitization for BPMN input.
/// Detects and prevents malicious content patterns.
/// </summary>
public sealed class BpmnContentValidator
{
    private static readonly Regex[] _maliciousPatterns = 
    {
        new(@"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"&lt;script&gt;\b[^<]*(?:(?!<\/script>)<[^<]*)*&lt;/script&gt;", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"data:.*base64", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"vbscript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled), // Event handlers
        new(@"expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), // CSS expressions
        new(@"<iframe\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<object\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<embed\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<!ENTITY", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"SYSTEM\s+['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"PUBLIC\s+['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    private static readonly HashSet<string> _suspiciousNamespaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "http://www.w3.org/1999/xhtml",
        "http://www.w3.org/2000/svg",
        "http://schemas.microsoft.com/expression/",
        "urn:oasis:names:tc:SAML:",
        "http://schemas.xmlsoap.org/ws/2005/05/identity"
    };

    /// <summary>
    /// Validates BPMN content for security threats.
    /// </summary>
    public ContentValidationResult ValidateContent(string xml)
    {
        var result = new ContentValidationResult();
        
        // 1. Check for malicious patterns
        foreach (var pattern in _maliciousPatterns)
        {
            var matches = pattern.Matches(xml);
            if (matches.Count > 0)
            {
                result.Threats.Add(new SecurityThreat
                {
                    Type = ThreatType.MaliciousContent,
                    Pattern = pattern.ToString(),
                    Occurrences = matches.Count,
                    FirstMatch = matches[0].Value.Substring(0, Math.Min(50, matches[0].Value.Length))
                });
            }
        }

        // 2. Check for suspicious namespaces
        foreach (var suspiciousNs in _suspiciousNamespaces)
        {
            if (xml.Contains(suspiciousNs, StringComparison.OrdinalIgnoreCase))
            {
                result.Threats.Add(new SecurityThreat
                {
                    Type = ThreatType.SuspiciousNamespace,
                    Pattern = suspiciousNs,
                    Occurrences = 1,
                    Description = "Non-BPMN namespace detected that could indicate malicious content"
                });
            }
        }

        // 3. Check for excessive CDATA usage (potential data exfiltration)
        var cdataMatches = Regex.Matches(xml, @"<!\[CDATA\[.*?\]\]>", RegexOptions.Singleline);
        if (cdataMatches.Count > 20 || cdataMatches.Sum(m => m.Length) > 50000)
        {
            result.Threats.Add(new SecurityThreat
            {
                Type = ThreatType.ExcessiveCDATA,
                Occurrences = cdataMatches.Count,
                Description = "Excessive CDATA usage detected - potential data hiding"
            });
        }

        // 4. Check for binary content indicators
        if (HasBinaryContent(xml))
        {
            result.Threats.Add(new SecurityThreat
            {
                Type = ThreatType.BinaryContent,
                Description = "Binary content detected in XML - potential malware"
            });
        }

        result.IsSecure = result.Threats.Count == 0;
        return result;
    }

    /// <summary>
    /// Sanitizes BPMN content by removing or neutralizing threats.
    /// </summary>
    public string SanitizeContent(string xml, ContentValidationResult validationResult)
    {
        if (validationResult.IsSecure)
            return xml;

        var sanitized = xml;
        
        // Remove malicious patterns
        foreach (var threat in validationResult.Threats.Where(t => t.Type == ThreatType.MaliciousContent))
        {
            if (!string.IsNullOrEmpty(threat.Pattern))
            {
                var regex = new Regex(threat.Pattern, RegexOptions.IgnoreCase);
                sanitized = regex.Replace(sanitized, "<!-- SANITIZED: Malicious content removed -->");
            }
        }

        // Log sanitization actions for security audit
        // Implementation would log to security audit trail
        
        return sanitized;
    }

    private static bool HasBinaryContent(string xml)
    {
        // Check for null bytes and other binary indicators
        return xml.Any(c => c == '\0' || (c < 32 && c != '\t' && c != '\n' && c != '\r'));
    }
}