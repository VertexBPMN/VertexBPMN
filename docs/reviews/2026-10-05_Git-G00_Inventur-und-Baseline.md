# Git-Modellversionierung – G00: Inventur und Baseline

Stand: 2026-10-05. Basiscommit: `cb88bee0f83807cd0d7906a4d092807a56b49969`.
Branch: `codex/git-source-control-phase-1`.

**Status: Inventur und verfügbare lokale Baseline abgeschlossen; OpenAPI und Speicherprofilierung korrigiert, Solution-Build und CI-safe Suite erfolgreich. Externe Infrastruktur-/Git-Abnahmen bleiben unqualifiziert.**
Dieser Bericht ist kein Git-Feature- oder Produktionsfreigabenachweis.

## 1. Ausgangszustand und Grenzen

- Vor Umsetzung: `master...origin/master`, keine getrackten Änderungen. Einzige ungetrackte Datei war der zuvor erstellte Git-Implementierungsplan; dieser bleibt erhalten.
- Eigener Branch angelegt; kein Commit, Push, PR, Fetch oder externes Git-Schreiben ausgeführt.
- Windows/PowerShell. SDK tatsächlich `10.0.303`, zulässig über `global.json` (`10.0.302`, `latestPatch`). Git `2.56.0.windows.1`, Pfad `C:\Program Files\Git\cmd\git.exe`.
- `git`, `dotnet`, `wsl` und ein `wslc`-Befehl sind auflösbar. Das beweist keine WSLC-Backendverfügbarkeit. Keine Containeroperation durchgeführt.
- Beim read-only Portcheck keine Listener an 5263/7027/55432/5432/55672/5672 festgestellt. Kein echter Studio-/DB-/Broker-Test gestartet; Linux-Git weder aufgerufen noch qualifiziert.
- Zunächst ausschließlich Restore/Build des unveränderten Produktcodes; danach Phase-1-Vertragsbibliothek. Keine Runtime-, Controller-, Plugin-, Editor- oder Konfigurationsänderung.

## 2. Wiederverwendbare Einstiegspunkte und belegte Lücken

