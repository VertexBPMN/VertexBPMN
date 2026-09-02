using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing;

public sealed class CmmnParserTests
{
    [Fact]
    public async Task ResolvesDefinitionReferencesStagesDiscretionaryItemsAndStandardSentries()
    {
        const string xml = """
            <definitions xmlns="https://www.omg.org/spec/CMMN/20151109/MODEL">
              <case id="case" name="Claims case">
                <casePlanModel id="plan">
                  <planItem id="stageItem" definitionRef="stageDefinition" />
                  <stage id="stageDefinition">
                    <planItem id="review" definitionRef="reviewDefinition">
                      <entryCriterion sentryRef="documentChanged" />
                    </planItem>
                    <humanTask id="reviewDefinition" name="Review" />
                  </stage>
                  <planningTable>
                    <discretionaryItem id="extraReview" definitionRef="extraDefinition" />
                  </planningTable>
                  <manualTask id="extraDefinition" />
                  <caseFileItem id="document" name="Document" />
                  <sentry id="documentChanged">
                    <caseFileItemOnPart sourceRef="document">
                      <standardEvent>update</standardEvent>
                    </caseFileItemOnPart>
                    <ifPart><condition><body>approved = true</body></condition></ifPart>
                  </sentry>
                </casePlanModel>
              </case>
            </definitions>
            """;

        var model = await new CmmnParser().ParseAsync(xml, TestContext.Current.CancellationToken);

        var stage = Assert.Single(model.PlanItems, item => item.Id == "stageItem");
        Assert.Equal("stage", stage.Type);
        var review = Assert.Single(model.PlanItems, item => item.Id == "review");
        Assert.Equal("humanTask", review.Type);
        Assert.Equal("stageItem", review.ParentPlanItemId);
        var discretionary = Assert.Single(model.PlanItems, item => item.Id == "extraReview");
        Assert.True(discretionary.IsDiscretionary);
        var sentry = Assert.Single(model.Sentries);
        Assert.Equal("document", sentry.OnPartRef);
        Assert.Equal("update", Assert.Single(sentry.Conditions).OnPartEvent);
        Assert.Equal("approved = true", Assert.Single(sentry.Conditions).Expression);
    }

    [Fact]
    public async Task RejectsUnknownDefinitionReferences()
    {
        const string xml = """
            <definitions xmlns="https://www.omg.org/spec/CMMN/20151109/MODEL">
              <case id="case"><casePlanModel id="plan"><planItem id="broken" definitionRef="missing" /></casePlanModel></case>
            </definitions>
            """;

        await Assert.ThrowsAsync<CmmnParseException>(
            () => new CmmnParser().ParseAsync(xml, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsDtdAndExternalEntities()
    {
        const string xml = """
            <!DOCTYPE definitions [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <definitions xmlns="https://www.omg.org/spec/CMMN/20151109/MODEL">
              <case id="case"><casePlanModel id="plan">&xxe;</casePlanModel></case>
            </definitions>
            """;

        await Assert.ThrowsAsync<CmmnParseException>(
            () => new CmmnParser().ParseAsync(xml, TestContext.Current.CancellationToken));
    }
}
