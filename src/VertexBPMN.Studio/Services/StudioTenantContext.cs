namespace VertexBPMN.Studio.Services;

internal sealed class StudioTenantContext
{
    public string? CurrentTenantId { get; private set; }

    public event EventHandler? Changed;
    public event EventHandler? TenantsChanged;

    public void NotifyTenantsChanged() => TenantsChanged?.Invoke(this, EventArgs.Empty);

    public void SetTenant(string? tenantId)
    {
        var normalizedTenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
        if (string.Equals(CurrentTenantId, normalizedTenantId, StringComparison.Ordinal))
        {
            return;
        }

        CurrentTenantId = normalizedTenantId;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