| Bereich | Tatsächlicher Pfad | Befund / Konsequenz |
| --- | --- | --- |
| Source Control | Repo-weite Suche nach `SourceControl`, `GitRepository` vor neuen Dateien | Kein bestehender Produktprovider identifiziert; keine zweite vorhandene Git-Implementierung übergehen |
| Entwürfe/ETag | Repo-weite Suche nach `Draft`, `ETag`, `If-Match` in C#/Razor | Nur OAuth2-UI-Draft, kein persistenter Modell-Draft-/ETag-Vertrag gefunden; G03/G06 müssen eigene Dokumentsitzung liefern oder später hinzugekommenen Pfad wiederverwenden |
| Aktive BPMN-API | `src/VertexBPMN.Api/Controllers/RepositoryController.cs` | Tenant-/Policyprüfung, normaler Deploy; anschließend separate Webhook-Synchronisation und Einmal-Secret-Header |
| Aktiver Deploydienst | `src/VertexBPMN.Application/ApplicationModule.cs:43`, `RepositoryService.cs` | `IRepositoryService` ist Application-Service; Parser/Deploymentvalidator/externe Taskverträge wiederverwenden |
| Definitionen | `src/VertexBPMN.Infrastructure/InfrastructureModule.cs:74`, `Persistence/Repositories/ProcessDefinitionRepository.cs` | Add speichert per `SaveChangesAsync`; keine Git-Provenienz-/Operation-ID vorhanden |
| Versionsschutz | `src/VertexBPMN.Application/RepositoryService.cs`, `src/VertexBPMN.Infrastructure/Persistence/BpmnDbContext.cs:288` | `latest.Version + 1`, Unique-Index `(TenantScope, Key, Version)` schützt vor Dubletten, bietet aber keinen erfolgreichen Konkurrenz-Retry oder idempotenten Deploy |
| Deploy-Idempotenz | Repositoryservice/-controller | Neue Deployment-/Definitions-GUID pro Aufruf; Git-Idempotenz darf nicht durch existierende Runtime-Inbox suggeriert werden |
| Webhook-Grenze | `src/VertexBPMN.Application/WorkflowTriggerService.cs:104`, `Persistence/Repositories/WorkflowTriggerRepository.cs` | Eigene Saves nach Definition; gemeinsames Git-Deploy/Provenienz/Trigger-Recovery erst in G08 |
| Jobs/Leases | `src/VertexBPMN.Infrastructure/Persistence/Repositories/JobRepository.cs` | Atomare Lease über Revision + `ExecuteUpdateAsync`; Update nutzt Concurrency-Token. Wiederverwendbares Muster, nicht bereits Git-Jobstore |
| Inbox-Idempotenz | `Persistence/BpmnDbContext.cs:571`, `Messaging/RuntimeInboxProcessor.cs` | Eindeutiger Tenant/Operation/Idempotenzschlüssel und Wiederanlauf vorhanden; Gitoperationen nicht ohne eigenen Vertrag in Runtime-Dispatch einschleusen |
| Credentials | `InfrastructureModule.cs:126`, `Persistence/Services/PersistentCredentialService.cs` | Tenantbezogene Metadaten/Secretauflösung, Data-Protection-geschützte Werte; G02 ergänzt Git-/Installationstokenadapter |
| Rollen/Tenant | `src/VertexBPMN.Api/Security/SecurityConfiguration.cs:127`, `src/VertexBPMN.ServiceDefaults/Security/VertexOidcClaims.cs` | Admin/ProcessManager/ReadOnly/ExternalTaskWorker vorhanden; kein Repository-ACL-Vertrag |
| Pluginladung | `src/VertexBPMN.Api/Plugins/{IPlugin,PluginServiceContainer,PluginSecurityManager,PluginAssemblyLoadContext}.cs` | API-Kopplung, eigenes Serviceregister, pauschale Sicherheitsablehnung und fehlender Resolver; unverändert lassen |
| Editorvergleich | `src/VertexBPMN.Studio/Components/Pages/BpmnModelerPage.razor:578` | Vergleicht normalisierte XML-Zeilen per `Except`; Reihenfolge/Dubletten können unsichtbar sein. Kein semantischer Diff, nicht als Git-Konfliktlösung verwenden |
| Export | `src/VertexBPMN.Application/ModelExportRedaction.cs`, `RepositoryService.cs` | Strukturierte Credential-Redaktion, Marker verhindert Deploy; opaque Skriptinhalte nicht vollständig erkennbar. Git darf keine Redaktionshintertür bilden |
| Persistenzprofile | `src/VertexBPMN.Infrastructure/InfrastructureModule.cs:230` | SQLite/PostgreSQL/SQL Server vorhanden; zusätzlich bestehender InMemory-Fallback. Neue dauerhafte Git-Writes brauchen relationalen Store, kein eigener InMemory-Produktpfad |
| Lokale Profile | `src/VertexBPMN.AppHost/AppHost.cs:22`, `appsettings.Wslc.json`, `deploy/compose/README.md` | Container-/ExternalServices-/WSLC-Profile vorhanden; nicht ändern oder Git voraussetzen, solange Integration deaktiviert |
| Tests | `tests/VertexBPMN.Tests`, `tests/VertexBPMN.Studio.UiTests`, `scripts/test-studio-e2e.ps1` | MTP/xUnit v3; Browserkategorie `LocalStudioE2E`, Existing/Wslc-Modi; kein Lauf ohne Infrastruktur behaupten |

Die beobachteten Grenzen sind Arbeitsvoraussetzungen für G02–G08, keine Nebenauftragserlaubnis zum breiten Umbau des Bestandsprodukts.

## 3. Baseline-Befehle und Ergebnisse

### Restore vor Vertragsänderungen

```powershell
dotnet restore VertexBPMN.sln --disable-parallel
```

Exit 0. NU1608-Warnungen: `Microsoft.AspNetCore.OpenApi 10.0.12` verlangt `Microsoft.OpenApi >= 2.12.0 && < 3.0.0`, tatsächlich wird `3.10.2` aufgelöst.

### Unveränderter Produkt-Build

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Exit 1; 15 Warnungen, 2 Fehler, 61,12 Sekunden. API-Sourcegenerator:

```text
OpenApiXmlCommentSupport.generated.cs(597,41): CS0200: IOpenApiMediaType.Example is read only
OpenApiXmlCommentSupport.generated.cs(659,41): CS0200: IOpenApiMediaType.Example is read only
```

Ursächliche Paketgrenze: `src/VertexBPMN.Api/VertexBPMN.Api.csproj` referenziert `Microsoft.OpenApi 3.10.2`, inkompatibel zur aufgelösten ASP.NET-OpenAPI-Version. Generierten Code nicht editieren und XML-Kommentargenerator nicht zum Verbergen des Fehlers deaktivieren. Paketkompatibilität separat am ursprünglichen Quell-/API-Vertrag reparieren und dann die ganze Baseline erneut ausführen.

Weitere Bestandswarnungen: veraltete Azure-Credential-APIs, CS8600 im RabbitMQ-Inboxconsumer, doppelte CLI-using-Direktive und xUnit-Warnungen. Keine davon in Phase 1 verändert.

