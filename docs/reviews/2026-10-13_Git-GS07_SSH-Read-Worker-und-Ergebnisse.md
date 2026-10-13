# Git GS07: SSH-Read-Worker und dauerhafte Ergebnisse

Stand: 13. Oktober 2026, Entwicklungsbranch `codex/git-generic-servers`. Fortsetzung von [SSH-Auftragsannahme und Claiming](2026-10-13_Git-GS07_SSH-Auftragsannahme-und-Claiming.md).

## Implementierter Umfang

Der produktive `SourceControlJobsHostedService` selektiert jetzt auch SSH-Read-Verträge der Version 4. Claim und Dispatch bleiben nach Version getrennt; Legacy-, HTTPS- und Hostingaufträge behalten ihre bestehenden Wege. Untypisierte interne SSH-Reads werden vor dem Workerclaim abgewiesen; unverändert ungestartete ungültige Aufträge werden abgeschlossen, damit weitere Aufträge fortschreiten können. Ein bereits übernommener Auftrag wird dabei nicht überschrieben.

`SourceControlSshReadExecutor` und `SourceControlClaimedSshReadRunner` verbinden den bestehenden SSH-Transport mit signiertem Workspace, Gesamtdeadline, frischen Heartbeat-/Abschluss-/Cleanup-Scopes und Fence. Aktuelle Rechte, Profil, Netzwerk- und Hostkeyregeln werden vor und nach Transport und Projektion sowie beim Abschluss erneut geprüft. Worker-Shutdown hält Reads wiederaufnehmbar; Aufruferabbruch ist davon getrennt. Ein überholter Worker darf weder den neuen Claim abschließen noch dessen Workspace löschen. Ein Cleanupfehler entwertet keinen bestätigten Erfolg, sondern bleibt als Wartungsbedarf sichtbar.

Ergebnisse werden im bestehenden `ProtectedResult` atomar mit dem erfolgreichen Abschluss gespeichert. Der bestehende typisierte Ergebnisschutz bindet Inhalt an Tenant, Actor, Repository, Auftrag, Requesthash und Fence. Modellbytes, Generation und Revision werden exakt wiederhergestellt. Es gibt keine zusätzliche Migration. Lesen von Ergebnissen prüft den ursprünglichen SSH-Vertrag und aktuelle Rechte/Regeln erneut; eine geänderte Hostkeyregel autorisiert keinen alten Ergebniszugriff.

`ProfiledSshReadService` projiziert Branchliste, Branchauflösung, Dateiliste, Modelldatei, Historie und Diff aus der angenommenen typisierten Anforderung. Remotequeries benutzen ihren eigenen abgesicherten ls-remote-Pfad; sie benötigen keinen vorgelagerten Fetch. Feste Commit-Reads verwenden den vorhandenen begrenzten SSH-Fetch, Historien den begrenzten History-Fetch.

Die vorhandenen API-Backendmethoden `EnqueueProfiledReadAsync` und `GetProfiledReadResultAsync` routen SSH explizit auf Version 4. Damit ist der vorhandene `POST /api/source-control/repositories/{id}/read-requests`- und `GET /api/source-control/operations/{id}/read-result`-Pfad im Code angeschlossen. Bestehende synchrone Studio-/Providerreads und die Verfügbarkeitsansicht sind noch nicht für SSH freigegeben. Die Tests des gemeinsamen Backendpfads sind kein echter HTTP-/Browser-/SSH-Servernachweis.

## Offene Abnahmen und Arbeit

Reale SSH-Anmeldung und Datentransfer unter Windows/Linux sowie kombinierte HTTP-/Worker-/Server-/Crash-Recoveryabnahmen bleiben unperformiert. Kontrollierte Transportcallbacks ersetzen diese nicht. SSH-Commit/Push mit Ref-CAS/Reconciliation, Passphrase, persistente Bereinigung verwaister privater Authentifizierungsdateien sowie Studioanbindung bleiben offen. Es wird keine allgemeine SSH- oder Produktions-Supportfreigabe dokumentiert.

Azure benötigt weiterhin reale PAT-/Entra-/Policyabnahmen; Audit weitere Recovery-/Mehrreplikat-/Langzeitabnahmen. Die bekannte C.8.0-FEEL-Instabilität ist nicht Gegenstand einer Behebung in diesem SSH-Paket.

## Prüfungen

Vollständiger finaler Release-Build mit unveränderten Editorassets: null Warnungen/Fehler (1:12,31). 331 gezielte Tests erfolgreich, keine Fehler oder Skips (61,764 Sekunden). Die 23 neuen Fälle prüfen Ergebnisbestätigung und Cleanup, Backendfehlersanitisierung, Aufruferabbruch, Fetch-/Projektionsdeadline, fremden Actor, Rechteentzug/Heartbeat, abschließende Autorisierung, überholten Worker, beschädigten Workspace-Marker, wiederaufnehmbaren Worker-Shutdown, geschützte Ergebnis-/Fence-/Typbindung, Originalbytes/Generation sowie den gemeinsamen API-Backendpfad. Der echte Jobhost wird mit einem manipulierten SSH-Ziel ausgeführt und beendet den ungestarteten Auftrag vor Workspace-/Netzwerkzugriff; dies ist kein nativer SSH-Serverlauf.

Elf neue beziehungsweise vollständig eigene C#-Dateien sind check-only whitespacegeprüft. In der geänderten ReadFacade wurden auch die vom Check gemeldeten Leerraumstellen des bereits bestehenden Requestinitializers berichtigt; keine Semantikänderung daraus. D1–D7 und manuelle Prüfung aller acht Clean-Code-Regeln: 13 eigene Dateien/geänderte Mitglieder plus drei DI-Registrierungen, keine bestätigten Befunde. Der SSH-Lebenszyklus bleibt explizit an Version 4 gebunden; gemeinsame Lease-, Workspace-, Fehler- und Ergebniskontrakte werden wiederverwendet, der HTTPS-Lebenszyklus wurde nicht umgeschrieben.

| Regel | Ergebnis im eigenen Umfang |
| --- | --- |
| Async/Cancellation | Awaitbare Verträge und Tokenweitergabe; Caller/Worker-Shutdown als bestehendes Enum getrennt; Fresh Scopes für Heartbeat/Abschluss/Cleanup |
| Collections | Typisierte materialisierte begrenzte Seiten; Snapshotbytes als Kopien; keine nullable Collections |
| Boolean-Modi | Workspace-/Cleanupstatus sind Zustand; keine neuen unklaren bool-Parameter |
| Namen | Executor, Claimrunner, Projektionsservice und Ergebnisstore nach Verantwortung benannt |
| Regions/Struktur | Keine Regions; versiongetrennte kohäsive Typen und bestehende Partial-Struktur |
| Interfaces | Bestehende Actor-/Options-/Scopeports erhalten; keine neuen Interfaces |
| Vererbung | Nur vorhandene Frameworkbase des Jobhosts und IAsyncDisposable im Testharness |
| Lesbarkeit | Guards, Resultatbindung und Besitzerwechsel manuell gelesen; Testnamen/synthetische Daten als begründete Ausnahmen |

Logs unter `artifacts/git-generic-servers/ssh-lifecycle-*.log` werden nicht versioniert. CI-safe mit den unveränderten Ausschlüssen aus .github/workflows/ci.yml: 1.974 Erfolge, acht bestehende externe Skips, keine Fehler, insgesamt 1.982 Fälle (2:10,591). Die Skips bleiben unperformierte externe Abnahmen. Dieser grüne Lauf beweist keine Behebung der früher beobachteten C.8.0-FEEL-Instabilität.
