using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

/// <summary>
/// Persists process mining events (one-by-one). For high throughput scenarios,
/// replace with a buffered/batched implementation.
/// </summary>
public sealed class PersistentProcessMiningEventSink : IProcessMiningEventSink
{
    private readonly ProcessMiningEventDbContext _db;
    private readonly ILogger<PersistentProcessMiningEventSink> _logger;

    public PersistentProcessMiningEventSink(ProcessMiningEventDbContext db,
                                            ILogger<PersistentProcessMiningEventSink> logger)
    {
        _db = db;
        _logger = logger;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("S2139", "S2139:Exceptions should be used to identify and handle failures, not silently rethrown", Justification = "This catch logs comprehensive context and then rethrows the original exception unchanged so callers observe the real failure type; no custom persistence exception type exists and wrapping would alter callers' error handling.")]
    public async ValueTask<ProcessMiningEvent> EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (string.IsNullOrWhiteSpace(evt.EventType))
		{
			throw new ArgumentException("EventType is required", nameof(evt));
		}

		if (string.IsNullOrWhiteSpace(evt.ProcessInstanceId))
		{
			throw new ArgumentException("ProcessInstanceId is required", nameof(evt));
		}

		// Ensure timestamp set
		if (evt.Timestamp == default)
		{
			evt.Timestamp = DateTimeOffset.UtcNow;
		}

		try
        {
            await _db.Events.AddAsync(evt, cancellationToken).ConfigureAwait(false);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Persisted mining event Type={Type} ProcInst={Pid} Id={Id}",
                    evt.EventType, evt.ProcessInstanceId, evt.Id);
            }
            return evt;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist mining event Type={Type} ProcInst={Pid}", evt.EventType, evt.ProcessInstanceId);
            // The original exception must propagate unchanged so callers observe the real failure;
            // comprehensive context is logged above for diagnostics.
            throw;
        }
    }
}