### Zentrale Tests und lokale UI

- CI-safe Suite **nicht ausgeführt**: kein aktueller erfolgreicher Solution-/API-/Testbuild. Vorhandene API-/Test-DLLs stammen vom 2026-10-01; `--no-build` dagegen wäre keine aktuelle Baseline.
- Browser-/PostgreSQL-/RabbitMQ-/HTTPS-Git-/GitHub-Abnahme **nicht ausgeführt**. Kein Dienst-/Containerstart im Inventurpaket.
- Linux-Git- und Mehrreplika-Tests **nicht ausgeführt**.

## 4. Übergabe an G01

G01 konnte auf den statischen Befunden aufgebaut und unabhängig von der bestehenden API-Inkompatibilität geprüft werden. Das schließt die projektweite Baseline nicht rückwirkend.

Ergebnis und Tests der neuen Verträge: [G01-Vertrag](2026-10-05_Git-G01_Vertraege-und-Sicherheitsgrenzen.md).
Gesamtplan: [Git-Implementierungsplan](2026-10-05_Git-Modellversionierung_Implementierungsplan.md).

## 5. Autorisierte OpenAPI-Korrektur und erneute Baseline

Die Ergebnisse in Abschnitt 3 dokumentieren den ursprünglichen Zustand, nicht den aktuellen Buildstand.

- `src/VertexBPMN.Api/VertexBPMN.Api.csproj`: direkte Referenz von `Microsoft.OpenApi 3.10.2` auf `2.12.0` korrigiert. ASP.NET OpenAPI 10.0.12 benötigt laut Paketmetadaten `[2.12.0, 3.0.0)`; Swashbuckle Swagger 10.2.3 akzeptiert diese Version. XML-Dokumentation und Generator bleiben aktiv.
- `src/VertexBPMN.Api/Program.cs`: Bearer-Schemareferenz an das erzeugte OpenAPI-Dokument gebunden. Ohne Dokument wurde die Sicherheitsanforderung als leeres Objekt serialisiert.
- `tests/VertexBPMN.Tests/Integration/Api/OpenApiCompatibilityTests.cs`: echte Swagger-HTTP-Antwort über Testhost/SQLite geprüft, einschließlich Repository-Deploymentvertrag, XML-Kommentar, Bearer-Schema und aufgelöster Sicherheitsanforderung. Keine Authentifizierungsregeln geändert.

Aktuelle Ergebnisse nach den Quelländerungen:

| Prüfung | Ergebnis |
| --- | --- |
| Solution-Restore wie Abschnitt 3 | Exit 0; OpenAPI-NU1608 beseitigt |
| Solution-Releasebuild wie Abschnitt 3 | Exit 0; 0 Fehler, 56 Bestandswarnungen; 22,22 s |
| `OpenApiCompatibilityTests`, Release, `--no-build --no-restore` | 1 bestanden, 0 fehlgeschlagen/übersprungen; 5,887 s |
| CI-safe Suite, unveränderte Ausschlüsse aus `.github/workflows/ci.yml`, Schritt `Test (CI-safe suite)` | 1.285 gesamt: 1.276 bestanden, 1 fehlgeschlagen, 8 übersprungen; Exit 1; 84,943 s |
| `BpmnEditorInsertionTests`, Release, `VERTEXBPMN_EDITOR_TESTS=1` | 1 bestanden, 0 fehlgeschlagen/übersprungen; 15,171 s |
| `BpmnModeler_ImportExport_Roundtrip_And_VersionCompare_Work_InBrowser`, Release | 1 bestanden, 0 fehlgeschlagen/übersprungen; 12,912 s |
| `MemoryProfiler_LargeModel_SnapshotsMemoryUsage` isoliert, Release | 1 bestanden, 0 fehlgeschlagen/übersprungen; 1,458 s |

Gezielte Läufe verwenden `dotnet test <Testprojekt> --configuration Release --no-build --no-restore --max-parallel-test-modules 1` mit `--filter-class` bzw. `--filter-method`. Editor-Insertion ist eine isolierte Chromium-/Asset-Vertragsprüfung; Import/Export/Versionsvergleich nutzt den echten Studio-Testhost mit **Stub-API**. Keine echte PostgreSQL-/RabbitMQ-Gesamtabnahme oder Remote-Git-Abnahme daraus ableiten.

