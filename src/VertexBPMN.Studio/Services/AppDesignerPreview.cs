using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// LC07 S4 preview builder: renders the current DRAFT (only allowlist components) against
/// an explicitly selected test target using REDACTED TEST DATA. The preview is strictly
/// read-only: it produces a pure data structure (pages + a redacted test-data snapshot) and
/// never invokes a task/process/external action. It mirrors the LC06 released renderer's
/// closed allowlist (ApplicationComponentCatalog == ReleasedRenderer.Allowlist) instead of
/// introducing an ad-hoc rendering mechanism — unknown components become a safe
/// Unsupported placeholder, exactly like <c>ReleasedRenderer</c>.
/// </summary>
internal static class AppDesignerPreview
{
    public const string RedactionPrefix = "[redigiert:";
    private const int PreviewRowCount = 2;
    private static readonly HashSet<string> Allowlist = new(ApplicationComponentCatalog.Components, StringComparer.Ordinal);

    /// <summary>The closed set of component types the preview knows how to render (mirrors the released renderer).</summary>
    public static IReadOnlySet<string> AllowlistForTest => Allowlist;

    /// <summary>
    /// Builds the read-only preview. An EXPLICIT test target (a data set to preview against)
    /// is mandatory — there is no implicit default that could leak or execute anything.
    /// </summary>
    public static AppPreview Build(string contentJson, PreviewTestTarget? target)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.DataSetName))
        {
            throw new InvalidOperationException("Für die Vorschau ist ein explizites Testziel (Datensatz) erforderlich.");
        }

        var document = AppDesignerDocument.Parse(contentJson);
        var pages = document.Pages
            .Select(page => new PreviewPage(page.Id, page.Route, BuildNodes(page.Components)))
            .ToList();
        var dataSets = document.DataSources
            .Select(BuildDataSet)
            .ToList();

        return new AppPreview(
            target,
            pages,
            dataSets,
            ReadOnly: true,
            UnsupportedCount: CountUnsupported(pages),
            ExecutableActions: []);
    }

    private static List<PreviewNode> BuildNodes(IEnumerable<DesignerComponent> components)
        => components.Select(BuildNode).ToList();

    private static PreviewNode BuildNode(DesignerComponent component)
    {
        var known = Allowlist.Contains(component.Type)
            && string.Equals(component.Version, AppDesignerDocument.AllowedVersion, StringComparison.Ordinal);
        return new PreviewNode(
            component.Type,
            component.Label,
            component.DataSource,
            HasAction: component.Action is not null,
            ActionIsSideEffecting: component.Action is not null && IsSideEffecting(component.Action.Type),
            Kind: known ? PreviewKind.Known : PreviewKind.Unsupported,
            Children: BuildNodes(component.Children));
    }

    /// <summary>Actions that would mutate production state if executed must be flagged as such (never executed here).</summary>
    private static bool IsSideEffecting(string actionType) => actionType is "start-process" or "claim-task" or "complete-task";

    private static PreviewDataSet BuildDataSet(DesignerDataSource source)
    {
        var rows = new List<IReadOnlyList<string>>();
        for (var rowIndex = 0; rowIndex < PreviewRowCount; rowIndex++)
        {
            rows.Add(source.Fields.Count == 0
                ? [$"{RedactionPrefix}leer]"]
                : source.Fields.Select(field => $"{RedactionPrefix}{field}]").ToList());
        }
        return new PreviewDataSet(source.Key, source.BindingVersion, source.Fields, rows);
    }

    private static int CountUnsupported(IReadOnlyList<PreviewPage> pages)
        => pages.Sum(page => page.Components.Sum(CountUnsupported));

    private static int CountUnsupported(PreviewNode node)
        => (node.Kind == PreviewKind.Unsupported ? 1 : 0) + node.Children.Sum(CountUnsupported);
}
