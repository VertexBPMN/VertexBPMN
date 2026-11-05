// offline-task-queue.js - VertexBPMN portal client glue (LC11 / LC-AC-19).
//
// The queue persists ONLY offline user-task completion drafts (taskId + variables + the
// idempotency key + the revision/schema guard) in localStorage. It NEVER caches authenticated responses, tokens, or sensitive business data. Reconnect replays the drafts through
// the endpoint below and surfaces per-item conflicts to the UI instead of silently
// overwriting newer server state. Server replay + conflict semantics live in
// OfflineCompletionReplayService / OfflineController.
(function () {
    "use strict";

    var QUEUE_KEY = "vertexbpmn.offline.completions";
    var REPLAY_URL = "/api/offline/completions/replay";

    function readQueue() {
        try {
            var parsed = JSON.parse(localStorage.getItem(QUEUE_KEY) || "[]");
            return Array.isArray(parsed) ? parsed : [];
        } catch (_) {
            return [];
        }
    }

    function writeQueue(items) {
        try {
            localStorage.setItem(QUEUE_KEY, JSON.stringify(items));
        } catch (_) {
            // Storage full/unavailable: keep the draft in memory for this page only.
        }
    }

    // Enqueues a completion draft while offline. Each item captures the idempotency key and
    // the frozen schema/revision guard so a reconnect replay keeps the task at-most-once and
    // never overrides newer server state.
    function enqueue(item) {
        var items = readQueue();
        items.push(item);
        writeQueue(items);
    }

    function replay() {
        var items = readQueue();
        if (!items.length) {
            return Promise.resolve();
        }
        return fetch(REPLAY_URL, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            credentials: "same-origin",
            body: JSON.stringify({ items: items })
        }).then(function (res) { return res.json(); })
            .then(function (body) {
                var remaining = [];
                var conflicts = [];
                (body.results || []).forEach(function (result, index) {
                    if (result.status === "Completed" || result.status === "Forbidden") {
                        return; // applied (or nothing to apply) - draft is settled
                    }
                    if (result.status === "Conflict") {
                        // Never auto-overwrite: hand the draft to the conflict surface.
                        conflicts.push(items[index]);
                        return;
                    }
                    remaining.push(items[index]);
                });
                writeQueue(remaining);
                if (conflicts.length) {
                    onConflict(conflicts);
                }
            });
    }

    // Default conflict surface: dispatch a DOM event the Blazor shell can render a visible
    // banner from. Consumers may override to show the stale draft without applying it.
    function onConflict(items) {
        window.dispatchEvent(new CustomEvent("vertexbpmn:offline-conflict", { detail: items }));
    }

    if (typeof window !== "undefined") {
        window.addEventListener("online", replay);
        window.VertexBpmnOfflineQueue = { enqueue: enqueue, replay: replay };
    }
})();
