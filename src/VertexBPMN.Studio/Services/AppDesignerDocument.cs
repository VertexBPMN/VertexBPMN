using System.Text.Json;
using System.Text.Json.Nodes;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// Authoring model for the Studio low-code application designer (LC07 S1 + S2). It holds a
/// draft's ContentJson in the released LC06 shape (dataSources with key + bindingVersion,
/// pages with an ordered component list and per-component properties) and edits it behind the
/// CLOSED renderer allowlist: every add/remove/reorder/property edit is validated against
/// <see cref="ApplicationComponentCatalog"/> so the designer can never emit an unknown
/// component type/version/prop. Invalid input is rejected (an InvalidOperationException
/// describing the unsupported item), mirroring the released renderer's UnsupportedComponent
/// posture - but at authoring time.
/// <para>
/// LC07 S2 adds the data-source + data-binding + schema-validated field-mapping layer:
/// a component's <c>dataSource</c> must reference a DECLARED source (otherwise a
/// UNKNOWN_DATA_SOURCE diagnostic, mirroring <see cref="ApplicationDomainDiagnostics"/>),
/// and field mappings may only reference fields that exist on the bound data source's
/// authored schema (unknown fields are rejected at authoring time).
/// </para>
/// </summary>
internal sealed class AppDesignerDocument
{
    public const string AllowedVersion = "1.0.0";
    private static readonly JsonSerializerOptions EndpointJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Editable scalar properties per allowlist component type (mirrors the LC06 renderer props).</summary>
    private static readonly Dictionary<string, string[]> AllowedProperties =
        new(StringComparer.Ordinal)
        {
            ["text"] = ["label"],
            ["button"] = ["label"],
            ["table"] = ["dataSource"],
            ["detail-card"] = ["dataSource"],
            ["form-js"] = [],
            ["status-diagram"] = [],
            ["page-container"] = []
        };

    private readonly List<DesignerPage> _pages;
    private readonly List<DesignerDataSource> _dataSources;
    private readonly List<string> _referencedProcessKeys;
    private readonly HashSet<string> _knownProcessKeys;
    private JsonObject _rootMetadata = new() { ["localizations"] = new JsonObject() };

    // LC07 S4: a bounded command stack over the draft. Every authoring mutation snapshots
    // the prior model state onto the undo stack; undo/redo swap states through the paired
    // stacks. The redo branch is cleared by any fresh edit so a stale snapshot can never
    // re-apply over newer authoring.
    private readonly List<DocumentState> _undoStack = [];
    private readonly List<DocumentState> _redoStack = [];
    private const int MaxUndoDepth = 200;
    private bool _inHistoryRestore;

    private AppDesignerDocument(List<DesignerPage> pages, List<DesignerDataSource> dataSources)
    {
        _pages = pages;
        _dataSources = dataSources;
        _referencedProcessKeys = new List<string>();
        _knownProcessKeys = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>A new, empty draft: no pages, no data sources yet (valid released content).</summary>
    public static AppDesignerDocument Empty => new([], []);

    /// <summary>
    /// Declares the catalogue of KNOWN process definition keys available to the open tenant
    /// (loaded from the Studio process-definitions catalogue). start-process actions may only
    /// reference a key from this catalogue; anything else is rejected / flagged UNKNOWN_PROCESS.
    /// </summary>
    public AppDesignerDocument SetProcessCatalog(IEnumerable<string> keys)
    {
        _knownProcessKeys.Clear();
        foreach (var key in keys.Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key.Trim()))
        {
            _knownProcessKeys.Add(key);
        }
        return this;
    }

    /// <summary>The known process definition keys from the open tenant's process catalogue.</summary>
    public IReadOnlyList<string> GetKnownProcessKeys() => _knownProcessKeys.OrderBy(key => key, StringComparer.Ordinal).ToList();

