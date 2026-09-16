# Lokaler External Agent: Vertragsprüfung

Der erste konkrete External Agent ist `agent.contract-review` mit dem Profil
`contract-reviewer.v1`. Er analysiert ausschließlich einen synthetischen oder anonymisierten
Vertragstext aus seinem unveränderlichen Job-Snapshot. Das Ergebnis ist eine Voranalyse für
einen nachfolgenden menschlichen User Task und niemals eine automatische Vertragsfreigabe.

## Runtime

Der Worker verwendet das providerneutrale `IAgentRuntime`. Der derzeit einzige echte Adapter
spricht die lokale Ollama-API über das dokumentierte, nicht streamende `POST /api/chat` an.
Für `local-sensitive` werden nur explizite Loopback-Endpunkte mit Port akzeptiert. Redirects
sind deaktiviert und jede aufgelöste Zieladresse wird beim Verbindungsaufbau erneut auf
Loopback geprüft. Der Adapter setzt `think=false`, damit Thinking-fähige Modelle wie Qwen3 das
begrenzte Ausgabetokenbudget nicht vor dem Tool-/Ergebnispfad verbrauchen. Es gibt keinen
Cloud-Fallback.

Minimale lokale Konfiguration; Client-Secret und Modellname sind operatorverwaltete Werte und
gehören nicht in BPMN-XML:

```json
{
  "ExternalTaskWorker": {
    "Enabled": true,
    "BaseAddress": "https://localhost:7026/",
    "TokenEndpoint": "https://localhost:8443/realms/vertex/protocol/openid-connect/token",
    "ClientId": "vertex-contract-reviewer",
    "ClientSecret": "use-user-secrets-or-environment",
    "Scope": "vertexbpmn-api",
    "Topics": [ "agent.contract-review" ],
    "MaxConcurrency": 1,
    "MaxTasksPerClaim": 1,
    "LeaseSeconds": 60,
    "HeartbeatSeconds": 20
  },
  "ContractReviewer": {
    "Enabled": true,
    "Profile": "contract-reviewer.v1",
    "DataClassification": "local-sensitive",
    "Adapter": "ollama",
    "Endpoint": "http://127.0.0.1:11434/",
    "Model": "qwen3:8b"
  }
}
```

Für den Aspire-AppHost im externen WSLC-/Dienstemodus ist der Worker optional. Er wird nur mit
`VertexBPMN:ContractReviewer:Enabled=true` und dem OIDC-Testprofil als eigener Prozess modelliert.
Das Worker-Secret bleibt ein Aspire-Secretparameter:

```powershell
$env:VERTEXBPMN_KEYCLOAK_WORKER_CLIENT_SECRET = '<eigenes starkes Secret>'
./scripts/wslc-apphost.ps1 -OidcTest -EnableContractReviewer
```

Alternativ werden in den AppHost User Secrets `Parameters:oidcWorkerClientSecret` sowie in der
normalen Konfiguration `VertexBPMN:ContractReviewer:TenantId`, `Model` und optional `Endpoint`
gesetzt. Der ausgelieferte Keycloak-Beispielclient `vertexbpmn-contract-reviewer` ist auf
`tenant-a`, Topic `agent.contract-review` und Profil `contract-reviewer.v1` fest begrenzt. Für
weitere Tenants muss jeweils ein eigener Client mit eigenem Secret und exakt passenden Claims
sowie ein passender API-Katalogeintrag angelegt werden.

Die API benötigt zusätzlich einen tenantgebundenen External-Task-Vertrag für Topic und Profil.
Seine Eingaben sind `document`, `documentId`, `documentVersion` und optional
`reviewLanguage`. Die skalaren Ausgaben sind `schemaVersion`, `documentVersion`, `summary`,
`findings`, `uncertainties`, `requiresHumanReview`, `promptVersion` und `documentHash`;
`findings` und `uncertainties` enthalten begrenztes JSON als String, weil der v1-Katalog
absichtlich nur skalare Felder erlaubt.

## Sicherheits- und Qualitätsgrenzen

- Das Modell sieht keine Dateipfade, Shell, allgemeinen HTTP-Client, Credentials oder fremde Dokumente.
- Es kann nur `read_section` und eine literale `search_document`-Suche im aktuellen Jobdokument anfordern.
- Abschnitts-, Such-, Tool-, Laufzeit-, Request-, Response- und Tokenlimits werden im Code erzwungen.
- Abschnitts-ID, Zeilenbereich und wörtliches Zitat jedes Findings werden gegen die tatsächlich gelesenen Daten geprüft.
- Ungültige strukturierte Ausgabe erhält genau einen Korrekturversuch innerhalb desselben Gesamtbudgets.
- `requiresHumanReview=true`, Promptversion und Dokument-Hash werden vom Worker gesetzt, nicht vom Modell.

Die Unit-Abnahme deckt E12 (Prompt-Injection/Berechtigungsausweitung) und E13 (erfundene
Belege, falsche Dokumentversion, ungültige Ausgabe und Budgets) ab. Eine echte Fachabnahme
ist erst mit installiertem Modell und versioniertem Benchmark-Dokumentensatz abgeschlossen.

Der technische 20-Fälle-Entwurf, die vorab festgelegten Schwellen und der Beispielprozess liegen
unter `tests/VertexBPMN.Tests/TestData/ContractReviewBenchmark/v1`. Der lokale Fachlauf wird mit
`scripts/test-contract-review-benchmark-local.ps1` gestartet. Er verweigert die Ausführung, bis
eine fachkundige Vertragsprüferin oder ein fachkundiger Vertragsprüfer die Referenzannotationen
im Manifest namentlich und mit Zeitpunkt freigegeben hat.
