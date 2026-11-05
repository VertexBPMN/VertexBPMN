# LC09B / LC-AC-14 — S1 Checkpoint

Basis: `564dab74cf4164d4bff58cc923874b7008b84a13`, uncommittiert.

## Ergebnis und Plandifferenz
Typisierte Endpoint-Metadaten werden im vorhandenen tenantbezogenen ContentJson-Entwurf und unveränderlichen Revisionssnapshot gespeichert. Keine neue Tabelle/Migration/API-Route. Neue Tests verwenden bestehende echte SQLite-Migrationen, Wiederöffnung und den realen ApplicationController. Endpoint-Bindung muss eine vorhandene lokale Seiten-/Komponentenaktion mit bekanntem Typ, Version und exaktem Target treffen. Geschlossener erster Methodenbereich GET/POST; nur relative `/endpoints/{key}/...`-Pfade mit exakt erklärten Parametern. Dies registriert keine ausführbare HTTP-Route.

Der Plan schlägt eine öffentliche `/api/apps/{appKey}/endpoints/{endpointKey}`-Grenze vor. S1 speichert ausschließlich relative Metadaten dafür. Freigegeben bedeutet hier **im bestehenden App-Aktionskatalog erlaubt und in dieser App erklärt**, nicht bereits veröffentlicht oder zur Ausführung berechtigt.

## TDD-Evidenz
Logs: `/home/azureuser/.hermes/cache/scratch/lc09b-s1/`.
- RED1: `Assert.Single` leere Endpoint-Sicht; Total 1/Failed 1/Errors 0/Skipped 0.
- GREEN1: Roundtrip Total 1/Failed 0/Errors 0/Skipped 0.
- RED2: `Assert.ThrowsAsync<ApplicationValidationException>` keine Exception bei unbekannter/fremder Bindung; 3/3/0/0.
- GREEN2: Bindungsprüfung 3/0/0/0.
- RED3: dieselbe Throws-Assertion bei nicht freigegebener Methode/Route; 5/5/0/0.
- GREEN3: Routenprüfung 5/0/0/0.
Je Theorie genau ein Ablehnungsverhalten; kein Compilerfehler als RED. Build-/Runner-Setupfehler getrennt geloggt. Zusätzliche Feld-/Tenant-/HTTP-Assertions sind Bestands-/Integrationsevidenz, keine weiteren RED-Zyklen.

Build: `dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj -c Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers` (SDK unter `$HOME/.dotnet`), kompletter Dependency-Build, 0 Warnungen/0 Fehler.

GREEN-Befehle jeweils `tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests -class 'VertexBPMN.Tests.Unit.Infrastructure.ApplicationEndpointRoundtripTests'` (GREEN1), entsprechend `ApplicationEndpointBindingTests` (GREEN2) und `ApplicationEndpointRouteTests` (GREEN3). Dazwischen Domain-Build und Testbuild mit `BuildProjectReferences=false`; abschließend vollständiger Dependency-Build.

Final direkt: `tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests -class 'VertexBPMN.Tests.Unit.Infrastructure.ApplicationEndpoint*' -class 'VertexBPMN.Tests.Integration.Applications.ApplicationEndpointApiTests'`: **10/0/0/0** (Total/Failed/Errors/Skipped), `checkpoint-final.log`.
Breitere App-/Release-/Controller-Regressionsauswahl: **89/2/0/0**, `regression-valid.log`. Bestehende Fehler in `PersistentApplicationValidationTests.Save_RejectsEmptyTenantAndOversizedContent` und `Save_RejectsMalformedMetadataWithExplicitError`: schon in HEAD parst `EnrichContentReferencesAsync` vor der Validierungs-/Exceptiongrenze. Nicht nebenbei verändert. Erster finaler Filter mit Wildcard in der Mitte war ungültig (Exit 3, keine Testevidenz); korrigierter Filter tatsächlich ausgeführt.

## Grenzen und nächster einzelner Checkpoint
CI-safe exakt gemäß `.github/workflows/ci.yml` ausgeführt (`ci-safe.log`): Total **2514**, Failed **22**, Succeeded **2486**, Skipped **6**, Exit **2**. MTP weist keine separate Errors-Zahl aus. 15 Fehler verlangen `LC09_TEST_CONNECTION` (bestehende externe LC09A-Kategorie nicht vom Workflow ausgeschlossen); übrige Fehler betreffen zwei SourceControl-Provider-Migrationstests, zwei Studio-Task-HTTP-Tests, einen StudioFullSupport-Acceptance-Test und die beiden oben genannten App-Validierungstests. Keine Behebung außerhalb S1, kein grünes Gesamtgate behauptet.
Clean-Code-Prüfung: 12 eigene C#-Dateien, D1–D7 und manuelle D8-Prüfung; Cancellation weitergereicht, materialisierte Endpoint-Sicht, keine neuen Interfaces/Vererbung/Modeflags. Keine bestätigten Clean-Code-Befunde; Schema-/Permission-/Limitqualifikation ausdrücklich offen. Eigene SQLite-Dateien/WAL/SHM nach Tests entfernt; keine Container oder Live-Portressourcen angelegt/geändert. `git diff --check` sauber, keine Commits/Pushes.
Keine Runtimeausführung, Schemavalidierung von Aufrufdaten, aktive Permission-Prüfung, Limitdurchsetzung, allgemeine asynchrone Operation, OIDC oder Browserabnahme. Schema/Permission/Limits sind vorerst typisierte gespeicherte Metadaten. `.http`-Katalog `application-endpoint-metadata.http` erstellt, nicht im Visual-Studio-Editor ausgeführt; AC22 offen.
Nächster **einzelner** Checkpoint: eine veröffentlichte GET-Endpoint-Bindung serverseitig auflösen und schema-geprüfte echte Testdaten begrenzt lesen; manipulierte Parameter vor Zugriff verweigern. Danach separat releasegebundener Prozessstart; dauerhafte 202-Operation + tenant-/objektgeschützter Status; Timeout/Größen/Parallelität/Cancellation; LC07 AppDesignerDocument/UndoRedo/HttpApplicationService mit realem Browser-Save/Reload. Kein LC09-/AC14-Abschluss; Flow-Editor/Palette und offener LC09A Release-ID-Filter bleiben außerhalb.
