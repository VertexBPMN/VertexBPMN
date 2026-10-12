# GS05: Hostingregistry und Bearer-IPC-Vorprüfung

Stand: 12. Oktober 2026. Branch `codex/git-generic-servers`, uncommittierter lokaler Kandidat auf Basis `02ebe35`. Keine Veröffentlichung, Ressourcenanlage oder externen Testwrites.

## Aktive Hostingregistry

`RepositoryHostingRegistry` löst ausschließlich fest registrierte Adapter anhand exakter Provider-ID auf. Unbekannte IDs haben keinen Fallback; doppelte Registrierung scheitert bei der Hostkomposition. Profilgebundene Repositories können nicht versehentlich den Legacy-GitHub-Pfad verwenden.

`GitHubHostingAdapter` kapselt die operationslokale Tokenlease und den begrenzten HTTP-Client. `SourceControlPullRequestExecutor` benutzt die Registry für Create und read-only Reconciliation; `SourceControlPullRequestStatusReader` für Livezustand und Reviews. Bestehende persistent angenommene Commands, Write-Intent, Idempotenz, Tenant/Actor/Grants, Lease/Fence und erneute Autorisierung bleiben in der vorhandenen Orchestrierung. Sitzungen werden auch nach einem fehlgeschlagenen Create beendet.

Neue interne Ports `IRepositoryHostingAdapter` und `IRepositoryHostingSession` transportieren den bereits akzeptierten Command einschließlich Headcommit und bieten ausdrücklich Reconciliation. Der vorhandene öffentliche `IRepositoryHostingProvider`-Vertrag wird nicht entfernt oder scheinimplementiert: Er besitzt diese Reconciliation-/angenommenen-Command-Invarianten nicht und bleibt bis zur abgestimmten öffentlichen Providerintegration unverändert.

Tests: vier Registryfälle zu Auswahl, unbekannten/case-geänderten IDs, Dubletten, Profilabweisung und Kontextweitergabe. Zusätzlich führt `SourceControlPersistenceTests.Active_registry_path_reconciles_a_lost_create_without_a_second_write` den produktiven Executorpfad mit relationaler Persistenz und fest registriertem Testadapter aus. Ein verlorenes Create wird genau einmal durch Reconcile bestätigt; kein zweites Create, zwei ordnungsgemäß beendete Sitzungen.

**GS05 bleibt teilweise offen:** profilgebundene Hostingannahme und unveränderliche Provider-/API-/Repositoryidentität, providerneutrale Belegvalidierung, Audit und weitere Adapter sind damit noch nicht implementiert. Bestehende Legacy-GitHub-Belege behalten ihre ursprüngliche Semantik.

## Standardbasierter Bearerkanal