    /// <summary>The closed set of component types the palette offers (the renderer allowlist).</summary>
    public static IReadOnlyList<string> GetPaletteTypes()
        => ApplicationComponentCatalog.Components.OrderBy(type => type, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> EditablePropertiesFor(string type)
        => AllowedProperties.TryGetValue(type, out var props) ? props : [];

    // ---------------------------------------------------------------- Undo / Redo (LC07 S4)
    /// <summary>An immutable full snapshot of the draft's authorable state (deep-cloned).</summary>
    private sealed record DocumentState(List<DesignerPage> Pages, List<DesignerDataSource> DataSources, List<string> ReferencedProcessKeys, JsonObject RootMetadata);

    public bool CanUndo => _undoStack.Count > 0 && !_inHistoryRestore;
    public bool CanRedo => _redoStack.Count > 0 && !_inHistoryRestore;

    /// <summary>Reverts the last authoring edit. Returns false when there is nothing to undo.</summary>
    public bool Undo()
    {
        if (_undoStack.Count == 0)
        {
            return false;
        }
        _redoStack.Add(CaptureState());
        Restore(_undoStack[^1]);
        _undoStack.RemoveAt(_undoStack.Count - 1);
        return true;
    }

    /// <summary>Re-applies a reverted edit. Returns false when there is nothing to redo.</summary>
    public bool Redo()
    {
        if (_redoStack.Count == 0)
        {
            return false;
        }
        _undoStack.Add(CaptureState());
        Restore(_redoStack[^1]);
        _redoStack.RemoveAt(_redoStack.Count - 1);
        return true;
    }

    /// <summary>Snapshots the current authorable state before a mutation is applied.</summary>
    private void RecordHistory()
    {
        if (_inHistoryRestore)
        {
            // Restoring is an internal operation, never an authoring edit to re-record.
            return;
        }
        _undoStack.Add(CaptureState());
        if (_undoStack.Count > MaxUndoDepth)
        {
            _undoStack.RemoveAt(0);
        }
        // A fresh edit invalidates any redo branch: a stale snapshot must never re-apply.
        _redoStack.Clear();
    }

    private DocumentState CaptureState() => new(
        _pages.Select(ClonePage).ToList(),
        _dataSources.Select(source => new DesignerDataSource(source.Key, source.BindingVersion, source.Fields.ToList())
            { Metadata = (JsonObject)source.Metadata.DeepClone() }).ToList(),
        _referencedProcessKeys.ToList(), (JsonObject)_rootMetadata.DeepClone());

    private void Restore(DocumentState state)
    {
        _inHistoryRestore = true;
        try
        {
            _pages.Clear();
            _pages.AddRange(state.Pages);
            _dataSources.Clear();
            _dataSources.AddRange(state.DataSources);
            _referencedProcessKeys.Clear();
            _referencedProcessKeys.AddRange(state.ReferencedProcessKeys);
            _rootMetadata = (JsonObject)state.RootMetadata.DeepClone();
        }
        finally
        {
            _inHistoryRestore = false;
        }
    }

    private static DesignerPage ClonePage(DesignerPage page)
        => new(page.Id, page.Route, page.Title, page.Components.Select(CloneComponent).ToList())
            { Metadata = (JsonObject)page.Metadata.DeepClone() };

    private static DesignerComponent CloneComponent(DesignerComponent component)
    {
        var clone = new DesignerComponent(
            component.Id, component.Type, component.Version, component.Label, component.DataSource,
            component.Children.Select(CloneComponent).ToList(),
            component.Mappings.Select(mapping => new StudioFieldMappingItem(mapping.SourcePath, mapping.TargetKey, mapping.Transform, mapping.Required)).ToList())
            { Metadata = (JsonObject)component.Metadata.DeepClone() };
        if (component.Action is not null)
        {
            clone.Action = new DesignerAction(
                component.Action.Type, component.Action.Version, component.Action.Target,
                component.Action.Inputs.Select(input => new DesignerActionInput(input.Name, input.Value)).ToList());
        }
        return clone;
    }

    public IReadOnlyList<DesignerPage> Pages => _pages;

    /// <summary>The declared data sources (key + bindingVersion + authored schema).</summary>
    public IReadOnlyList<DesignerDataSource> DataSources => _dataSources;

    public static AppDesignerDocument Parse(string contentJson)
    {
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        var pages = new List<DesignerPage>();
        if (root.TryGetProperty("pages", out var pagesElement) && pagesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var page in pagesElement.EnumerateArray())
            {
                var components = new List<DesignerComponent>();
                if (page.TryGetProperty("components", out var componentList) && componentList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var component in componentList.EnumerateArray())
                    {
                        components.Add(ParseComponent(component));
                    }
                }
                pages.Add(new DesignerPage(
                    GetString(page, "id") ?? Guid.NewGuid().ToString("n"),
                    GetString(page, "route") ?? "/page",
                    GetString(page, "title") ?? GetString(page, "id") ?? "Page",
                    components) { Metadata = JsonNode.Parse(page.GetRawText())!.AsObject() });
            }
        }

