using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Security;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Engine.Security;

/// <summary>
/// Phase 11: Fuzz testing harness for BPMN parser hardening.
/// Generates and executes malformed XML mutations to test parser resilience.
/// </summary>
public sealed class BpmnFuzzHarness
{
	private static readonly string[] _xmlMutations =
	[
        // Structural mutations
        "<", ">", "</>", "<<", ">>", "<>",
        
        // Namespace mutations  
        "xmlns=", "xmlns:bad=", "xmlns:=\"\"",
        
        // Attribute mutations
        "id=", "id=\"\"", "id=\"\"\"", "id='",
		"sourceRef=", "targetRef=", "name=",
        
        // Content mutations
        "&", "&amp", "&#", "&#x", "<?xml",
        
        // CDATA mutations
        "<![CDATA[", "]]>", "<![CDATA[]]>", "<![CDATA[invalid]]>",
        
        // Special characters
        "\0", "\x01", "\xFF", "€", "𝔲𝔫𝔦𝔠𝔬𝔡𝔢"
	];

	/// <summary>
	/// Executes fuzz testing with random XML mutations.
	/// </summary>
	public async Task<FuzzTestResult> ExecuteFuzzTestAsync(int iterations, TimeSpan timeout)
	{
		var result = new FuzzTestResult();
		var stopwatch = Stopwatch.StartNew();
		using var cancellation = new CancellationTokenSource(timeout);

		var parser = new BpmnParser(new BpmnParserOptions
		{
			RoundtripMode = BpmnRoundtripMode.Normalized, // Use normalized for resilience testing
			EnableAdvancedValidation = false, // Disable to focus on crash prevention
			ThrowOnFatalValidation = false,
			InternIds = true
		});

        const string baseTemplate = BaseBpmnTemplate;

		for (var i = 0; i < iterations && !cancellation.Token.IsCancellationRequested; i++)
		{
			result.TotalExecutions++;

			try
			{
				var mutatedXml = ApplyRandomMutations(baseTemplate);
				await parser.ParseAsync(mutatedXml, cancellation.Token);

				// If we reach here, parsing succeeded
				result.SuccessfulParses++;
			}
			catch (OperationCanceledException)
			{
				break; // Timeout reached
			}
			catch (OutOfMemoryException)
			{
				result.CrashCount++; // This counts as a crash
				break;
			}
			catch (StackOverflowException)
			{
				result.CrashCount++; // This counts as a crash
				break;
			}
			catch (AccessViolationException)
			{
				result.CrashCount++; // This counts as a crash
				break;
			}
			catch (Exception)
			{
				// Any other exception is considered a handled failure (good!)
				result.HandledFailures++;
			}
		}

		result.ExecutionTime = stopwatch.Elapsed;
		return result;
	}

	private static string ApplyRandomMutations(string baseXml)
	{
		var mutated = new StringBuilder(baseXml);
		var mutationCount = RandomNumberGenerator.GetInt32(1, 5); // Apply 1-4 random mutations

		for (var i = 0; i < mutationCount; i++)
		{
			ApplySingleMutation(mutated);
		}

		return mutated.ToString();
	}

	private static void ApplySingleMutation(StringBuilder xml)
	{
		var mutationType = RandomNumberGenerator.GetInt32(0, 6);

		switch (mutationType)
		{
			case 0: // Insert random characters
				InsertRandomCharacters(xml);
				break;
			case 1: // Delete random characters  
				DeleteRandomCharacters(xml);
				break;
			case 2: // Replace with mutation patterns
				ReplaceWithMutationPattern(xml);
				break;
			case 3: // Duplicate random section
				DuplicateRandomSection(xml);
				break;
			case 4: // Scramble attributes
				ScrambleAttributes(xml);
				break;
			case 5: // Insert malformed elements
				InsertMalformedElement(xml);
				break;
		}
	}

	private static void InsertRandomCharacters(StringBuilder xml)
	{
		if (xml.Length == 0)
		{
			return;
		}

		var position = RandomNumberGenerator.GetInt32(0, xml.Length);
		var mutation = _xmlMutations[RandomNumberGenerator.GetInt32(_xmlMutations.Length)];
		xml.Insert(position, mutation);
	}

	private static void DeleteRandomCharacters(StringBuilder xml)
	{
		if (xml.Length <= 10)
		{
			return; // Don't make it too small
		}

		var position = RandomNumberGenerator.GetInt32(0, xml.Length - 5);
		var length = RandomNumberGenerator.GetInt32(1, Math.Min(10, xml.Length - position));
		xml.Remove(position, length);
	}

	private static void ReplaceWithMutationPattern(StringBuilder xml)
	{
		var searchPatterns = new[] { "id=\"", "sourceRef=\"", "targetRef=\"", "<process", "<definitions" };
		var pattern = searchPatterns[RandomNumberGenerator.GetInt32(searchPatterns.Length)];

		var index = xml.ToString().IndexOf(pattern, StringComparison.Ordinal);
		if (index >= 0)
		{
			var mutation = _xmlMutations[RandomNumberGenerator.GetInt32(_xmlMutations.Length)];
			xml.Remove(index, Math.Min(pattern.Length, xml.Length - index));
			xml.Insert(index, mutation);
		}
	}

	private static void DuplicateRandomSection(StringBuilder xml)
	{
		if (xml.Length <= 20)
		{
			return;
		}

		var start = RandomNumberGenerator.GetInt32(0, xml.Length / 2);
		var length = RandomNumberGenerator.GetInt32(5, Math.Min(50, xml.Length - start));
		var section = xml.ToString(start, length);

		var insertPos = RandomNumberGenerator.GetInt32(0, xml.Length);
		xml.Insert(insertPos, section);
	}

	private static void ScrambleAttributes(StringBuilder xml)
	{
		// Find and scramble id attributes
		var xmlStr = xml.ToString();
		var idIndex = xmlStr.IndexOf("id=\"", StringComparison.Ordinal);
		if (idIndex >= 0)
		{
			var endQuote = xmlStr.IndexOf('"', idIndex + 4);
			if (endQuote > idIndex)
			{
				var scrambled = GenerateRandomId();
				xml.Remove(idIndex + 4, endQuote - idIndex - 4);
				xml.Insert(idIndex + 4, scrambled);
			}
		}
	}

	private static void InsertMalformedElement(StringBuilder xml)
	{
		var malformedElements = new[]
		{
			"<invalidElement>",
			"<element id=>",
			"<element id=\"\" id=\"duplicate\">",
			"<element sourceRef=\"nonexistent\">",
			"</>",
			"<element><nested></element></nested>",
			"<element attr=\"unclosed",
			"<element xmlns:bad=\"\">content</bad:element>"
		};

		var element = malformedElements[RandomNumberGenerator.GetInt32(malformedElements.Length)];
		var position = RandomNumberGenerator.GetInt32(0, xml.Length);
		xml.Insert(position, element);
	}

	private static string GenerateRandomId()
	{
		const string chars = "abcdefghijklmnopqrstuvwxyz0123456789_-";
		var length = RandomNumberGenerator.GetInt32(1, 20);
		var result = new StringBuilder(length);

		for (var i = 0; i < length; i++)
		{
			result.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
		}

		return result.ToString();
	}

	private const string BaseBpmnTemplate = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="testProcess">
    <startEvent id="start"/>
    <userTask id="task1" name="Task One"/>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>
  </process>
</definitions>
""";
}
