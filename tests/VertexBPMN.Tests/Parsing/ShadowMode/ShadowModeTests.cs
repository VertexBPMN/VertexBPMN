//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using VertexBPMN.Domain.Interfaces;
//using VertexBPMN.Engine.Parsing;
//using Xunit;

//namespace VertexBPMN.Test.Parsing.ShadowMode;

///// <summary>
///// Phase 9: Tests for Engine Parser Shadow Mode
///// Verifies shadow mode facade correctly delegates to unified parser
///// and provides accurate diff diagnostics between approaches.
///// </summary>
//public class ShadowModeTests
//{
//    private readonly ITestOutputHelper _output;
    
//    public ShadowModeTests(ITestOutputHelper output)
//    {
//        _output = output;
//    }

//    [Fact]
//    public async Task ShadowModeFacade_ShouldProvideBackwardCompatibleAPI()
//    {
//        // RED: This test will fail until we implement the facade
//        var facade = new LegacyEngineParserFacade();
        
//        var xml = """
//<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
//  <process id='test-process'>
//    <startEvent id='start'/>
//    <userTask id='task1' name='Review Document'/>
//    <endEvent id='end'/>
//    <sequenceFlow id='f1' sourceRef='start' targetRef='task1'/>
//    <sequenceFlow id='f2' sourceRef='task1' targetRef='end'/>
//  </process>
//</definitions>
//""";

//        var result = await facade.ParseForEngineAsync(xml);
        
//        Assert.NotNull(result);
//        Assert.Equal("test-process", result.Key);
//        Assert.Contains(result.Nodes, n => n.Key == "start");
//        Assert.Contains(result.Nodes, n => n.Key == "task1");
//        Assert.Contains(result.Nodes, n => n.Key == "end");
//        Assert.Equal(2, result.SequenceFlows.Count);
//    }

////    [Fact]
////    public async Task ShadowModeComparator_ShouldDetectMismatchesBetweenApproaches()
////    {
////        // RED: This test will fail until we implement the comparator
////        var xml = """
////<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
////  <process id='test-process'>
////    <startEvent id='start'/>
////    <userTask id='task1'>
////      <extensionElements>
////        <camunda:assignee>john.doe</camunda:assignee>
////        <camunda:formField id='amount' type='long'/>
////      </extensionElements>
////    </userTask>
////    <endEvent id='end'/>
////    <sequenceFlow id='f1' sourceRef='start' targetRef='task1'/>
////    <sequenceFlow id='f2' sourceRef='task1' targetRef='end' default='true'>
////      <conditionExpression>${amount > 1000}</conditionExpression>
////    </sequenceFlow>
////  </process>
////</definitions>
////""";

////        var comparator = new EngineParserComparator();
////        var comparison = await comparator.CompareAsync(xml);
        
////        Assert.NotNull(comparison);
////        Assert.Empty(comparison.CriticalMismatches); // Should have no critical differences
////        // May have minor differences in extension handling
////    }

//    [Fact]
//    public async Task DeprecationWarnings_ShouldBeLoggedWhenLegacyParserDirectlyUsed()
//    {
//        // RED: This test will fail until we implement deprecation warnings
//        var loggerFactory = LoggerFactory.Create(builder => builder.AddXUnit(_output));
//        var logger = loggerFactory.CreateLogger<LegacyEngineParserFacade>();
        
//        var facade = new LegacyEngineParserFacade(logger);
        
//        var xml = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='p1'/></definitions>";
        
//        await facade.ParseForEngineAsync(xml);
        
//        // Verify deprecation warning was logged
//        // Note: In real implementation, we'd use a test logger that captures log messages
//    }

//    [Fact]
//    public void DependencyInjection_ShouldRegisterUnifiedParserAsDefault()
//    {
//        // RED: This test will fail until we update DI registration
//        var services = new ServiceCollection();
        
//        // This method should register unified parser as the default
//        services.AddVertexBpmnParsing();
        
//        var serviceProvider = services.BuildServiceProvider();
//        var parser = serviceProvider.GetRequiredService<IBpmnParser>();
        
//        Assert.IsType<BpmnParser>(parser); // Should be unified parser, not legacy
//    }
//}