        var dataSources = new List<DesignerDataSource>();
        if (root.TryGetProperty("dataSources", out var dataSourcesElement) && dataSourcesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in dataSourcesElement.EnumerateArray())
            {
                var key = GetString(source, "key");
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }
                var bindingVersion = source.TryGetProperty("bindingVersion", out var versionElement) && versionElement.TryGetInt32(out var version)
                    ? version : 0;
                var fields = new List<string>();
                if (source.TryGetProperty("fields", out var fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var field in fieldsElement.EnumerateArray())
                    {
                        if (field.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(field.GetString()))
                        {
                            fields.Add(field.GetString()!);
                        }
                    }
                }
                dataSources.Add(new DesignerDataSource(key, bindingVersion, fields)
                    { Metadata = JsonNode.Parse(source.GetRawText())!.AsObject() });
            }
        }
        var result = new AppDesignerDocument(pages, dataSources)
        {
            _rootMetadata = JsonNode.Parse(contentJson)!.AsObject()
        };
        result._rootMetadata.Remove("pages");
        result._rootMetadata.Remove("dataSources");
        return result;
    }

    private static DesignerComponent ParseComponent(JsonElement component)
    {
        var children = new List<DesignerComponent>();
        if (component.TryGetProperty("children", out var childrenElement) && childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenElement.EnumerateArray())
            {
                children.Add(ParseComponent(child));
            }
        }
        var mappings = new List<StudioFieldMappingItem>();
        if (component.TryGetProperty("mappings", out var mappingsElement) && mappingsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var mapping in mappingsElement.EnumerateArray())
            {
                var source = GetString(mapping, "sourcePath");
                var target = GetString(mapping, "targetKey");
                var transform = GetString(mapping, "transform");
                var required = mapping.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.True;
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }
                mappings.Add(new StudioFieldMappingItem(source, target, transform, required));
            }
        }
        return new DesignerComponent(
            GetString(component, "id") ?? Guid.NewGuid().ToString("n"),
            GetString(component, "type") ?? "text",
            GetString(component, "version") ?? AllowedVersion,
            GetString(component, "label"),
            GetString(component, "dataSource"),
            children,
            mappings) { Action = ParseAction(component), Metadata = JsonNode.Parse(component.GetRawText())!.AsObject() };
    }

    private static DesignerAction? ParseAction(JsonElement component)
    {
        if (!component.TryGetProperty("action", out var actionElement) || actionElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var type = GetString(actionElement, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }
        var action = new DesignerAction(type, GetString(actionElement, "version") ?? AllowedVersion, null, []);
        var targetProperty = ApplicationActionCatalog.TargetPropertyFor(type);
        action.Target = GetString(actionElement, targetProperty);

        if (actionElement.TryGetProperty("inputs", out var inputs) && inputs.ValueKind == JsonValueKind.Object)
        {
            foreach (var input in inputs.EnumerateObject())
            {
                var value = input.Value.ValueKind == JsonValueKind.String ? input.Value.GetString() : input.Value.GetRawText();
                action.Inputs.Add(new DesignerActionInput(input.Name, value ?? string.Empty));
            }
        }
        return action;
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // ---------------------------------------------------------------- Data sources
    public DesignerDataSource AddDataSource(string key, int bindingVersion = 2, IEnumerable<string>? fields = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("A data source key is required.");
        }
        var normalizedKey = key.Trim();
        if (_dataSources.Any(source => string.Equals(source.Key, normalizedKey, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"A data source with key '{normalizedKey}' is already declared.");
        }
        var fieldList = (fields ?? []).Select(field => field.Trim()).Where(field => field.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var source = new DesignerDataSource(normalizedKey, bindingVersion, fieldList);
        RecordHistory();
        _dataSources.Add(source);
        return source;
    }

    public void RemoveDataSource(string key)
    {
        var source = _dataSources.SingleOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Data source not found.");
        RecordHistory();
        _dataSources.Remove(source);
    }

    public bool IsDataSourceDeclared(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }
        return _dataSources.Any(source => string.Equals(source.Key, key, StringComparison.Ordinal));
    }

    /// <summary>The authored schema (field names) of a declared data source, or <c>null</c> if undeclared.</summary>
    public IReadOnlyList<string>? GetDataSourceFields(string key)
    {
        var source = _dataSources.SingleOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
        return source?.Fields;
    }

    // ---------------------------------------------------------------- Data binding
    /// <summary>
    /// Binds a component to a DECLARED data source. Binding to an undeclared source is
    /// rejected at authoring time (the draft can never carry a dangling reference created
    /// through the designer; any that would arrive from external content is surfaced by
    /// <see cref="Validate"/> as UNKNOWN_DATA_SOURCE).
    /// </summary>
    public void BindComponent(string pageId, string componentId, string dataSourceKey)
    {
        var component = FindComponent(pageId, componentId);
        if (!IsDataSourceDeclared(dataSourceKey))
        {
            throw new InvalidOperationException($"Data source '{dataSourceKey}' is not declared. Declare it under Data sources before binding.");
        }
        // Snapshot BEFORE any mutation: a source switch also clears stale field mappings,
        // and undo must restore those original mappings too.
        RecordHistory();
        if (component.DataSource is not null && !string.Equals(component.DataSource, dataSourceKey, StringComparison.Ordinal))
        {
            // Switching to a different source invalidates prior field mappings authored for the old schema.
            component.Mappings.Clear();
        }
        component.DataSource = dataSourceKey.Trim();
    }

    // ---------------------------------------------------------------- Field mapping
    /// <summary>
    /// Adds a field mapping (data-source field -> table column / component field) to a bound
    /// component. The source field must exist on the bound data source's authored schema;
    /// unknown fields are rejected, never silently accepted.
    /// </summary>
    public StudioFieldMappingItem MapField(string pageId, string componentId, string sourceField, string targetKey, string? transform = "Copy", bool required = false)
    {
        var component = FindComponent(pageId, componentId);
        if (string.IsNullOrWhiteSpace(component.DataSource))
        {
            throw new InvalidOperationException("Bind the component to a data source before mapping fields.");
        }
        if (string.IsNullOrWhiteSpace(sourceField) || string.IsNullOrWhiteSpace(targetKey))
        {
            throw new InvalidOperationException("Both the source field and the target column are required.");
        }
        var schema = GetDataSourceFields(component.DataSource);
        if (schema?.Contains(sourceField.Trim(), StringComparer.Ordinal) != true)
        {
            throw new InvalidOperationException(
                $"Unknown field '{sourceField.Trim()}' for data source '{component.DataSource}'. It must exist on the data source's schema.");
        }
        var mapping = new StudioFieldMappingItem(sourceField.Trim(), targetKey.Trim(), transform, required);
        RecordHistory();
        component.Mappings.Add(mapping);
        return mapping;
    }

    public void RemoveFieldMapping(string pageId, string componentId, int index)
    {
        var component = FindComponent(pageId, componentId);
        if (index < 0 || index >= component.Mappings.Count)
        {
            throw new InvalidOperationException("Mapping not found.");
        }
        RecordHistory();
        component.Mappings.RemoveAt(index);
    }

    // ---------------------------------------------------------------- Validation
    /// <summary>
    /// Authoring-time validation diagnostics over the whole draft, mirroring the released
    /// semantic gate. Returns diagnostic codes/messages: a referenced-but-undeclared data
    /// source yields "UNKNOWN_DATA_SOURCE"; a mapping referencing a field outside the bound
    /// source's authored schema yields "UNKNOWN_MAPPING_FIELD".
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var diagnostics = new List<string>();

        // Reuse the released semantic gate for declared+referenced data sources (UNKNOWN_DATA_SOURCE).
        using (var released = JsonDocument.Parse(ApplicationDomainDiagnostics.Validate(ToJson())))
        {
            if (released.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var code in released.RootElement.EnumerateArray())
                {
                    if (code.ValueKind == JsonValueKind.String)
                    {
                        diagnostics.Add(code.GetString()!);
                    }
                }
            }
        }

        // Field mappings must reference fields that exist on the bound data source's schema.
        foreach (var page in _pages)
        {
            foreach (var component in CollectComponents(page))
            {
                if (component.Mappings.Count == 0 || string.IsNullOrWhiteSpace(component.DataSource))
                {
                    continue;
                }
                var schema = GetDataSourceFields(component.DataSource);
                foreach (var mapping in component.Mappings.Where(mapping => schema?.Contains(mapping.SourcePath, StringComparer.Ordinal) != true))
                {
                    diagnostics.Add($"UNKNOWN_MAPPING_FIELD '{mapping.SourcePath}' on '{component.DataSource}'");
                }
            }
        }

        // Actions must resolve their reference target against the CLOSED authoring catalogues.
        // (The released gate above already flags a data-source-read to an undeclared source and
        // a start-process whose key is absent from references; here we also enforce that a
        // start-process points at a key the OPEN process catalogue actually knows, so a crafted
        // bogus reference never slips through.)
        foreach (var action in _pages.SelectMany(CollectComponents).Select(component => component.Action).OfType<DesignerAction>())
        {
            if (action.Type == "start-process" && (string.IsNullOrWhiteSpace(action.Target) || !_knownProcessKeys.Contains(action.Target)))
            {
                diagnostics.Add($"UNKNOWN_PROCESS '{action.Target}'");
            }
            else if (action.Type == "data-source-read" && (string.IsNullOrWhiteSpace(action.Target) || !IsDataSourceDeclared(action.Target)))
            {
                diagnostics.Add($"UNKNOWN_DATA_SOURCE '{action.Target}'");
            }
        }
        return diagnostics;
    }

    // ---------------------------------------------------------------- Pages
    public DesignerPage AddPage(string title)
    {
        var slug = Slug(title);
        var route = $"/{slug}";
        if (_pages.Any(page => string.Equals(page.Route, route, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"A page with route '{route}' already exists.");
        }
        var id = slug;
        if (_pages.Any(page => string.Equals(page.Id, id, StringComparison.Ordinal)))
        {
            id = $"{slug}-{Guid.NewGuid():N}"[..12];
        }
        var page = new DesignerPage(id, route, string.IsNullOrWhiteSpace(title) ? "Page" : title.Trim(), []);
        RecordHistory();
        _pages.Add(page);
        return page;
    }

    public void RemovePage(string pageId)
    {
        var page = FindPage(pageId);
        RecordHistory();
        _pages.Remove(page);
    }

    public void MovePage(string pageId, int toIndex)
    {
        var page = FindPage(pageId);
        RecordHistory();
        _pages.Remove(page);
        _pages.Insert(Math.Clamp(toIndex, 0, _pages.Count), page);
    }

    // ---------------------------------------------------------------- Components
    public DesignerComponent AddComponent(string pageId, string type)
    {
        if (!ApplicationComponentCatalog.Components.Contains(type))
        {
            throw new InvalidOperationException($"Unsupported component type '{type}'. The palette is a closed allowlist.");
        }
        var component = new DesignerComponent(Guid.NewGuid().ToString("n"), type, AllowedVersion, null, null, []);
        RecordHistory();
        FindPage(pageId).Components.Add(component);
        return component;
    }

    public void RemoveComponent(string pageId, string componentId)
    {
        var page = FindPage(pageId);
        var component = page.Components.SingleOrDefault(candidate => candidate.Id == componentId)
            ?? throw new InvalidOperationException("Component not found.");
        RecordHistory();
        page.Components.Remove(component);
    }

    public void MoveComponent(string pageId, string componentId, int toIndex)
    {
        var page = FindPage(pageId);
        var component = page.Components.SingleOrDefault(candidate => candidate.Id == componentId)
            ?? throw new InvalidOperationException("Component not found.");
        RecordHistory();
        page.Components.Remove(component);
        page.Components.Insert(Math.Clamp(toIndex, 0, page.Components.Count), component);
    }

    // ---------------------------------------------------------------- Property editor
    public void SetComponentProperty(string pageId, string componentId, string property, string? value)
    {
        var page = FindPage(pageId);
        var component = page.Components.SingleOrDefault(candidate => candidate.Id == componentId)
            ?? throw new InvalidOperationException("Component not found.");

        // The property editor never lets the user emit an unknown component/version.
        if (!ApplicationComponentCatalog.Components.Contains(component.Type))
        {
            throw new InvalidOperationException($"Unsupported component type '{component.Type}'.");
        }
        if (!string.Equals(component.Version, AllowedVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported component version '{component.Version}'. Only '{AllowedVersion}' is allowed.");
        }

        // Unknown property for this allowlist component is rejected (mirrors the renderer's posture).
        if (!AllowedProperties.TryGetValue(component.Type, out var allowed) || !allowed.Contains(property, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported property '{property}' for component '{component.Type}'.");
        }

        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        RecordHistory();
        if (property == "label") { component.Label = normalized; }
        else if (property == "dataSource")
        {
            // Lenient here (S1 contract): the free-form property-editor path may still carry a
            // reference to an undeclared source, which <see cref="Validate"/> surfaces as
            // UNKNOWN_DATA_SOURCE. The strict "must be declared" gate lives on
            // <see cref="BindComponent"/>, used by the explicit data-binding surface.
            if (component.DataSource is not null && normalized is not null
                && !string.Equals(component.DataSource, normalized, StringComparison.Ordinal))
            {
                component.Mappings.Clear();
            }
            component.DataSource = normalized;
        }
    }

    // ---------------------------------------------------------------- Actions (LC07 S3)
    /// <summary>The closed action catalogue the palette offers (Button-Ausführung, v1.0.0).</summary>
    public static IReadOnlyList<string> GetActionTypes()
        => ApplicationActionCatalog.Types.OrderBy(type => type, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Adds an explicit action to a component. Only closed-catalogue types are accepted;
    /// unknown/unsanctioned types are rejected and never emitted into ContentJson.
    /// </summary>
    public DesignerAction AddAction(string pageId, string componentId, string type)
    {
        var component = FindComponent(pageId, componentId);
        if (!ApplicationActionCatalog.IsKnown(type))
        {
            throw new InvalidOperationException($"Unsupported action type '{type}'. The action palette is a closed catalogue.");
        }
        if (component.Action is not null)
        {
            throw new InvalidOperationException($"Component already has an action of type '{component.Action.Type}'. Remove or edit it first.");
        }
        RecordHistory();
        component.Action = new DesignerAction(type.Trim(), ApplicationActionCatalog.Version, null, []);
        return component.Action;
    }

    public void RemoveAction(string pageId, string componentId)
    {
        var component = FindComponent(pageId, componentId);
        RecordHistory();
        component.Action = null;
    }

    /// <summary>Switches an existing action's type (keeps the task/dataSource/process target and inputs).</summary>
    public DesignerAction SetActionType(string pageId, string componentId, string type)
    {
        var component = FindComponent(pageId, componentId);
        if (component.Action is null)
        {
            throw new InvalidOperationException("The component has no action to edit.");
        }
        if (!ApplicationActionCatalog.IsKnown(type))
        {
            throw new InvalidOperationException($"Unsupported action type '{type}'. The action palette is a closed catalogue.");
        }
        RecordHistory();
        component.Action.Type = type.Trim();
        return component.Action;
    }

    /// <summary>
    /// Binds an action's target through a closed reference (declared data source for
    /// data-source-read; known process KEY from the process catalogue for start-process;
    /// a task key for claim/complete-task). An unknown/undeclared reference is REJECTED at
    /// authoring time - never silently accepted.
    /// </summary>
    public DesignerAction SetActionTarget(string pageId, string componentId, string target)
    {
        var component = FindComponent(pageId, componentId);
        var action = component.Action ?? throw new InvalidOperationException("The component has no action to configure.");
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("An action target is required.");
        }
        var normalized = target.Trim();

        if (action.Type == "data-source-read")
        {
            if (!IsDataSourceDeclared(normalized))
            {
                throw new InvalidOperationException($"Data source '{normalized}' is not declared. Declare it under Data sources before binding.");
            }
        }
        else if (action.Type == "start-process")
        {
            if (!_knownProcessKeys.Contains(normalized))
            {
                throw new InvalidOperationException($"Process '{normalized}' is not a known process definition. Select from the process catalogue.");
            }
        }
        else if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("A task key is required for the task action.");
        }

        // Switching the referenced source/process invalidates stale references.
        RecordHistory();
        if (action.Type == "start-process")
        {
            if (action.Target is not null && !string.Equals(action.Target, normalized, StringComparison.Ordinal))
            {
                _referencedProcessKeys.Remove(action.Target);
            }
            if (!_referencedProcessKeys.Contains(normalized))
            {
                _referencedProcessKeys.Add(normalized);
            }
        }
        action.Target = normalized;
        return action;
    }

    public DesignerAction AddActionInput(string pageId, string componentId, string name, string value)
    {
        var component = FindComponent(pageId, componentId);
        var action = component.Action ?? throw new InvalidOperationException("The component has no action to configure.");
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("An input name is required.");
        }
        if (component.Action.Inputs.Any(input => input.Name == name.Trim()))
        {
            throw new InvalidOperationException($"Input '{name.Trim()}' is already set on the action.");
        }
        var input = new DesignerActionInput(name.Trim(), value ?? string.Empty);
        RecordHistory();
        action.Inputs.Add(input);
        return action;
    }

    public void RemoveActionInput(string pageId, string componentId, int index)
    {
        var component = FindComponent(pageId, componentId);
        var action = component.Action ?? throw new InvalidOperationException("The component has no action to configure.");
        if (index < 0 || index >= action.Inputs.Count)
        {
            throw new InvalidOperationException("Action input not found.");
        }
        RecordHistory();
        action.Inputs.RemoveAt(index);
    }

    public void UpsertEndpoint(ApplicationEndpointDefinition endpoint)
    {
        var endpoints = ApplicationEndpointMetadata.Read(ToJson()).ToList();
        endpoints.RemoveAll(candidate => candidate.Key == endpoint.Key);
        endpoints.Add(endpoint);
        if (endpoint.Permission is not ("ReadOnly" or "ProcessManager" or "AdminOnly")
            || endpoint.Limits.TimeoutSeconds <= 0 || endpoint.Limits.MaxRequestBytes <= 0 || endpoint.Limits.MaxResponseBytes <= 0)
        { throw new VertexBPMN.Domain.Exceptions.ApplicationValidationException("Endpoint permission and positive limits are required."); }
        var candidate = JsonNode.Parse(ToJson())!.AsObject();
        candidate["endpoints"] = JsonSerializer.SerializeToNode(endpoints, EndpointJsonOptions);
        ApplicationComponentCatalog.Validate(candidate.ToJsonString());
        RecordHistory();
        _rootMetadata["endpoints"] = JsonSerializer.SerializeToNode(endpoints, EndpointJsonOptions);
    }

    // ---------------------------------------------------------------- Serialization
    public string ToJson()
    {
        var root = (JsonObject)_rootMetadata.DeepClone();
        if (_referencedProcessKeys.Count > 0)
        {
            var references = new JsonArray();
            foreach (var key in _referencedProcessKeys.OrderBy(key => key, StringComparer.Ordinal))
            {
                references.Add(new JsonObject { ["type"] = "process", ["key"] = key });
            }
            root["references"] = references;
        }
        if (_dataSources.Count > 0)
        {
            var dataSources = new JsonArray();
            foreach (var source in _dataSources)
            {
                var sourceObject = (JsonObject)source.Metadata.DeepClone();
                sourceObject["key"] = source.Key;
                sourceObject["bindingVersion"] = source.BindingVersion;
                sourceObject.Remove("fields");
                if (source.Fields.Count > 0)
                {
                    var fields = new JsonArray();
                    foreach (var field in source.Fields)
                    {
                        fields.Add(field);
                    }
                    sourceObject["fields"] = fields;
                }
                dataSources.Add(sourceObject);
            }
            root["dataSources"] = dataSources;
        }
        var pages = new JsonArray();
        foreach (var page in _pages)
        {
            var pageObject = (JsonObject)page.Metadata.DeepClone();
            pageObject["id"] = page.Id;
            pageObject["route"] = page.Route;
            pageObject["title"] = page.Title;
            var components = new JsonArray();
            foreach (var component in page.Components)
            {
                components.Add(SerializeComponent(component));
            }
            pageObject["components"] = components;
            pages.Add(pageObject);
        }
        root["pages"] = pages;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonObject SerializeComponent(DesignerComponent component)
    {
        var node = (JsonObject)component.Metadata.DeepClone();
        var actionMetadata = node["action"]?.DeepClone() as JsonObject;
        foreach (var property in new[] { "label", "dataSource", "mappings", "action", "children" }) { node.Remove(property); }
        node["type"] = component.Type;
        node["version"] = component.Version;
        if (!string.IsNullOrWhiteSpace(component.Label)) { node["label"] = component.Label; }
        if (!string.IsNullOrWhiteSpace(component.DataSource)) { node["dataSource"] = component.DataSource; }
        if (component.Mappings.Count > 0)
        {
            var mappings = new JsonArray();
            foreach (var mapping in component.Mappings)
            {
                var mappingObject = new JsonObject
                {
                    ["sourcePath"] = mapping.SourcePath,
                    ["targetKey"] = mapping.TargetKey
                };
                if (!string.IsNullOrWhiteSpace(mapping.Transform)) { mappingObject["transform"] = mapping.Transform; }
                if (mapping.Required) { mappingObject["required"] = true; }
                mappings.Add(mappingObject);
            }
            node["mappings"] = mappings;
        }
        if (component.Action is not null)
        {
            var serializedAction = SerializeAction(component.Action);
            if (actionMetadata is not null)
            {
                foreach (var property in new[] { "type", "version", "inputs", "dataSource", "processDefinitionId", "taskKey", "pageId", "operationId" }) { actionMetadata.Remove(property); }
                foreach (var property in serializedAction) { actionMetadata[property.Key] = property.Value?.DeepClone(); }
                serializedAction = actionMetadata;
            }
            node["action"] = serializedAction;
        }
        if (component.Children.Count > 0)
        {
            var children = new JsonArray();
            foreach (var child in component.Children)
            {
                children.Add(SerializeComponent(child));
            }
            node["children"] = children;
        }
        return node;
    }

    private static JsonObject SerializeAction(DesignerAction action)
    {
        var node = new JsonObject
        {
            ["type"] = action.Type,
            ["version"] = action.Version
        };
        if (!string.IsNullOrWhiteSpace(action.Target))
        {
            node[ApplicationActionCatalog.TargetPropertyFor(action.Type)] = action.Target;
        }
        if (action.Inputs.Count > 0)
        {
            var inputs = new JsonObject();
            foreach (var input in action.Inputs)
            {
                inputs[input.Name] = input.Value;
            }
            node["inputs"] = inputs;
        }
        return node;
    }

    private DesignerPage FindPage(string pageId)
        => _pages.SingleOrDefault(page => page.Id == pageId)
           ?? throw new InvalidOperationException("Page not found.");

    private DesignerComponent FindComponent(string pageId, string componentId)
    {
        var page = FindPage(pageId);
        return page.Components.SingleOrDefault(candidate => candidate.Id == componentId)
            ?? throw new InvalidOperationException("Component not found.");
    }

    private static IEnumerable<DesignerComponent> CollectComponents(DesignerPage page)
    {
        foreach (var component in page.Components)
        {
            yield return component;
            foreach (var descendant in CollectComponents(component))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<DesignerComponent> CollectComponents(DesignerComponent component)
    {
        foreach (var child in component.Children)
        {
            yield return child;
            foreach (var descendant in CollectComponents(child))
            {
                yield return descendant;
            }
        }
    }

    private static string Slug(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return $"page-{Guid.NewGuid():N}"[..10];
        }
        var slug = new string(title.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal)) { slug = slug.Replace("--", "-", StringComparison.Ordinal); }
        slug = slug.Trim('-');
        return string.IsNullOrEmpty(slug) ? $"page-{Guid.NewGuid():N}"[..10] : slug;
    }
}
