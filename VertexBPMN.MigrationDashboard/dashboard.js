// --- Tutorials & Docs ---
function showTutorial(topic) {
    const contentDiv = document.getElementById('tutorialContent');
    let html = '';
    switch (topic) {
        case 'quickstart':
            html = `<h3>Quickstart: Migration & Monitoring</h3>
                <ol>
                    <li>Enter source and target process definition IDs.</li>
                    <li>Map activities using JSON.</li>
                    <li>Preview or execute migration.</li>
                    <li>Monitor live engine status in the dashboard.</li>
                </ol>
                <p>See <a href="docs/monitoring.md" target="_blank">Monitoring & Observability</a> for more.</p>`;
            break;
        case 'api':
            html = `<h3>API Reference</h3>
                <ul>
                    <li><b>GET /api/health</b>: Health check</li>
                    <li><b>GET /api/metrics</b>: Engine metrics (JSON)</li>
                    <li><b>GET /api/metrics/prometheus</b>: Prometheus metrics</li>
                    <li><b>GET /api/analytics/events</b>: Recent activity/events</li>
                    <li><b>POST /api/process-migration/plan/execute</b>: Execute migration</li>
                </ul>
                <p>See <a href="docs/openapi.html" target="_blank">OpenAPI Docs</a> for full details.</p>`;
            break;
        case 'bpmn':
            html = `<h3>BPMN Features</h3>
                <ul>
                    <li>Full BPMN 2.0 support</li>
                    <li>Camunda API parity</li>
                    <li>DMN integration</li>
                    <li>Multi-tenancy, simulation, migration tooling</li>
                </ul>
                <p>See <a href="docs/bpmn-features.md" target="_blank">BPMN Features</a> for more.</p>`;
            break;
        case 'observability':
            html = `<h3>Observability & Dashboards</h3>
                <ul>
                    <li>Health checks, metrics, logging out-of-the-box</li>
                    <li>Prometheus & Grafana integration</li>
                    <li>Live monitoring via dashboard UI</li>
                </ul>
                <p>See <a href="docs/monitoring.md" target="_blank">Monitoring Docs</a> for setup and examples.</p>`;
            break;
        case 'faq':
            html = `<h3>FAQ</h3>
                <ul>
                    <li><b>How do I migrate a process?</b> Use the migration form above and execute.</li>
                    <li><b>How do I monitor engine health?</b> See the dashboard and health endpoint.</li>
                    <li><b>Where can I find API docs?</b> See the API Reference tab or OpenAPI docs.</li>
                </ul>`;
            break;
        default:
            html = '<em>Select a topic above.</em>';
    }
    contentDiv.innerHTML = html;
}
document.getElementById('migrationForm').addEventListener('submit', async function(e) {
    e.preventDefault();
    const sourceId = document.getElementById('sourceId').value;
    const targetId = document.getElementById('targetId').value;
    let activityMappings;
    try {
        activityMappings = JSON.parse(document.getElementById('activityMappings').value);
    } catch {
        showResult('Invalid JSON for activity mappings.', true);
        return;
    }
    const plan = {
        sourceProcessDefinitionId: sourceId,
        targetProcessDefinitionId: targetId,
        activityMappings: activityMappings
    };
    const res = await fetch('/api/process-migration/plan/feedback', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(plan)
    });
    const data = await res.json();
    showResult(JSON.stringify(data, null, 2));
});

document.getElementById('executeBtn').addEventListener('click', async function() {
    const sourceId = document.getElementById('sourceId').value;
    const targetId = document.getElementById('targetId').value;
    let activityMappings;
    try {
        activityMappings = JSON.parse(document.getElementById('activityMappings').value);
    } catch {
        showResult('Invalid JSON for activity mappings.', true);
        return;
    }
    const plan = {
        sourceProcessDefinitionId: sourceId,
        targetProcessDefinitionId: targetId,
        activityMappings: activityMappings
    };
    const res = await fetch('/api/process-migration/plan/execute', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(plan)
    });
    const data = await res.json();
    showResult(JSON.stringify(data, null, 2));
});

function showResult(msg, isError) {
    const resultDiv = document.getElementById('result');
    resultDiv.textContent = msg;
    resultDiv.style.color = isError ? 'red' : '#2c3e50';
}

// --- Observability Dashboard ---
async function fetchHealth() {
    try {
        const res = await fetch('/api/health');
        const data = await res.json();
        document.getElementById('healthStatus').innerHTML = `<b>Health:</b> <span style="color:green">${data.status}</span> <small>(${data.timestamp})</small>`;
    } catch {
        document.getElementById('healthStatus').innerHTML = '<b>Health:</b> <span style="color:red">unreachable</span>';
    }
}

async function fetchMetrics() {
    try {
        const res = await fetch('/api/metrics');
        const data = await res.json();
        let html = '<b>Engine Metrics:</b><ul>';
        for (const [key, value] of Object.entries(data)) {
            html += `<li>${key}: <b>${value}</b></li>`;
        }
        html += '</ul>';
        document.getElementById('metrics').innerHTML = html;
    } catch {
        document.getElementById('metrics').innerHTML = '<b>Engine Metrics:</b> <span style="color:red">unavailable</span>';
    }
}

async function fetchRecentActivity() {
    try {
        const res = await fetch('/api/analytics/events');
        const data = await res.json();
        let html = '<b>Recent Activity:</b><ul>';
        for (let i = 0; i < Math.min(data.length, 10); i++) {
            const evt = data[i];
            html += `<li>${evt.EventType} <small>(${evt.Timestamp})</small> <span style="color:#888">Instance: ${evt.ProcessInstanceId}</span></li>`;
        }
        html += '</ul>';
        document.getElementById('recentActivity').innerHTML = html;
    } catch {
        document.getElementById('recentActivity').innerHTML = '<b>Recent Activity:</b> <span style="color:red">unavailable</span>';
    }
}

function refreshDashboard() {
    fetchHealth();
    fetchMetrics();
    fetchRecentActivity();
}

setInterval(refreshDashboard, 5000);
window.addEventListener('DOMContentLoaded', refreshDashboard);
