
# Process Mining & Analytics API (Production)

VertexBPMN exposes advanced process mining and analytics endpoints for dashboards, export, and integration with tools like Celonis, Power BI, and Camunda Optimize.

## Endpoints

### List All Process Mining Events
- **GET** `/api/analytics/events`
- Returns all process mining events (raw event log).

### Event Type Statistics
- **GET** `/api/analytics/event-stats`
- Returns a dictionary of event type counts (e.g., how many TaskCompleted, ProcessStarted, etc.).

### Process Instance Trace
- **GET** `/api/analytics/trace/{processInstanceId}`
- Returns the ordered event trace for a specific process instance.

### Predictive Analytics (Demo)
- **POST** `/api/analytics/predict-duration`
- Request: `{ "traceLengths": [5, 7, 6, 8, 7] }`
- Response: `{ "mean": 6.6, "stdDev": 1.02 }`

## Event Types
- `ProcessStarted`, `ProcessEnded`, `ProcessSuspended`, `ProcessResumed`, `ProcessSignaled`
- `TaskClaimed`, `TaskCompleted`, `TaskDelegated`
- `IncidentCreated`, `IncidentResolved`

## Example: Event Log Entry
```json
{
  "eventType": "TaskCompleted",
  "processInstanceId": "...",
  "taskId": "...",
  "userId": "demo",
  "tenantId": "default",
  "timestamp": "2025-08-12T12:34:56Z",
  "payload": { "duration": 1234 }
}
```

## Integration
- Export event logs for process mining tools (CSV, JSON, direct API integration)
- Use `/api/analytics/event-stats` for dashboards and KPIs
- Use `/api/analytics/trace/{processInstanceId}` for trace analysis and conformance checking

---
See the main README and OpenAPI docs for more details.
