# LC09B-S2 — veröffentlichte GET-Runtime (Teilcheckpoint)

Basis `564dab74cf4164d4bff58cc923874b7008b84a13`, uncommittiert; S1 erhalten.

## Ergebnis und Plandifferenz
`GET /api/apps/{appKey}/endpoints/{endpointKey}/{**path}` löst ausschließlich den claimgebundenen Tenant auf. LC10 Active-Revision hat Vorrang; ohne aktive LC10-Generation bleibt der vorhandene LC05/07 ReleasedRevision-Pfad kompatibel. Kein Draft-Fallback. Route wird segmentweise gegen S1-Metadaten geprüft; lokale S1-Aktion muss exakt passen.

Ein Datenquellen-Eintrag `{key:"invoices",bindingVersion:2,query:"personal-task-inbox"}` bindet die vorhandene `IPersonalTaskService.QueryInboxAsync`-Query. Nur der benannte Stringparameter `search` ist in diesem ersten freigegebenen Queryvertrag ausführbar, auch hinter statischen Routensegmenten. Keine freie URL, kein SQL, keine Credentialauflösung. Tenant-/Assignee-/Candidatefilter bleiben in der bestehenden Query. Antwort minimiert auf `{items:[{id,name}]}`, PageSize 20.

Der vorhandene `ReleasedDataSourceReader` war nur ein Bindungsgate mit Status/RowCount 0, KEIN realer Datenreader. Dieses Gate wird wiederverwendet; S2 ergänzt den einen bestehenden Inbox-Datenzugriff, nicht einen erfundenen Kunden-/SQL-Dienst. Der Planreferenzfall Kunden-Test-API ist damit noch NICHT abgenommen. Die Datenquelle ist hier ein persistierter Eintrag im veröffentlichten App-Snapshot, keine neue eigenständige DataSourceRevision-Tabelle.

Input und Output verwenden einen ausdrücklich geschlossenen JSON-Schema-Teilumfang: object/properties/required, string min/maxLength, integer/number minimum/maximum, boolean, enum, array/items/maxItems; unbekannte Keywords abgelehnt, keine entfernten Referenzen. Unbekannte Felder werden immer abgelehnt, auch wenn additionalProperties nicht false ist. Kein allgemeiner JSON-Schema-Draft-Nachweis; Pfadwerte bleiben Strings. Schemafehler 400/502, keine Teilantwort. Endpoint-Permission nutzt vorhandene ReadOnly/ProcessManager/AdminOnly-Policies. Antwortbytes werden vor Versand geprüft.

## Evidenz
Logs und nach jedem GREEN aktualisierte `progress.json`: `/home/azureuser/.hermes/cache/scratch/lc09b-s2/`.
Echte SQLite-Migrationen, persistierte UserTask-Zeilen, reale MVC-API über TestServer. Testprincipals sind nur Contractnachweis, KEIN OIDC-/Kestrel-/Browsernachweis. Keine Container oder Live-Ports verwendet.

### Ausgeführte RED → GREEN
Counts Total/Failed/Errors/Skipped. Jeder RED **1/1/0/0**:
1. `Assert.Equal("Invoice Alpha", rowNameOrStatus)` → NotFound; GREEN **1/0/0/0**.
2. `Assert.Equal(BadRequest, status)` bei 33 Zeichen → OK; GREEN **2/0/0/0**.
3. `Assert.Equal(BadGateway, status)` string-ID gegen integer-Output → OK; GREEN **3/0/0/0**.
4. `Assert.Equal(Forbidden, status)` ReadOnly gegen ProcessManager → OK; GREEN **4/0/0/0**.
5. `Assert.Equal(NotFound, status)` bindingVersion 999 → OK; GREEN **12/0/0/0**.
6. `Assert.Single(items)` hinter `/by-name/{search}` → leer; GREEN **13/0/0/0**.
7. `Assert.Equal(OK, status)` Endpoint nur in aktiver LC10-Revision 2 → NotFound; GREEN **15/0/0/0**.
8. `Assert.Null(failure)` kapselt 404-Vertrag für `{other}` → gefangene KeyNotFoundException; GREEN **16/0/0/0**. Erster uncaught Versuch getrennt `red8.log`, Assertion-RED `red8b.log`.
9. `Assert.Equal(BadGateway, status)` MaxResponseBytes 1 → OK; GREEN **18/0/0/0**.

Direkte RED-Befehle: `tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests -method '*<Methodenname>'`, Namen siehe Testdatei/logs; RED7 `-class 'VertexBPMN.Tests.Integration.Applications.ApplicationEndpointActiveReleaseTests'`, RED9 entsprechend `ApplicationEndpointLimitTests`.
GREEN1 gleicher Methodenfilter; GREEN2–4 `-class 'VertexBPMN.Tests.Integration.Applications.ApplicationEndpointRuntimeTests'`; GREEN5–6 `-class 'VertexBPMN.Tests.Integration.Applications.ApplicationEndpointRuntime*'`; GREEN7–9 `-class 'VertexBPMN.Tests.Integration.Applications.ApplicationEndpoint*'`. Wildcards nur am Ende/Anfang, keine leeren Selektionen. Zusätzliche Boundary-Theorien sind Bestands-/Negativregression, keine zusätzlichen RED-Behauptungen.

