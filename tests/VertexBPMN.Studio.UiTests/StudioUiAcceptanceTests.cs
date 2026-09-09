using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

/// <summary>
/// Local phase-7 acceptance checks for keyboard access, accessibility semantics and reflow.
/// These checks use the deterministic Studio test host and intentionally do not run in CI.
/// </summary>
public sealed class StudioUiAcceptanceTests(StudioUiTestHost host) : IClassFixture<StudioUiTestHost>
{
    [Fact]
    [Trait("Category", "LocalUiAcceptance")]
    public async Task OAuthCallback_WithoutInitiatingBrowserProof_FailsBeforeApiCall()
    {
        var page = await host.Browser.NewPageAsync();
        try
        {
            var callbacksBefore = host.ApiRequests.Count(x => x.Contains("/api/oauth2/callback", StringComparison.Ordinal));
            var response = await page.GotoAsync($"{host.BaseAddress}oauth2/callback?state=foreign-state&code=foreign-code");
            Assert.NotNull(response);
            Assert.True(System.Net.Http.Headers.CacheControlHeaderValue.Parse(response.Headers["cache-control"]).NoStore);
            Assert.Equal("no-referrer", response.Headers["referrer-policy"]);
            await page.GetByText("Authorization could not be completed. Start again from Credentials in this browser tab using the original account.", new() { Exact = true }).WaitForAsync();
            Assert.Equal(callbacksBefore, host.ApiRequests.Count(x => x.Contains("/api/oauth2/callback", StringComparison.Ordinal)));
            Assert.DoesNotContain("foreign-code", page.Url);
            Assert.DoesNotContain(host.StudioLogs, line => line.Contains("foreign-code", StringComparison.Ordinal));
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    [Trait("Category", "LocalUiAcceptance")]
    public async Task Shell_Has_ScreenReader_Landmarks_And_Can_Be_Operated_By_Keyboard()
    {
        var page = await host.Browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        try
        {
            await page.GotoAsync(host.BaseAddress.ToString());
            await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
            await WaitForInteractiveStudioAsync(page);

            Assert.Equal("en", await page.Locator("html").GetAttributeAsync("lang"));
            Assert.Equal(1, await page.GetByRole(AriaRole.Main).CountAsync());
            Assert.Equal(1, await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).CountAsync());

            var menuButton = page.GetByRole(AriaRole.Button, new() { Name = "Toggle navigation", Exact = true });
            await FocusByKeyboardAsync(page, menuButton);
            var menuFocus = await GetFocusIndicatorStateAsync(menuButton);
            Assert.Contains("\"visible\":true", menuFocus, StringComparison.Ordinal);
            var menuBounds = await menuButton.BoundingBoxAsync();
            Assert.NotNull(menuBounds);
            Assert.True(menuBounds.Width >= 24 && menuBounds.Height >= 24,
                $"The menu target is only {menuBounds.Width:0}x{menuBounds.Height:0} CSS pixels.");
            await page.Keyboard.PressAsync("Enter");

            var navigation = page.GetByRole(AriaRole.Navigation);
            await navigation.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            var bpmnLink = navigation.GetByRole(AriaRole.Link, new() { Name = "BPMN Modeler", Exact = true });
            await FocusByKeyboardAsync(page, bpmnLink);
            var linkFocus = await GetFocusIndicatorStateAsync(bpmnLink);
            Assert.Contains("\"visible\":true", linkFocus, StringComparison.Ordinal);
            await page.Keyboard.PressAsync("Enter");
            await page.GetByRole(AriaRole.Heading, new() { Name = "BPMN Modeler", Exact = true }).WaitForAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    [Trait("Category", "LocalUiAcceptance")]
    public async Task Dialog_Returns_Keyboard_Focus_To_Its_Trigger()
    {
        var page = await host.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{host.BaseAddress}process-definitions");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync();
            await WaitForInteractiveStudioAsync(page);

            var trigger = page.GetByRole(AriaRole.Button, new() { Name = "View Versions", Exact = true });
            await trigger.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            var dialog = page.GetByRole(AriaRole.Dialog);
            await dialog.WaitForAsync();
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

            Assert.True(await trigger.EvaluateAsync<bool>("element => element === document.activeElement"),
                "Closing the dialog did not return focus to the button that opened it.");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Theory]
    [InlineData(640, 400)] // 1280 x 800 at 200% zoom
    [InlineData(320, 256)] // 1280 x 1024 at 400% zoom
    [Trait("Category", "LocalUiAcceptance")]
    public async Task Key_Pages_Reflow_Without_Uncontrolled_Page_Overflow(int width, int height)
    {
        var page = await host.Browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        try
        {
            foreach (var scenario in new[]
                     {
                         (Route: "", Heading: "Dashboard"),
                         (Route: "tasks", Heading: "Tasks"),
                         (Route: "bpmn-modeler", Heading: "BPMN Modeler")
                     })
            {
                await page.GotoAsync($"{host.BaseAddress}{scenario.Route}");
                await page.GetByRole(AriaRole.Heading, new() { Name = scenario.Heading, Exact = true }).First.WaitForAsync();
                var dimensions = await page.EvaluateAsync<int[]>(
                    "() => [document.documentElement.clientWidth, document.documentElement.scrollWidth]");
                Assert.Equal(dimensions[0], dimensions[1]);
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    [Trait("Category", "LocalUiAcceptance")]
    public async Task Bpmn_Workspace_Provides_At_Least_500_Pixels_Of_Canvas_At_1280_By_800()
    {
        var page = await host.Browser.NewPageAsync(new() { ViewportSize = new() { Width = 1280, Height = 800 } });
        try
        {
            await page.GotoAsync($"{host.BaseAddress}bpmn-modeler");
            var canvas = page.GetByTestId("bpmn-modeler-shell").Locator(".bpmn-io-canvas");
            await canvas.WaitForAsync();
            var bounds = await canvas.BoundingBoxAsync();

            Assert.NotNull(bounds);
            Assert.True(bounds.Height >= 500, $"The BPMN canvas is only {bounds.Height:0}px high at 1280x800.");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Theory]
    [InlineData("cmmn-modeler", "CMMN Modeler", "cmmn-modeler-shell")]
    [InlineData("form-builder", "Form Builder", "form-builder-shell")]
    [Trait("Category", "LocalUiAcceptance")]
    public async Task Local_Template_Based_Editors_Are_Ready_Without_A_Network_Timeout(
        string route,
        string heading,
        string readyTestId)
    {
        var page = await host.Browser.NewPageAsync();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await page.GotoAsync($"{host.BaseAddress}{route}");
            await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).WaitForAsync();
            await page.GetByTestId(readyTestId).WaitForAsync();
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"{heading} needed {stopwatch.Elapsed.TotalMilliseconds:0} ms to become ready; a local template must not wait for a network timeout.");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    [Trait("Category", "LocalUiAcceptance")]
    public async Task Core_Normal_Text_Meets_Wcag_Aa_Contrast()
    {
        var page = await host.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync(host.BaseAddress.ToString());
            var description = page.Locator(".studio-page-header__description");
            await page.Locator("[data-interactive-ready='true']").WaitForAsync();
            await description.WaitForAsync();
            var contrast = await description.EvaluateAsync<double>("""
                element => {
                    const rgb = value => {
                        const channels = value.match(/[\d.]+/g);
                        if (!channels || channels.length < 3)
                            throw new Error(`Unsupported computed color: ${value}`);
                        return channels.slice(0, 3).map(Number);
                    };
                    const luminance = value => {
                        const channels = rgb(value).map(channel => {
                            const normalized = channel / 255;
                            return normalized <= 0.04045
                                ? normalized / 12.92
                                : Math.pow((normalized + 0.055) / 1.055, 2.4);
                        });
                        return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
                    };
                    const foreground = getComputedStyle(element).color;
                    let backgroundElement = element;
                    let background = 'rgb(255, 255, 255)';
                    while (backgroundElement) {
                        const candidate = getComputedStyle(backgroundElement).backgroundColor;
                        if (candidate !== 'transparent'
                            && !candidate.endsWith(', 0)')
                            && candidate !== 'rgba(0, 0, 0, 0)') {
                            background = candidate;
                            break;
                        }
                        backgroundElement = backgroundElement.parentElement;
                    }
                    const light = Math.max(luminance(foreground), luminance(background));
                    const dark = Math.min(luminance(foreground), luminance(background));
                    return (light + 0.05) / (dark + 0.05);
                }
                """);

            Assert.True(contrast >= 4.5, $"Muted normal text contrast is only {contrast:0.00}:1; WCAG AA requires 4.5:1.");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static Task<string> GetFocusIndicatorStateAsync(ILocator locator) =>
        locator.EvaluateAsync<string>("""
            element => {
                const style = getComputedStyle(element);
                return JSON.stringify({
                    visible: (style.outlineStyle !== 'none' && parseFloat(style.outlineWidth) > 0)
                        || style.boxShadow !== 'none',
                    focusVisible: element.matches(':focus-visible'),
                    outlineStyle: style.outlineStyle,
                    outlineWidth: style.outlineWidth,
                    outlineColor: style.outlineColor,
                    boxShadow: style.boxShadow
                });
            }
            """);

    private static async Task FocusByKeyboardAsync(IPage page, ILocator target)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            await page.Keyboard.PressAsync("Tab");
            if (await target.EvaluateAsync<bool>("element => element === document.activeElement"))
                return;
        }

        var activeElement = await page.EvaluateAsync<string>(
            "() => `${document.activeElement?.tagName ?? 'none'}#${document.activeElement?.id ?? ''}.${document.activeElement?.className ?? ''}`");
        throw new InvalidOperationException($"Keyboard tab order did not reach the requested control. Active element: {activeElement}");
    }

    private static Task WaitForInteractiveStudioAsync(IPage page) =>
        page.Locator("[data-interactive-ready='true']").WaitForAsync();
}
