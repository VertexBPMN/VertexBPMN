using Microsoft.AspNetCore.Components;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Studio.Components.Modeling;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "The Razor compiler generates the public component contract for this partial class.")]
public sealed partial class AppDesignerSurface : IDisposable
{
    private string? _loadedTenantId;
    private string? _loadedApplicationId;
    private int _contextGeneration;
    private bool _disposed;
    [Parameter] public string? ApplicationId { get; set; }

    private AppDesignerDocument _document = AppDesignerDocument.Empty;
    private StudioApplicationDraft? _draft;
    private string _baselineJson = string.Empty;
    private string? _selectedPageId;
    private string? _selectedComponentId;
    private string _newPageTitle = string.Empty;
    private bool _saving;
    private bool _publishing;
    private bool _dirty;
    private string? _error;
    private string? _unsupportedMessage;

    private DesignerPage? _selectedPage
    {
        get
        {
            if (_selectedPageId is null) { return null; }
            foreach (var page in _document.Pages)
            {
                if (page.Id == _selectedPageId) { return page; }
            }
            return null;
        }
    }

    private DesignerComponent? _selectedComponent
    {
        get
        {
            var page = _selectedPage;
            if (page is null || _selectedComponentId is null) { return null; }
            foreach (var component in page.Components)
            {
                if (component.Id == _selectedComponentId) { return component; }
            }
            return null;
        }
    }

    // ---- LC07 S2: data sources + data binding + field mapping -----------------------------
    private string _newDataSourceKey = string.Empty;
    private int _newDataSourceBindingVersion = 2;
    private string _newDataSourceFields = string.Empty;
    private string? _mappingSource;
    private string? _mappingTarget;
    private IReadOnlyList<string> _knownProcessKeys = [];

    // ---- LC07 S3: action (Button-Ausführung) -------------------------------------------------
    private string? _newActionType;
    private string? _newActionTarget;
    private string _newActionInputName = string.Empty;
    private string _newActionInputValue = string.Empty;
    private string? _actionEditorMessage;

    private IReadOnlyList<string>? SchemaFor(DesignerComponent component)
        => string.IsNullOrWhiteSpace(component.DataSource) ? null : _document.GetDataSourceFields(component.DataSource);

    private bool CanMap(DesignerComponent component)
        => !string.IsNullOrWhiteSpace(component.DataSource) && _document.IsDataSourceDeclared(component.DataSource);

    private async Task AddDataSourceAsync()
    {
        try
        {
            var fields = _newDataSourceFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _document.AddDataSource(_newDataSourceKey, _newDataSourceBindingVersion, fields);
            _newDataSourceKey = string.Empty;
            _newDataSourceFields = string.Empty;
            _newDataSourceBindingVersion = 2;
            MarkDirty();
        }
        catch (Exception exception)
        {
            Snackbar.Add(exception.Message, MudBlazor.Severity.Error);
        }
    }

    private async Task RemoveDataSourceAsync(string key)
    {
        try
        {
            _document.RemoveDataSource(key);
            MarkDirty();
        }
        catch (Exception exception)
        {
            Snackbar.Add(exception.Message, MudBlazor.Severity.Error);
        }
    }

    private async Task OnBindChangedAsync(string componentId, string? sourceKey)
    {
        if (_selectedPage is null || string.IsNullOrWhiteSpace(sourceKey)) { return; }
        try
        {
            _document.BindComponent(_selectedPage.Id, componentId, sourceKey);
            _mappingSource = null;
            _unsupportedMessage = null;
            MarkDirty();
        }
        catch (Exception exception)
        {
            // Binding to an undeclared source is rejected at authoring time.
            _unsupportedMessage = exception.Message;
        }
    }

    private async Task AddMappingAsync(string componentId)
    {
        if (_selectedPage is null || string.IsNullOrWhiteSpace(_mappingSource) || string.IsNullOrWhiteSpace(_mappingTarget)) { return; }
        try
        {
            _document.MapField(_selectedPage.Id, componentId, _mappingSource, _mappingTarget, "Copy", false);
            _mappingSource = null;
            _mappingTarget = null;
            _unsupportedMessage = null;
            MarkDirty();
        }
        catch (Exception exception)
        {
            _unsupportedMessage = exception.Message;
        }
    }

    private async Task RemoveMappingAsync(string componentId, int index)
    {
        if (_selectedPage is null) { return; }
        try
        {
            _document.RemoveFieldMapping(_selectedPage.Id, componentId, index);
            MarkDirty();
        }
        catch (Exception exception)
        {
            Snackbar.Add(exception.Message, MudBlazor.Severity.Error);
        }
    }

