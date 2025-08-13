using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

using VertexBPMN.Core.Services;
using VertexBPMN.Persistence.Services;

namespace VertexBPMN.Api.Controllers
{
        [ApiController]
        [Route("api/[controller]")]
        [Microsoft.AspNetCore.Authorization.Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly IProcessMiningEventSink _eventSink;
        private readonly ProcessMiningEventDbContext _db;

        public AnalyticsController(IProcessMiningEventSink eventSink, ProcessMiningEventDbContext db)
        {
            _eventSink = eventSink;
            _db = db;
        }
    /// <summary>
    /// Returns all process mining events (for analytics, dashboards, export).
    /// </summary>
    [HttpGet("events")]
    public ActionResult<IEnumerable<ProcessMiningEvent>> GetAllEvents()
    {
    // Persistente Events aus DB
    var events = _db.Events.ToList();
    return Ok(events);
    }

    /// <summary>
    /// Returns event type statistics (count per event type).
    /// </summary>
    [HttpGet("event-stats")]
    public ActionResult<IDictionary<string, int>> GetEventTypeStats()
    {
        var stats = _db.Events
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());
        return Ok(stats);
    }

    /// <summary>
    /// Returns the trace (ordered event list) for a given process instance.
    /// </summary>
    [HttpGet("trace/{processInstanceId}")]
    public ActionResult<IEnumerable<ProcessMiningEvent>> GetTrace(string processInstanceId)
    {
        var trace = _db.Events
            .Where(e => e.ProcessInstanceId == processInstanceId)
            .OrderBy(e => e.Timestamp)
            .ToList();
        return Ok(trace);
    }
    /// <summary>
    /// Returns a simple prediction for process duration based on historical traces (stub/demo).
    /// </summary>
    /// <remarks>
    /// Example request:
    ///
    ///     POST /api/analytics/predict-duration
    ///     {
    ///         "traceLengths": [5, 7, 6, 8, 7]
    ///     }
    /// </remarks>
    [HttpPost("predict-duration")]
    public ActionResult<PredictionResult> PredictDuration([FromBody] PredictDurationRequest request)
    {
        // Simple stub: mean + stddev
        var mean = request.TraceLengths.Any() ? request.TraceLengths.Average() : 0;
        var stddev = request.TraceLengths.Any() ? Math.Sqrt(request.TraceLengths.Select(x => Math.Pow(x - mean, 2)).Average()) : 0;
        return Ok(new PredictionResult(mean, stddev));
    }

    /// <summary>
    /// Returns all events for a given tenant (multi-tenancy analytics).
    /// </summary>
    [HttpGet("events/by-tenant/{tenantId}")]
    public ActionResult<IEnumerable<ProcessMiningEvent>> GetEventsByTenant(string tenantId)
    {
        var events = _db.Events.Where(e => e.TenantId == tenantId).ToList();
        return Ok(events);
    }

    /// <summary>
    /// Returns event time series for a given event type (for dashboards).
    /// </summary>
    [HttpGet("events/timeseries/{eventType}")]
    public ActionResult<IEnumerable<object>> GetEventTimeSeries(string eventType)
    {
        var series = _db.Events
            .Where(e => e.EventType == eventType)
            .GroupBy(e => e.Timestamp.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToList();
        return Ok(series);
    }

    /// <summary>
    /// Returns process metrics (count, average duration, etc.) for analytics.
    /// </summary>
    [HttpGet("metrics/process")]
    public ActionResult<object> GetProcessMetrics()
    {
        var processGroups = _db.Events
            .Where(e => e.EventType == "ProcessStarted" || e.EventType == "ProcessEnded")
            .GroupBy(e => e.ProcessInstanceId)
            .ToList()
            .Select(g => {
                var started = g.FirstOrDefault(e => e.EventType == "ProcessStarted")?.Timestamp;
                var ended = g.FirstOrDefault(e => e.EventType == "ProcessEnded")?.Timestamp;
                return new {
                    ProcessInstanceId = g.Key,
                    Started = started,
                    Ended = ended
                };
            })
            .Where(x => x.Started != null && x.Ended != null)
            .ToList();
        var count = processGroups.Count;
        var avgDuration = processGroups.Any() ? processGroups.Average(x => (x.Ended - x.Started)?.TotalSeconds ?? 0) : 0;
        return Ok(new { Count = count, AvgDurationSeconds = avgDuration });
    }
    }

    public record PredictDurationRequest(List<int> TraceLengths);
    public record PredictionResult(double Mean, double StdDev);
}
