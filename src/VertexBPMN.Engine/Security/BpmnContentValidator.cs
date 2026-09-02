using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using VertexBPMN.Domain.Model.Security;

namespace VertexBPMN.Engine.Security;

/// <summary>
/// Content validation and sanitization for BPMN input.
/// Detects and prevents malicious content patterns.
/// </summary>
public sealed class BpmnContentValidator
{
    private const string BpmnModelNamespace = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private const string UnsafeScriptElementDescription = "Script element outside the BPMN model namespace detected";

    private static readonly Regex[] _maliciousPatterns = 
    {
        new(@"&lt;script&gt;\b[^<]*(?:(?!<\/script>)<[^<]*)*&lt;/script&gt;", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"data:.*base64", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"vbscript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(?:^|[<\s])on[a-z][\w:-]*\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled), // HTML-style event-handler attributes
        new(@"expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), // CSS expressions
        new(@"<iframe\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<object\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<embed\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<!ENTITY", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"SYSTEM\s+['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"PUBLIC\s+['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled)
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

        // 2. BPMN script tasks legitimately use a BPMN <script> element. Only
        // script elements outside the BPMN model namespace are executable-content threats.
        var unsafeScriptElements = CountUnsafeScriptElements(xml);
        if (unsafeScriptElements > 0)
        {
            result.Threats.Add(new SecurityThreat
            {
                Type = ThreatType.MaliciousContent,
                Occurrences = unsafeScriptElements,
                Description = UnsafeScriptElementDescription
            });
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

        if (validationResult.Threats.Any(t =>
                string.Equals(t.Description, UnsafeScriptElementDescription, StringComparison.Ordinal)))
        {
            sanitized = RemoveUnsafeScriptElements(sanitized);
        }
        
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

    private static int CountUnsafeScriptElements(string xml)
    {
        try
        {
            using var reader = CreateSecureReader(xml);
            var document = XDocument.Load(reader, LoadOptions.None);
            return document.Root?
                .DescendantsAndSelf()
                .Count(IsUnsafeScriptElement) ?? 0;
        }
        catch (XmlException)
        {
            // Malformed XML and prohibited DTDs are handled by the secure parser pipeline.
            return 0;
        }
    }

    private static string RemoveUnsafeScriptElements(string xml)
    {
        try
        {
            using var reader = CreateSecureReader(xml);
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (document.Root is { } root)
            {
                root.DescendantsAndSelf()
                    .Where(IsUnsafeScriptElement)
                    .Remove();
            }
            return document.ToString(SaveOptions.DisableFormatting);
        }
        catch (XmlException)
        {
            return xml;
        }
    }

    private static XmlReader CreateSecureReader(string xml)
    {
        return XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
    }

    private static bool IsUnsafeScriptElement(XElement element)
    {
        return string.Equals(element.Name.LocalName, "script", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(element.Name.NamespaceName, BpmnModelNamespace, StringComparison.Ordinal);
    }
}