    private bool HasValidationIssues => _document.Validate().Count > 0;

    // ---- LC07 S4: undo/redo + preview ------------------------------------------------------
    private bool CanUndo => _document.CanUndo && !_saving;
    private bool CanRedo => _document.CanRedo && !_saving;

    private async Task UndoAsync()
    {
        if (!_document.Undo()) { return; }
        MarkDirty();
        await InvokeAsync(StateHasChanged);
    }

    private async Task RedoAsync()
    {
        if (!_document.Redo()) { return; }
        MarkDirty();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnKeyDownAsync(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs args)
    {
        if ((args.CtrlKey || args.MetaKey) && args.Key.Equals("z", StringComparison.OrdinalIgnoreCase))
        {
            if (args.ShiftKey) { await RedoAsync(); }
            else { await UndoAsync(); }
        }
        else if ((args.CtrlKey || args.MetaKey) && args.Key.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            await RedoAsync();
        }
    }

    private bool _previewOpen;
    private string? _previewTarget;
    private AppPreview? _preview;
    private string? _previewMessage;
    private string _previewError = string.Empty;

    private List<string> PreviewTargetOptions
    {
        get
        {
            var sources = _document.DataSources.Select(source => source.Key).ToList();
            return sources.Count > 0 ? sources : ["standard"];
        }
    }

    private void OpenPreview()
    {
        _previewOpen = true;
        _preview = null;
        _previewTarget = PreviewTargetOptions.Count > 0 ? PreviewTargetOptions[0] : null;
        _previewMessage = null;
        _previewError = string.Empty;
    }

    private void ClosePreview() => _previewOpen = false;

    /// <summary>
    /// Builds the read-only preview against the EXPLICITLY selected test target using
    /// REDACTED TEST DATA. No save, no start, no claim/complete, no external action is fired.
    /// </summary>
    private void RunPreview()
    {
        if (string.IsNullOrWhiteSpace(_previewTarget))
        {
            _previewMessage = "Bitte wählen Sie ein explizites Testziel (Datensatz).";
            return;
        }
        try
        {
            var tenantId = _loadedTenantId ?? TenantContext.CurrentTenantId ?? "current";
            _preview = AppDesignerPreview.Build(_document.ToJson(), new PreviewTestTarget(tenantId, _previewTarget));
            _previewError = string.Empty;
            _previewMessage = null;
        }
        catch (Exception exception)
        {
            _previewError = exception.Message;
            _preview = null;
        }
    }


    // ---- LC07 S3: action editor ------------------------------------------------------------
    private static bool IsTaskAction(DesignerAction action)
        => action.Type is "claim-task" or "complete-task";

    /// <summary>The closed reference targets for an action type: declared data sources for
    /// data-source-read, the OPEN process catalogue keys for start-process, and a free-form
    /// task key (the runtime task) for claim/complete-task.</summary>
    private IReadOnlyList<string>? ActionTargetChoices(DesignerAction action)
    {
        if (action.Type == "data-source-read") { return _document.DataSources.Select(s => s.Key).ToList(); }
        if (action.Type == "start-process") { return _knownProcessKeys; }
        return null; // task actions accept a free-form task key
    }

    private async Task AddActionAsync(string componentId)
    {
        if (_selectedPage is null || string.IsNullOrWhiteSpace(_newActionType)) { return; }
        try
        {
            _document.AddAction(_selectedPage.Id, componentId, _newActionType);
            _actionEditorMessage = null;
            MarkDirty();
        }
        catch (Exception exception)
        {
            _actionEditorMessage = exception.Message;
        }
    }

    private async Task SetActionTargetAsync(string componentId, string? target)
    {
        if (_selectedPage is null || string.IsNullOrWhiteSpace(target)) { return; }
        try
        {
            _document.SetActionTarget(_selectedPage.Id, componentId, target);
            _newActionTarget = target;
            _actionEditorMessage = null;
            MarkDirty();
        }
        catch (Exception exception)
        {
            // Bind a reference that does not exist (undeclared source / unknown process) is
            // REJECTED at authoring time - never silently accepted.
            _actionEditorMessage = exception.Message;
        }
    }

    private async Task AddActionInputAsync(string componentId)
    {
        if (_selectedPage is null || string.IsNullOrWhiteSpace(_newActionInputName)) { return; }
        try
        {
            _document.AddActionInput(_selectedPage.Id, componentId, _newActionInputName, _newActionInputValue);
            _newActionInputName = string.Empty;
            _newActionInputValue = string.Empty;
            _actionEditorMessage = null;
            MarkDirty();
        }
        catch (Exception exception)
        {
            _actionEditorMessage = exception.Message;
        }
    }

    private async Task RemoveActionInputAsync(string componentId, int index)
    {
        if (_selectedPage is null) { return; }
        try
        {
            _document.RemoveActionInput(_selectedPage.Id, componentId, index);
            MarkDirty();
        }
        catch (Exception exception)
        {
            _actionEditorMessage = exception.Message;
        }
    }

    private async Task RemoveActionAsync(string componentId)
    {
        if (_selectedPage is null) { return; }
        _document.RemoveAction(_selectedPage.Id, componentId);
        _newActionType = null;
        _newActionTarget = null;
        _newActionInputName = string.Empty;
        _newActionInputValue = string.Empty;
        _actionEditorMessage = null;
        MarkDirty();
    }

    protected override void OnInitialized() => TenantContext.Changed += TenantChanged;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedApplicationId == ApplicationId && _loadedTenantId == TenantContext.CurrentTenantId) { return; }
        var generation = ++_contextGeneration;
        _loadedApplicationId = ApplicationId;
        _loadedTenantId = TenantContext.CurrentTenantId;
        _draft = null;
        _document = AppDesignerDocument.Empty;
        _dirty = false;
        _saving = false;
        _error = null;
        _selectedPageId = null;
        _selectedComponentId = null;
        if (string.IsNullOrWhiteSpace(_loadedTenantId) || string.IsNullOrWhiteSpace(_loadedApplicationId))
        {
            _error = "A tenant and an application are required. Select a tenant, then open an application from the Applications page.";
            return;
        }
        try
        {
            var draft = await ApplicationService.GetDraftAsync(_loadedApplicationId, _loadedTenantId);
            if (_disposed || generation != _contextGeneration) { return; }
            if (draft is null)
            {
                _error = "The application draft could not be found.";
                return;
            }
            _draft = draft;
            _document = AppDesignerDocument.Parse(draft.Content);
            _baselineJson = _document.ToJson();
            _dirty = false;
            _selectedPageId = _document.Pages.Count > 0 ? _document.Pages[0].Id : null;
            // Load the OPEN process catalogue so start-process actions resolve their reference
            // target by stable KEY from a real catalogue (never a hand-typed GUID).
            try
            {
                var definitions = await Repository.GetProcessDefinitionsAsync(_loadedTenantId);
                if (_disposed || generation != _contextGeneration) { return; }
                _knownProcessKeys = definitions.Select(d => d.Key).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
                _document.SetProcessCatalog(_knownProcessKeys);
            }
            catch
            {
                // The process catalogue is an enhancement; a failed fetch leaves start-process
                // targets simply unresolved (UNKNOWN_PROCESS) rather than breaking the designer.
                _knownProcessKeys = [];
            }
        }
        catch (Exception exception)
        {
            if (!_disposed && generation == _contextGeneration) { _error = $"The draft could not be loaded: {exception.Message}"; }
        }
    }

    private void TenantChanged(object? sender, EventArgs args)
    {
        ++_contextGeneration;
        _draft = null;
        _document = AppDesignerDocument.Empty;
        _dirty = false;
        _saving = false;
        _selectedPageId = null;
        _selectedComponentId = null;
        _error = "Tenant changed. Open an application from the Applications page.";
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        _disposed = true;
        ++_contextGeneration;
        TenantContext.Changed -= TenantChanged;
    }

    private void SelectPage(string pageId)
    {
        _selectedPageId = pageId;
        _selectedComponentId = null;
    }

    private async Task AddPageAsync()
    {
        try
        {
            var page = _document.AddPage(_newPageTitle);
            _newPageTitle = string.Empty;
            _selectedPageId = page.Id;
            _selectedComponentId = null;
            MarkDirty();
        }
        catch (Exception exception)
        {
            Snackbar.Add(exception.Message, MudBlazor.Severity.Error);
        }
    }

    private async Task RemovePageAsync(string pageId)
    {
        _document.RemovePage(pageId);
        if (_selectedPageId == pageId) { _selectedPageId = null; }
        _selectedComponentId = null;
        MarkDirty();
    }

    private async Task MovePageAsync(string pageId, int toIndex)
    {
        _document.MovePage(pageId, toIndex);
        MarkDirty();
    }

    private async Task AddComponentAsync(string type)
    {
        if (_selectedPage is null) { return; }
        try
        {
            var component = _document.AddComponent(_selectedPage.Id, type);
            _selectedComponentId = component.Id;
            MarkDirty();
        }
        catch (Exception exception)
        {
            Snackbar.Add(exception.Message, MudBlazor.Severity.Error);
        }
    }

    private void SelectComponent(string componentId) => _selectedComponentId = componentId;

    private async Task RemoveComponentAsync(string componentId)
    {
        if (_selectedPage is null) { return; }
        _document.RemoveComponent(_selectedPage.Id, componentId);
        if (_selectedComponentId == componentId) { _selectedComponentId = null; }
        MarkDirty();
    }

    private async Task MoveComponentAsync(string componentId, int toIndex)
    {
        if (_selectedPage is null) { return; }
        _document.MoveComponent(_selectedPage.Id, componentId, toIndex);
        MarkDirty();
    }

    private async Task OnPropertyChangedAsync(string componentId, string property, string? value)
    {
        if (_selectedPage is null) { return; }
        try
        {
            _document.SetComponentProperty(_selectedPage.Id, componentId, property, value);
            _unsupportedMessage = null;
            MarkDirty();
        }
        catch (InvalidOperationException exception)
        {
            // The property editor mirrors the renderer's posture: unsupported input is
            // flagged and never applied to the document.
            _unsupportedMessage = exception.Message;
        }
    }

    private async Task SaveAsync()
    {
        if (!_dirty || _draft is null || _saving || _loadedTenantId != TenantContext.CurrentTenantId) { return; }
        if (string.IsNullOrWhiteSpace(_loadedTenantId)) { Snackbar.Add("Select a tenant before saving.", MudBlazor.Severity.Warning); return; }
        var generation = _contextGeneration;
        var draft = _draft;
        var tenantId = _loadedTenantId;
        _saving = true;
        try
        {
            var content = _document.ToJson();
            await ApplicationService.SaveDraftAsync(draft.Id, tenantId, draft.Key, draft.Name, draft.Version, content);
            if (_disposed || generation != _contextGeneration) { return; }
            _draft = draft with { Version = draft.Version + 1 };
            _baselineJson = content;
            MarkDirty();
            Snackbar.Add($"Draft saved as revision {_draft.Version}.", MudBlazor.Severity.Success);
        }
        catch (Exception exception)
        {
            if (!_disposed && generation == _contextGeneration) { Snackbar.Add($"Draft save failed: {exception.Message}", MudBlazor.Severity.Error); }
        }
        finally
        {
            if (!_disposed && generation == _contextGeneration) { _saving = false; }
        }
    }

    private void MarkDirty()
    {
        _dirty = _document.ToJson() != _baselineJson;
    }

    private async Task PublishAsync()
    {
        if (_draft is null || _publishing || _saving || _loadedTenantId != TenantContext.CurrentTenantId) { return; }
        if (string.IsNullOrWhiteSpace(_loadedTenantId)) { Snackbar.Add("Select a tenant before publishing.", MudBlazor.Severity.Warning); return; }
        // Ensure the latest authoring content is persisted before releasing it.
        if (_dirty) { await SaveAsync(); }
        if (_draft is null) { return; }
        var generation = _contextGeneration;
        var draft = _draft;
        var tenantId = _loadedTenantId;
        _publishing = true;
        try
        {
            // Veröffentlichen: turns the current revision into a released revision the
            // portal's released reader serves; the designer preview stays a separate path.
            await ApplicationService.PublishAsync(draft.Id, tenantId, draft.Version);
            if (_disposed || generation != _contextGeneration) { return; }
            Snackbar.Add($"Revision {draft.Version} veröffentlicht.", MudBlazor.Severity.Success);
        }
        catch (Exception exception)
        {
            if (!_disposed && generation == _contextGeneration) { Snackbar.Add($"Veröffentlichen fehlgeschlagen: {exception.Message}", MudBlazor.Severity.Error); }
        }
        finally
        {
            if (!_disposed && generation == _contextGeneration) { _publishing = false; }
        }
    }

    private void ExitToApplications() => Navigation.NavigateTo("/applications");

    private static string? ValueFor(DesignerComponent component, string property)
    {
        if (property == "label") { return component.Label; }
        if (property == "dataSource") { return component.DataSource; }
        return null;
    }
}
