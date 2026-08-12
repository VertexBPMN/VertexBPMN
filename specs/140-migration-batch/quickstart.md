# Quickstart – Migration/Batch
1) POST /migrations/plans { sourceDefId, targetDefId, rules[] }
2) POST /migrations/batches { planId, concurrency }
3) GET  /migrations/batches/{id} (progress)
4) POST /migrations/batches/{id}/resume | /cancel
