namespace VertexBPMN.Application.Lc06;

/// <summary>
/// LC06 reference application: a hand-written, released low-code application used as the
/// ontology for the LC-AC-08 acceptance. Its <see cref="ContentJson"/> is expressed in the
/// exact schema the platform persists (an <c>ApplicationRevisionRecord</c> pinned by a
/// released release candidate) and the Portal BFF serves (the released revision), and it is
/// authored entirely from the allowlist renderer's known component set (table, detail-card,
/// text, form-js, button, status-diagram, page-container; version 1.0.0). It declares
/// dataSources, a sanctioned data-source-read action and a start/claim/complete task flow.
///
/// The same fixture is shared by the released-revision seed
/// (<c>ReferenceApplicationSeeder</c>) and the LC06 tests, so the acceptance runs against the
/// precise payload the platform would serve.
/// </summary>
public static class Lc06ReferenceApp
{
    /// <summary>Tenant the reference application (and its reference process) is seeded into.</summary>
    public const string TenantId = "vertexbpmn-reference";

    /// <summary>Unique application key (TenantId+Key is unique).</summary>
    public const string Key = "lc06-reference-app";

    /// <summary>Human-readable application name.</summary>
    public const string Name = "LC06 Reference Application";

    /// <summary>The reference process started by the application's start-process action.</summary>
    public const string ProcessId = "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d";

    /// <summary>A data source that is declared AND referenced by a sanctioned data-source-read action.</summary>
    public const string SanctionedBinding = "invoiceList";

    /// <summary>A data source that is declared but NOT referenced by any sanctioned read (the narrow gate rejects it).</summary>
    public const string UnreferencedBinding = "claimsOpen";

    /// <summary>A data source that is neither declared nor referenced (the narrow gate rejects it).</summary>
    public const string ArbitraryBinding = "arbitraryRawQuery";

    /// <summary>The authored, released application payload (the LC-AC-08 ontology).</summary>
    public static string ContentJson => """
        {
          "localizations": {
            "en": { "appTitle": "LC06 Reference Application", "appSubtitle": "Ontology for the released low-code runtime" }
          },
          "references": [
            { "type": "process", "id": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d", "tenantId": "vertexbpmn-reference" }
          ],
          "dataSources": [
            { "key": "invoiceList", "bindingVersion": 2 },
            { "key": "claimsOpen", "bindingVersion": 2 }
          ],
          "pages": [
            {
              "id": "home",
              "route": "/home",
              "navigation": ["tasks"],
              "actions": [
                { "type": "data-source-read", "version": "1.0.0", "dataSource": "invoiceList" },
                { "type": "start-process", "version": "1.0.0", "processDefinitionId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d" }
              ],
              "components": [
                {
                  "type": "page-container",
                  "version": "1.0.0",
                  "children": [
                    { "type": "text", "version": "1.0.0" },
                    { "type": "table", "version": "1.0.0", "dataSource": "invoiceList" },
                    { "type": "detail-card", "version": "1.0.0", "dataSource": "invoiceList" },
                    { "type": "status-diagram", "version": "1.0.0" },
                    { "type": "button", "version": "1.0.0", "label": "Claim task", "action": { "type": "claim-task", "version": "1.0.0" } },
                    { "type": "button", "version": "1.0.0", "label": "Complete task", "action": { "type": "complete-task", "version": "1.0.0" } }
                  ]
                }
              ]
            },
            {
              "id": "tasks",
              "route": "/tasks",
              "navigation": ["settings"],
              "components": [
                { "type": "form-js", "version": "1.0.0" },
                { "type": "button", "version": "1.0.0", "label": "Claim task", "action": { "type": "claim-task", "version": "1.0.0" } },
                { "type": "button", "version": "1.0.0", "label": "Complete task", "action": { "type": "complete-task", "version": "1.0.0" } },
                { "type": "status-diagram", "version": "1.0.0" }
              ]
            },
            {
              "id": "settings",
              "route": "/settings",
              "components": [
                { "type": "detail-card", "version": "1.0.0" },
                { "type": "text", "version": "1.0.0" }
              ]
            }
          ]
        }
        """;
}