Der Suitefehler betrifft `tests/VertexBPMN.Tests/Parsing/Hardening/Phase11HardeningTests.cs:166`: `MemoryProfiler_LargeModel_SnapshotsMemoryUsage` überschreitet die 500-MB-Grenze. `src/VertexBPMN.Engine/Security/BpmnMemoryProfiler.cs` misst mit `GC.GetTotalMemory(false)` den **prozessweiten verwalteten Heap**, nicht ausschließlich den Parser. Der erfolgreiche isolierte Lauf spricht für Beeinflussung durch den gemeinsamen Testprozess; er beweist keine genaue Zuordnung der verursachenden Allokationen. Der fehlgeschlagene Gesamtlauf bleibt fehlgeschlagen. Keine Grenze erhöht, Assertion entfernt oder Workflow-Ausnahme hinzugefügt.

Die acht ausgelassenen Fälle benötigen explizite Azure-Data-Protection-, PostgreSQL- oder Live-TypeSafe-Voraussetzungen; sie sind kein Nachweis externer Abnahme. Speicherprofilierung und reproduzierbare Messisolation müssen separat geklärt werden. Kein Commit/Push/PR ausgeführt.

Der obige Gesamtlauf dokumentiert den Zwischenstand vor der folgenden autorisierten Korrektur.

## 6. Speicherprofilierung korrigiert und Gesamtbaseline abgeschlossen

- `src/VertexBPMN.Engine/Security/BpmnMemoryProfiler.cs`: Messumfang ausdrücklich prozessweiter verwalteter Heap, keine Parser-exklusive Messung. Prozessweiter Allokationszähler statt threadlokalem Zähler über `await`; abschließende Heapwerte gehen in das gesampelte Maximum ein. Tracker wird über Cancellation und `finally` beendet und abgewartet, auch bei Parserfehlern. Optionaler CancellationToken wird an den Parser weitergereicht; Modell bleibt während der Retained-Messung lebendig.
- `src/VertexBPMN.Domain/Model/Security/MemoryProfileSnapshot.cs`: Messumfang und Samplinggrenze dokumentiert.
- `tests/VertexBPMN.Tests/Parsing/Hardening/Phase11HardeningTests.cs` und `MemoryProfilerLifecycleTests.cs`: Collection `ProcessMemoryMeasurements` verhindert gleichzeitige Ausführung anderer Testcollections während prozessweiter Heapmessungen. Keine globale Parallelitätsabschaltung und keine Änderung der intern parallelen Stress-/Parserfälle. Die vorhandenen 500-MB-, Optimierungs- und übrigen Assertions bleiben unverändert. Hintergrundarbeit außerhalb der Tests kann weiterhin prozessweite Messungen beeinflussen; dies ist kein Allokationsnachweis einzelner Parserobjekte.
- Neue Regressionen: konkreter SecurityException-Pfad mit anschließend erfolgreichem Profil sowie vorab abgebrochener Aufruf. Der `finally`-Join ist im Quellcode geprüft; diese Tests allein beweisen nicht jeden möglichen asynchronen Abbruchzeitpunkt.

Nach allen C#-Änderungen ausgeführt:

| Prüfung | Ergebnis |
| --- | --- |
| Solution-Releasebuild, Befehl aus Abschnitt 3 | Exit 0, 0 Fehler, 59 Warnungen; 11,59 s. Davon drei xUnit-Cancellation-Hinweise durch den neuen optionalen Token an bestehenden Profileraufrufen; keine warnungsfreie Baseline behaupten. |
| `--filter-class '*MemoryProfilerLifecycleTests'` | 2 bestanden, 0 fehlgeschlagen/übersprungen; 2,359 s |
| `--filter-method '*MemoryProfiler_*'` | Beide bisherigen Profilerfälle bestanden, 0 fehlgeschlagen/übersprungen; 1,557 s |
| Unveränderte CI-safe Suite aus Abschnitt 5 | **1.287 gesamt, 1.279 bestanden, 0 fehlgeschlagen, 8 übersprungen; Exit 0; 75,879 s** |

Die acht expliziten externen Voraussetzungen und die Editor-Vertragsprüfungen aus Abschnitt 5 bleiben unverändert eingeordnet. Keine erneute echte DB-/Broker-/Studio-Gesamtabnahme durchgeführt. G00-Inventur und verfügbare lokale Baseline sind abgeschlossen; zusammen mit G01 ist Phase 1 abgeschlossen, nicht die Git-Integration oder ihre Produktionsabnahme. Kein Commit/Push/PR.

Primärquellen zur Mess-/Schedulinggrenze: [prozessweiter Allokationszähler](https://learn.microsoft.com/dotnet/api/system.gc.gettotalallocatedbytes), [xUnit-Collectionparallelität](https://xunit.net/docs/running-tests-in-parallel).