Build jeweils API-Projekt mit `-c Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers`; danach Testbuild `BuildProjectReferences=false` bei neuen Tests. Zwischen kurzen GREEN-Runs aktuelles API-DLL in Testoutput kopiert. Final voller Dependency-Build: `$HOME/.dotnet/dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj -c Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers` → **0 Warnungen/0 Fehler**. CS0117 im Fixture-Setup und CA1861 beim GREEN8-Aufbau sind dokumentierte Buildfehler, kein RED.

Final direkt (AppHost-Testbinary, nicht Aspire-Orchestrator): `tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests -class 'VertexBPMN.Tests.Integration.Applications.ApplicationEndpoint*' -class 'VertexBPMN.Tests.Unit.Infrastructure.ApplicationEndpoint*' -result-xml /home/azureuser/.hermes/cache/scratch/lc09b-s2/final.xml` → **27/0/0/0**. Breitere direkte Regression (ApplicationController*, ApplicationReleaseServiceTests, PersistentApplicationDefinition*, PersonalTaskServiceTests, BpmnCoreLifecycleContractTests): **86/0/0/0**, `regression.log`.

CI-safe exakt Workflow: **2531 Total, 40 Failed, 2485 Succeeded, 6 Skipped**, Exit 2; MTP meldet keine getrennte Errors-Zahl. 40 Fehler programmatisch gezählt, keine Endpoint-Fehler. 18 zusätzliche Fehler gegenüber S1 betreffen doppelte PostgreSQL-/SQLite-Provider im Hostsetup, kein Endpoint-Aufruf; die BpmnCoreLifecycle-Klasse separat grün. 15 LC09A-Fälle benötigen externe Verbindung; übrige sieben entsprechen den S1-Befunden. Kein grünes Gesamtgate behauptet; vollständiger HEAD-Baselinevergleich der 18 Providerfehler hier NICHT durchgeführt.

Clean-Code D1–D7 mit Python-RegEx-Fallback und D8-Lesung, 10 C#-Dateien: keine bestätigten Maintainability-Befunde. Cancellation an DB/Query weitergereicht; Framework-MVC-Get und Testnamen ohne Async-Suffix sind Ausnahmen. Bool publish ist benannte Fixture-Steuerung. Keine neuen Interfaces, keine eigene Vererbung außer MVC/Disposal. Sicherheits-/Vollständigkeitsgrenzen unten bleiben davon unabhängig. `dotnet format whitespace ... --verify-no-changes --include <3 Produktionsdateien>` ist **NICHT grün** (Tabs/Inline-Braces; `format-check.log`); kein Formatter-Write ausgeführt. `git diff --check` sauber.

## Geänderte Pfade / Grenzen
Neu: `src/VertexBPMN.Api/Controllers/ApplicationEndpointController.cs`, `Services/EndpointJsonSchema.cs`, `Services/EndpointPathParameters.cs`, `Http/application-endpoint-runtime.http`; sechs Dateien unter `tests/VertexBPMN.Tests/Integration/Applications/` (RuntimeTests, RuntimeBoundaryTests, ActiveReleaseTests, LimitTests, RuntimeContent, RuntimeFixture). S1 `Unit/Infrastructure/EndpointCheckpointStore.cs` erweitert um wählbaren Scratchpfad; bestehende Aufrufer bleiben unverändert. Dieser Bericht neu. Keine Commits/Pushes/Stages; Supabase-Artefakte unangetastet. Eigene SQLite/WAL/SHM nach Tests entfernt (verifiziert: keine endpoint-*.db* in S2).

S2 ist ein Teilnachweis für AC14, KEIN LC09-/AC14-Abschluss. Offen: tatsächlicher Designer-Save/Reload/Publish (AppDesignerDocument + HttpApplicationService + Browser), Kunden-Test-API-Datenquelle, allgemeine Schemaqualifikation/Malformed-Metadaten-Fehlergrenze, Timeout-/MaxRequestBytes-/Parallelitätsdurchsetzung und Cancellation-Lastnachweis. Die vorhandene Inbox-Query begrenzt die ausgegebene Seite, materialisiert intern jedoch die tenantbezogenen offenen Tasks; keine neue Skalierungsaussage. Für die lange Aktion fehlen releasegebundener Prozessstart, durable ApplicationOperation mit Idempotenz/Recovery und tenant-/objektgeschützter Status samt 202-Referenz; hier kein Prozess gestartet. `.http` erstellt und mit MVC-Route abgeglichen, nicht im Visual-Studio-HTTP-Editor ausgeführt; AC22 offen.

Nächster **EINZELNER** Checkpoint: S2-Härtung des synchronen GET-Vertrags (Timeout/MaxRequestBytes, fail-closed malformed Schema/Binding, Check-only-Formatgate), vor der separat folgenden releasegebundenen Prozessstart-/202-Statuskette. Kein Studio-/Floweditor in diesem Schritt.
