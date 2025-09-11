namespace VertexBPMN.Core.Scripting;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Jint;


/// <summary>
/// Führt ScriptTasks aus: C# via Roslyn (default) und optional JavaScript via Jint.
/// Ergebnis (return) wird in die resultVariable geschrieben, zusätzlich kann das Script direkt die 'variables' Map ändern.
/// Warning: Roslyn/Jint sind keine harte Sandbox — fremden Code nur in isolierten Umgebungen ausführen.
/// </summary>
public static class ScriptTaskExecution
{
    /// <summary>
    /// Versucht, ein BpmnTask des Typs "scriptTask" auszuführen.
    /// Erwartet in task.Attributes:
    ///   - "scriptFormat": z. B. "C#" (Default) oder "JavaScript"
    ///   - "script": Script-Quelltext (aus <script>..</script> oder extensionElements)
    ///   - "resultVariable": optionaler Name der Prozessvariable für Rückgabewert
    /// </summary>
    public static async Task<bool> TryHandleScriptTaskAsync(
        VertexBPMN.Core.Bpmn.BpmnTask task,
        IDictionary<string, object> processVariables,
        CancellationToken ct = default)
    {
        if (!string.Equals(task.Type, "scriptTask", StringComparison.OrdinalIgnoreCase))
            return false;

        if (task.Attributes == null)
            throw new InvalidOperationException($"ScriptTask '{task.Id}' ohne Attributes.");

        task.Attributes.TryGetValue("script", out var script);
        if (string.IsNullOrWhiteSpace(script))
            throw new InvalidOperationException($"ScriptTask '{task.Id}' hat kein Script (attributes['script']).");

        var format = "C#";
        if (task.Attributes.TryGetValue("scriptFormat", out var fmt) && !string.IsNullOrWhiteSpace(fmt))
            format = fmt;

        object? result;
        if (format.Equals("JavaScript", StringComparison.OrdinalIgnoreCase))
        {
            result = ExecuteJavaScript(script!, processVariables);
        }
        else
        {
            // Default: C#
            result = await ExecuteCSharpAsync(script!, processVariables, ct).ConfigureAwait(false);
        }

        if (task.Attributes.TryGetValue("resultVariable", out var rv) && !string.IsNullOrWhiteSpace(rv))
        {
            processVariables[rv!] = result!;
        }

        return true;
    }

    // ---------------- C# / Roslyn ----------------
    public sealed class Globals
    {
        public IDictionary<string, object> variables { get; }
        public Globals(IDictionary<string, object> vars) => variables = vars;
    }

    private static readonly ScriptOptions RoslynOptions = ScriptOptions.Default
        .AddReferences(typeof(object).Assembly)
        .AddImports("System", "System.Collections.Generic");

    private static Task<object?> ExecuteCSharpAsync(string code, IDictionary<string, object> variables, CancellationToken ct)
    {
        // Hinweis: Roslyn ist keine Sicherheits-Sandbox. Untrusted Code nur in isolierten Umgebungen ausführen.
        return CSharpScript.EvaluateAsync<object?>(code, RoslynOptions, globals: new Globals(variables), cancellationToken: ct);
    }

    // ---------------- JavaScript / Jint ----------------
    private static object? ExecuteJavaScript(string code, IDictionary<string, object> variables)
    {
        // Einfache Ressourcenbegrenzungen; passe bei Bedarf an.
        var engine = new Engine(cfg => cfg.Strict().LimitMemory(8_000_000).TimeoutInterval(TimeSpan.FromSeconds(2)));
        engine.SetValue("variables", variables);
        var eval = engine.Evaluate(code);
        return eval.ToObject();
    }
}
