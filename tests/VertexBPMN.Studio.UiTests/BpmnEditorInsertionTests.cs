using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

/// <summary>Opt-in real Chromium tests for the shipped modeler and interop, without starting Aspire.</summary>
public sealed class BpmnEditorInsertionTests
{
    [Fact]
    public async Task CatalogInsertionPreservesRoutingAndRequiresACompleteIfCondition()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("VERTEXBPMN_EDITOR_TESTS") == "1", "Local only: set VERTEXBPMN_EDITOR_TESTS=1.");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src", "VertexBPMN.Studio"))) root = root.Parent;
        Assert.NotNull(root);
        var www = Path.Combine(root.FullName, "src", "VertexBPMN.Studio", "wwwroot");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, ExecutablePath = global::Chromium.Path });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1600, Height = 1100 } });
        var screenshots = Path.Combine(root.FullName, "tests", "VertexBPMN.Studio.UiTests", "TestResults", "editor-corrections");
        Directory.CreateDirectory(screenshots);
        await page.RouteAsync("http://editor.test/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath.TrimStart('/');
            if (path.Length == 0)
                await route.FulfillAsync(new() { ContentType = "text/html", Body = "<div id='canvas' style='width:1200px;height:900px'></div><div id='properties' style='position:absolute;left:1220px;top:0;width:360px;height:900px;overflow:auto'></div>" });
            else
                await route.FulfillAsync(new() { ContentType = path.EndsWith(".css", StringComparison.Ordinal) ? "text/css" : "text/javascript", Path = Path.Combine(www, path.Replace('/', Path.DirectorySeparatorChar)) });
        });
        await page.GotoAsync("http://editor.test/");
        await page.AddStyleTagAsync(new() { Url = "http://editor.test/lib/bpmn-js/diagram-js.css" });
        await page.AddStyleTagAsync(new() { Url = "http://editor.test/lib/bpmn-js/bpmn.css" });
        await page.AddStyleTagAsync(new() { Url = "http://editor.test/lib/bpmn-js-properties-panel/properties-panel.css" });
        await page.AddScriptTagAsync(new() { Url = "http://editor.test/lib/bpmn-js/bpmn-modeler.js" });
        await page.AddScriptTagAsync(new() { Url = "http://editor.test/lib/bpmn-js-properties-panel/properties-panel.js" });
        // The moddle descriptor is normally loaded by Studio's host page.
        var vertex = await File.ReadAllTextAsync(Path.Combine(www, "lib", "vertex-bpmn-moddle", "vertex.json"), TestContext.Current.CancellationToken);
        await page.EvaluateAsync("json => window.VertexBpmnModdle = JSON.parse(json)", vertex);
        await page.AddScriptTagAsync(new() { Url = "http://editor.test/js/bpmn-modeler.js", Type = "module" });
        var xml = """
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" targetNamespace="urn:test">
              <process id="p" isExecutable="true"><startEvent id="s"/><endEvent id="e"/><sequenceFlow id="f" sourceRef="s" targetRef="e"/></process>
              <bpmndi:BPMNDiagram id="diagram"><bpmndi:BPMNPlane id="plane" bpmnElement="p">
                <bpmndi:BPMNShape id="sd" bpmnElement="s"><dc:Bounds x="100" y="100" width="36" height="36"/></bpmndi:BPMNShape>
                <bpmndi:BPMNShape id="ed" bpmnElement="e"><dc:Bounds x="800" y="100" width="36" height="36"/></bpmndi:BPMNShape>
                <bpmndi:BPMNEdge id="fd" bpmnElement="f"><di:waypoint x="136" y="118"/><di:waypoint x="800" y="118"/></bpmndi:BPMNEdge>
              </bpmndi:BPMNPlane></bpmndi:BPMNDiagram>
            </definitions>
            """;
        await page.EvaluateAsync("async xml => { window.modeler = await BpmnModelerInterop.createModeler('canvas', xml, 'properties'); }", xml);
        var refusal = await page.EvaluateAsync<string>("""
            () => { try { BpmnModelerInterop.insertLowCodeNode(modeler, 'form'); return 'unexpected success'; } catch(e) { return e.message; } }
            """);
        Assert.Contains("Select a sequence flow", refusal);
        await page.EvaluateAsync("""
            () => { modeler.get('selection').select(modeler.get('elementRegistry').get('f')); BpmnModelerInterop.insertLowCodeNode(modeler, 'form'); }
            """);
        Assert.True(await page.EvaluateAsync<bool>("""
            () => { const task = modeler.get('elementRegistry').getAll().find(e => e.type === 'bpmn:UserTask');
              return task.incoming.length === 1 && task.outgoing.length === 1 && task.incoming[0].source.id === 's' && task.outgoing[0].target.id === 'e'; }
            """));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => BpmnModelerInterop.getValidationIssues(modeler).length"));
        Assert.True(await page.EvaluateAsync<bool>("""
            () => { const stack = modeler.get('commandStack'); stack.undo();
              const restored = !!modeler.get('elementRegistry').get('f') && !modeler.get('elementRegistry').getAll().some(e => e.type === 'bpmn:UserTask');
              stack.redo(); return restored && modeler.get('elementRegistry').getAll().filter(e => e.type === 'bpmn:UserTask').length === 1; }
            """));
        await page.EvaluateAsync("""
            () => { const task = modeler.get('elementRegistry').getAll().find(e => e.type === 'bpmn:UserTask');
              modeler.get('selection').select(task.outgoing[0]); BpmnModelerInterop.insertLowCodeNode(modeler, 'if'); }
            """);
        Assert.True(await page.EvaluateAsync<bool>("() => BpmnModelerInterop.getValidationIssues(modeler).some(i => i.code === 'DEP-GATEWAY-CONDITION')"));
        Assert.True(await page.EvaluateAsync<bool>("""
            () => { const issue = BpmnModelerInterop.getValidationIssues(modeler).find(i => i.code === 'DEP-GATEWAY-CONDITION');
              BpmnModelerInterop.focusElement(modeler, issue.elementId);
              return modeler.get('selection').get()[0].id === issue.elementId; }
            """));
        await page.EvaluateAsync("""
            () => { const branch = modeler.get('selection').get()[0];
              modeler.get('modeling').updateProperties(branch, { conditionExpression: modeler.get('bpmnFactory').create('bpmn:FormalExpression', { body: 'approved = true' }) });
              // Insert a second user task on the default branch and verify that its default binding survives.
              const split = branch.source;
              const defaultFlow = modeler.get('elementRegistry').get(split.businessObject.default.id);
              modeler.get('selection').select(defaultFlow); BpmnModelerInterop.insertLowCodeNode(modeler, 'form');
              window.defaultPreserved = split.outgoing.some(f => f.businessObject === split.businessObject.default && f.target.type === 'bpmn:UserTask');
            }
            """);
        Assert.True(await page.EvaluateAsync<bool>("() => window.defaultPreserved"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => BpmnModelerInterop.getValidationIssues(modeler).length"));
        var exported = await page.EvaluateAsync<string>("() => BpmnModelerInterop.getXml(modeler)");
        Assert.Contains("approved = true", exported);
        Assert.Contains("approval-form", exported);
        Assert.Empty(await page.EvaluateAsync<string[]>("xml => window.VertexValidateBpmn(xml).map(i => i.code)", exported));
        using (var corpus = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(root.FullName, "tests", "VertexBPMN.Tests", "TestData", "editor-validation-cases.json"), TestContext.Current.CancellationToken)))
        {
            foreach (var item in corpus.RootElement.EnumerateArray())
            {
                var expected = item.GetProperty("codes").EnumerateArray().Select(c => c.GetString()).Order().ToArray();
                var actual = await page.EvaluateAsync<string[]>("xml => window.VertexValidateBpmn(xml).map(i => i.code)", item.GetProperty("xml").GetString());
                Assert.Equal(expected, actual.Order().ToArray());
            }
        }
        await page.EvaluateAsync("() => { modeler.get('selection').select([]); modeler.get('canvas').zoom('fit-viewport'); }");
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, "if-insertion.png") });
        foreach (var pattern in new[] { "http-retry", "webhook-if-http", "cron-batch-db", "user-approval", "decision-routing", "case-start" })
        {
            await page.EvaluateAsync("async xml => { await BpmnModelerInterop.loadXml(modeler, xml); modeler.get('commandStack').clear(); }", xml);
            await page.EvaluateAsync("id => { BpmnModelerInterop.insertLowCodePattern(modeler, id); }", pattern);
            var codes = await page.EvaluateAsync<string[]>("() => BpmnModelerInterop.getValidationIssues(modeler).map(i => i.code)");
            if (pattern is "webhook-if-http" or "decision-routing") Assert.Equal(["DEP-GATEWAY-CONDITION"], codes);
            else Assert.Empty(codes);
            await page.EvaluateAsync("() => { modeler.get('selection').select([]); modeler.get('canvas').zoom('fit-viewport'); }");
            await page.ScreenshotAsync(new() { Path = Path.Combine(screenshots, pattern + ".png") });
            Assert.True(await page.EvaluateAsync<bool>("""
                () => { const before = modeler.get('elementRegistry').getAll().length;
                  const stack = modeler.get('commandStack'); stack.undo();
                  const baseline = modeler.get('elementRegistry').getAll().filter(e => e.businessObject?.$instanceOf('bpmn:FlowNode')).length === 2;
                  stack.redo(); return baseline && modeler.get('elementRegistry').getAll().length === before; }
                """));
        }
    }
}
