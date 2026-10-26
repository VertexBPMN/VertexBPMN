using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Domain.Model.Runtime;

/// <summary>Contract template only; no Node-RED runtime or token storage.</summary>
public sealed class ExternalTaskNodeSession(ExternalTaskLease lease, string effectName = "primary")
{
    private bool _effectConfirmed;
    private bool _leaseLost;
    private readonly Guid _completionId = Guid.NewGuid();
    private readonly Guid _failureId = Guid.NewGuid();
    public ExternalTaskLease Lease { get; } = lease;
    public string EffectName { get; } = effectName;
    public string EffectKey => ExternalTaskEffectKey.For(Lease.ActivityExecutionId, EffectName);

    /// <summary>Call only after the independent effect owner has durably acknowledged this key.</summary>
    public bool ConfirmBusinessEffect(string effectKey)
    {
        _effectConfirmed = string.Equals(effectKey, EffectKey, StringComparison.Ordinal);
        return _effectConfirmed;
    }

    public void LoseLease() => _leaseLost = true;

    public ExternalTaskFailCommand Fail(string kind, string code)
    {
        EnsureLease();
        if (kind == "business")
        {
            using var schema = JsonDocument.Parse(Lease.SchemaSnapshot);
            if (!schema.RootElement.TryGetProperty("businessErrors", out var errors)
                || !errors.EnumerateArray().Any(item => item.GetString() == code))
            {
                throw new InvalidOperationException("business_error_not_allowed");
            }
        }

        return new ExternalTaskFailCommand(Lease.LeaseId, Lease.LeaseGeneration, _failureId, kind, code);
    }

    public ExternalTaskHeartbeatCommand Heartbeat(int leaseSeconds = 60)
    {
        EnsureLease();
        return new ExternalTaskHeartbeatCommand(Lease.LeaseId, Lease.LeaseGeneration, leaseSeconds);
    }

    private void EnsureLease()
    {
        if (_leaseLost || Lease.LeaseId == Guid.Empty)
        {
            throw new InvalidOperationException("lease_lost");
        }
    }

    public ExternalTaskCompleteCommand Complete(JsonElement result)
    {
        EnsureLease();

        if (!_effectConfirmed)
        {
            throw new InvalidOperationException("business_effect_unconfirmed");
        }

        return new ExternalTaskCompleteCommand(Lease.LeaseId, Lease.LeaseGeneration, _completionId, result.Clone());
    }
}
