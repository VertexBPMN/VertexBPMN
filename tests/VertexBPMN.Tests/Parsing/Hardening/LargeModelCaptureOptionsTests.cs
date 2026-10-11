using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Hardening;

public sealed class LargeModelCaptureOptionsTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task OptionalRawCaptureDoesNotRemoveExecutableNodes(bool skipRawCapture)
	{
		const string xml = """
			<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
			  <process id="large">
			    <documentation>Process documentation</documentation>
			    <startEvent id="start" />
			    <task id="task" />
			    <endEvent id="end" />
			    <sequenceFlow id="first" sourceRef="start" targetRef="task" />
			    <sequenceFlow id="second" sourceRef="task" targetRef="end" />
			    <textAnnotation id="note"><text>Model note</text></textAnnotation>
			    <association id="association" sourceRef="task" targetRef="note" />
			    <group id="group" />
			  </process>
			</definitions>
			""";
		var parser = new BpmnParser(new BpmnParserOptions
		{
			RoundtripMode = BpmnRoundtripMode.Strict,
			OptimizeLargeModels = true,
			LargeModelThreshold = 1,
			SkipDocumentationForLargeModels = skipRawCapture,
			SkipArtifactsForLargeModels = skipRawCapture,
			CaptureArtifacts = true
		});

		var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

		Assert.Equal("large", model.ProcessId);
		Assert.Equal("task", Assert.Single(model.Tasks!).Id);
		Assert.Equal(2, model.SequenceFlows!.Count);
		Assert.Single(model.TextAnnotations!);
		Assert.NotNull(model.RawMetadata);
		if (skipRawCapture)
		{
			Assert.Null(model.RawMetadata.RawDocumentation);
			Assert.Null(model.RawMetadata.RawArtifacts);
		}
		else
		{
			Assert.Contains("__process", model.RawMetadata.RawDocumentation!.Keys, StringComparer.Ordinal);
			Assert.Equal(3, model.RawMetadata.RawArtifacts!.Count);
		}
	}
}
