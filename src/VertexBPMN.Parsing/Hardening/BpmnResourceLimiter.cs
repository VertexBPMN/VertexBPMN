using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace VertexBPMN.Parsing.Hardening;

/// <summary>
/// Phase 11+: Resource limits for BPMN parsing to prevent DoS attacks.
/// Enforces memory, time, and structural complexity limits.
/// </summary>
public sealed class BpmnResourceLimiter
{
    private readonly BpmnSecurityOptions _options;

    public BpmnResourceLimiter(BpmnSecurityOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Validates XML input size and structure before parsing.
    /// </summary>
    public ValidationResult ValidateInputLimits(string xml)
    {
        var result = new ValidationResult { IsValid = true };

        // 1. XML Size Limit (DoS prevention)
        if (xml.Length > _options.MaxXmlSizeBytes)
        {
            result.IsValid = false;
            result.Violations.Add($"XML size {xml.Length:N0} bytes exceeds limit of {_options.MaxXmlSizeBytes:N0} bytes");
        }

        // 2. Basic structure validation (prevent deeply nested or malformed XML)
        var structureResult = ValidateXmlStructure(xml);
        if (!structureResult.IsValid)
        {
            result.IsValid = false;
            result.Violations.AddRange(structureResult.Violations);
        }

        // 3. Element count estimation (prevent XML bombs)
        var estimatedElements = EstimateElementCount(xml);
        if (estimatedElements > _options.MaxElementCount)
        {
            result.IsValid = false;
            result.Violations.Add($"Estimated element count {estimatedElements:N0} exceeds limit of {_options.MaxElementCount:N0}");
        }

        return result;
    }

    /// <summary>
    /// Creates an XML reader with security-hardened settings.
    /// </summary>
    public XmlReader CreateSecureXmlReader(string xml, CancellationToken cancellationToken = default)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,           // Prevent XXE
            XmlResolver = null,                               // Disable external resolution
            MaxCharactersInDocument = _options.MaxXmlSizeBytes,
            MaxCharactersFromEntities = 0,                    // No entity expansion
            CheckCharacters = true,                           // Validate XML characters
            ConformanceLevel = ConformanceLevel.Document,
            IgnoreWhitespace = false,                         // Preserve for roundtrip
            IgnoreComments = false,                           // Preserve comments
            IgnoreProcessingInstructions = false,             // Preserve PIs
            CloseInput = true,
            Async = true                                      // Enable async operations
        };

        var stringReader = new StringReader(xml);
        return XmlReader.Create(stringReader, settings);
    }

    /// <summary>
    /// Monitors parsing operation with timeout and memory limits.
    /// </summary>
    public async Task<T> ExecuteWithResourceLimitsAsync<T>(
        Func<CancellationToken, Task<T>> parseOperation,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.ParseTimeout);

        var initialMemory = GC.GetTotalMemory(false);
        
        try
        {
            var result = await parseOperation(timeoutCts.Token);
            
            // Check memory usage after parsing
            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;
            
            if (memoryUsed > _options.MaxMemoryUsageBytes)
            {
                throw new SecurityException(
                    $"Parse operation exceeded memory limit. Used: {memoryUsed:N0} bytes, Limit: {_options.MaxMemoryUsageBytes:N0} bytes");
            }
            
            return result;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new SecurityException($"Parse operation exceeded timeout limit of {_options.ParseTimeout.TotalSeconds:F1} seconds");
        }
    }

    private ValidationResult ValidateXmlStructure(string xml)
    {
        var result = new ValidationResult { IsValid = true };
        
        try
        {
            int maxDepth = 0;
            int currentDepth = 0;
            int elementCount = 0;
            
            // Quick structure scan without full parsing
            using var reader = new StringReader(xml);
            char? prevChar = null;
            bool inElement = false;
            
            int c;
            while ((c = reader.Read()) != -1)
            {
                char ch = (char)c;
                
                if (ch == '<' && prevChar != '\\')
                {
                    inElement = true;
                    elementCount++;
                    
                    // Peek ahead to see if it's a closing tag
                    int nextChar = reader.Peek();
                    if (nextChar == '/')
                    {
                        currentDepth--;
                    }
                    else if (nextChar != '!' && nextChar != '?') // Not comment or PI
                    {
                        currentDepth++;
                        maxDepth = Math.Max(maxDepth, currentDepth);
                    }
                }
                else if (ch == '>' && inElement)
                {
                    inElement = false;
                }
                
                prevChar = ch;
                
                // Early termination if limits exceeded
                if (maxDepth > _options.MaxXmlDepth)
                {
                    result.IsValid = false;
                    result.Violations.Add($"XML nesting depth {maxDepth} exceeds limit of {_options.MaxXmlDepth}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Violations.Add($"XML structure validation failed: {ex.Message}");
        }
        
        return result;
    }

    private static int EstimateElementCount(string xml)
    {
        // Quick estimate by counting '<' characters (excluding comments/CDATA)
        int count = 0;
        bool inComment = false;
        bool inCdata = false;
        
        for (int i = 0; i < xml.Length - 3; i++)
        {
            if (!inComment && !inCdata && xml[i] == '<')
            {
                // Check for comment start
                if (i + 3 < xml.Length && xml.Substring(i, 4) == "<!--")
                {
                    inComment = true;
                    continue;
                }
                
                // Check for CDATA start
                if (i + 8 < xml.Length && xml.Substring(i, 9) == "<![CDATA[")
                {
                    inCdata = true;
                    continue;
                }
                
                // Regular element
                count++;
            }
            else if (inComment && i + 2 < xml.Length && xml.Substring(i, 3) == "-->")
            {
                inComment = false;
            }
            else if (inCdata && i + 2 < xml.Length && xml.Substring(i, 3) == "]]>")
            {
                inCdata = false;
            }
        }
        
        return count;
    }
}

/// <summary>
/// Security configuration options for BPMN parsing.
/// </summary>
public sealed record BpmnSecurityOptions
{
    /// <summary>
    /// Maximum XML file size in bytes (default: 100MB).
    /// </summary>
    public long MaxXmlSizeBytes { get; init; } = 100 * 1024 * 1024;
    
    /// <summary>
    /// Maximum XML nesting depth (default: 100).
    /// </summary>
    public int MaxXmlDepth { get; init; } = 100;
    
    /// <summary>
    /// Maximum estimated element count (default: 100,000).
    /// </summary>
    public int MaxElementCount { get; init; } = 100_000;
    
    /// <summary>
    /// Maximum memory usage during parsing (default: 500MB).
    /// </summary>
    public long MaxMemoryUsageBytes { get; init; } = 500 * 1024 * 1024;
    
    /// <summary>
    /// Maximum parse operation timeout (default: 30 seconds).
    /// </summary>
    public TimeSpan ParseTimeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Enable detailed security logging.
    /// </summary>
    public bool EnableSecurityLogging { get; init; } = true;
}

/// <summary>
/// Security validation result with detailed violation information.
/// </summary>
public sealed record ValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Violations { get; set; } = new();
}

/// <summary>
/// Security-related exception for parsing operations.
/// </summary>
public sealed class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception innerException) : base(message, innerException) { }
}