Die [offizielle Git-Credential-Dokumentation](https://git-scm.com/docs/git-credential) beschreibt seit Git 2.46 die ausgehandelte Capability `authtype`. Sie erlaubt `authtype` und `credential` statt einer Basic-Kodierung. Der bestehende Mindeststand Git 2.56 genügt. Die frühere Proxy-Idee ist dadurch für diesen Prototyp nicht erforderlich; keine eigene OAuth- oder TLS-Vermittlung wurde geschrieben.

`GitHttpBearerCredentialLease` ist kurzlebig, repositorygebunden und redigiert ihre Darstellung. Der vorhandene Same-Account-Pipekanal verlangt für Bearer eine explizite Capability-Aushandlung, bevor Tokenmaterial ausgegeben wird. Der Helper liefert erst danach den standardisierten Bearer-Vertrag mit `ephemeral=true`. Fehlende Capability, fremde Host-/Pfadwerte, vorgegebene Identität oder Credentialinjektion führen zum Abbruch. Kein Basic-Fallback; `store`/`erase` persistieren weiterhin keine Credentials.

Der native interne HTTPS-Pfad erkennt diesen Leasetyp. Zusätzliche native Fälle prüfen Basic und Bearer jeweils mit SHA-1/SHA-256, konfliktgeschütztem Push und unabhängigen Clonebytes. Sieben Helperfälle prüfen Capability, Fremdrepository und fehlenden Fallback.

**Kein Entra-Supportnachweis:** Der profilgebundene Produktbroker bietet weiterhin Basic/PAT und Anonymous; ein Entra-Tokenissuer ist noch nicht angeschlossen. Linux und echte Azure-Repos-Abnahme bleiben offen. Die [Microsoft-Dokumentation zu Service Principals](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/service-principal-managed-identity?view=azure-devops) wurde für die nächste explizite Identitäts-/Audiencebindung geprüft; keine Anmeldung oder Tokenbeschaffung ausgeführt.

## Tatsächliche Prüfungen

- Gesamtbuild nach den Registry-/Beareränderungen: erfolgreich, **keine Warnungen oder Fehler**, 21,64 Sekunden; Editorassets unverändert, `SkipBpmnIoAssetBuild=true`, `--no-restore`.
- Gezielte Registry-/Persistenz-/Security-Auswahl: **67 bestanden**, keine Fehler/Skips, 11,795 Sekunden.
- GitHub-Transport-/Response-/Reconciliation-Adapter: **31 bestanden**, keine Fehler/Skips, 0,471 Sekunden.
- Native gesamte Git-Auswahl mit erweitertem Bearerpfad: **131 bestanden**, keine Fehler/Skips, 350,096 Sekunden. Windows mit echtem Git; inklusive Basic/Bearer-SHA-1/SHA-256-Push, unabhängigen Clonebytes und sieben Bearer-Helper-Negativ-/Negotiationfällen.
- Erste CI-safe-Auswahl: **1.752 bestanden, ein Fehler, acht externe Skips**, 1.761 insgesamt, 2:12,889. Fehler: bestehender `C_8_0_Test.Test_C_8_0_Bpmn`, FEEL meldet fehlende Variable `Vacation Approval` nach dem internen Decisionpfad.
- Diagnostischer Einzellauf der C.8.0-Klassen: **zwei bestanden**, 3,201 Sekunden. Der breite Fehler ist damit nicht erklärt oder behoben. Keine Engine-/Fixtureänderung, keine Assertion abgeschwächt, kein zusätzlicher Ausschluss. Sequenzieller Wiederholungslauf ebenfalls mit einem C.8.0-Fehler: 1.752 bestanden, acht übersprungen, 1:44,208. Danach wurde ausschließlich die Testdiagnose ergänzt: der echte Engineaufruf und alle bisherigen Assertions bleiben, protokollierte DMN-Ausnahmen werden beim Folgefehler als AggregateException mit ausgegeben. Kein Produktionscode der Engine geändert.
- Letzter CI-Lauf mit ergänzter C.8.0-Diagnose: **1.753 bestanden, acht übersprungen, keine Fehler**, 1.761 insgesamt, 1:49,044. Die beiden vorherigen Fehler sind damit nicht als behoben bewiesen; die Teststabilität bleibt eine offene GS10-Lücke. Build des zentralen Testprojekts nach der Diagnoseänderung: keine Warnungen/Fehler, 40,92 Sekunden.
- Check-only-Formatprüfung der sieben Hosting-/Bearer-Produktdateien und der zwei neuen Registry-/Persistenztestdateien erfolgreich. D1–D7 für 15 konkrete Dateien durchgeführt; ergänzende manuelle Prüfung der neuen Klassen und geänderten Orchestrierungs-/Helperstellen. Bestehende große DI-/Fixturedateien nur im geänderten Bereich geprüft. Vollständiger Review der angesammelten Featureänderungen bleibt GS10.

## Scoped Clean-Code-Prüfung

Skill: `.agents/skills/dotnet-clean-code-review/SKILL.md`. Eigener Änderungsumfang, keine generierten Dateien, keine fremde Passkeys-Planung.

| Regel | Ergebnis im geprüften Änderungsumfang |
| --- | --- |
| Async/Cancellation | Keine bestätigten Befunde; Token bis Adapter/Helper/IPC weitergereicht |
| Collection-Verträge | Materialisierte Rollen und begrenzte Reviewlisten; keine neue null-Collection |
| Bool-Modi | Capability ist Protokolldatum; Transportwahl und Berechtigungen typisiert |
| Verantwortungsnamen | Registry, Adapter, Session und Credentiallease tragen konkrete Zuständigkeiten |
| Regionen | Keine neuen Regionen |
| Interfaces | Adapter- und Sitzungsports für Substitution und Write-Reconciliation erforderlich |
| Vererbung | Keine neue eigene Klassenhierarchie; Sitzungen implementieren Ressourcenvertrag |
| Lesbarkeit | Compiler-/Analyzerbefunde korrigiert; Eigentum von Client/Lease in beendbarer Session |

Logs liegen ignoriert unter `artifacts/git-generic-servers/gs05-*.log`. Der Gesamtplan einschließlich Azure-/SSH-/Hosting-/Browser-/Server-Recoveryabnahme ist **nicht fertig**.
