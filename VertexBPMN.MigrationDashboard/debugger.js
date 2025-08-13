document.getElementById('debugForm').addEventListener('submit', async function(e) {
    e.preventDefault();
    const instanceId = document.getElementById('instanceId').value;
    const res = await fetch(`/api/visual-debugger/instance/${instanceId}/state`);
    const data = await res.json();
    showDebugState(data);
});

document.getElementById('stepBtn').addEventListener('click', async function() {
    const instanceId = document.getElementById('instanceId').value;
    const res = await fetch(`/api/visual-debugger/instance/${instanceId}/step`, {
        method: 'POST'
    });
    const data = await res.json();
    showDebugState(data);
});

function showDebugState(state) {
    const stateDiv = document.getElementById('debugState');
    stateDiv.innerHTML = `<pre>${JSON.stringify(state, null, 2)}</pre>`;
    // Advanced features
    if (typeof showTokens === 'function') showTokens(state.Tokens);
    if (typeof showVariables === 'function') showVariables(state.Variables);
    if (typeof showMultiInstance === 'function') showMultiInstance(state.MultiInstances);
    // BPMN rendering
    if (state.BpmnXml && state.CurrentActivityId) {
        if (typeof renderBpmnDiagram === 'function') {
            renderBpmnDiagram(state.BpmnXml, state.CurrentActivityId);
        }
    } else {
        document.getElementById('diagramOverlay').innerHTML = '<div style="padding:20px;color:#888;">[No BPMN diagram available]</div>';
    }
// Export button handler
document.getElementById('exportBtn').addEventListener('click', function() {
    if (typeof exportDebuggerState === 'function') {
        const state = JSON.parse(document.getElementById('debugState').textContent);
        exportDebuggerState(state);
    }
});
}
