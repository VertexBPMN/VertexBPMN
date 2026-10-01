namespace VertexBPMN.Studio.Services;

public sealed class StudioTenantContext
{
    public string? CurrentTenantId { get; private set; }

    public event Action? Changed;
    public event Action? TenantsChanged;

    public void NotifyTenantsChanged() => TenantsChanged?.Invoke();

    public void SetTenant(string? tenantId)
    {
        var normalizedTenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
        if (string.Equals(CurrentTenantId, normalizedTenantId, StringComparison.Ordinal))
        {
            return;
        }

        CurrentTenantId = normalizedTenantId;
        Changed?.Invoke();
    }
}
