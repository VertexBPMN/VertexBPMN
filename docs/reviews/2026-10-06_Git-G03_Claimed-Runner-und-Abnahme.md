# G03 – gefencete Commit-Ausführung mit Heartbeat und Fehlerabschluss

Stand: 2026-10-06. Branch `codex/git-source-control-phase-1`, Basis `a5ecdce`.
Status: Teilpaket implementiert; G03 insgesamt weiter offen. Keine ursprünglichen Abnahmekriterien gestrichen.

## Umsetzung

- `src/VertexBPMN.Infrastructure/SourceControl/SourceControlClaimedCommitRunner.cs`: führt genau einen bereits erworbenen Commit-Claim aus. Tenant, Actor, Art, Fence, Worker, Lease und Zustand werden vor Ausführung sowie vor Fehlerbehandlung geprüft. Keine Übernahme fremder Actor-Aufträge, keine selbst erfundenen Rollen.
- Erstverlängerung vor Arbeitsbeginn; anschließend Heartbeat alle 30 Sekunden mit zwei Minuten Lease. Heartbeat und Fehlerabschluss haben jeweils eigene DI-Scopes/DbContexts. Ausführung und EF-Heartbeat verwenden niemals gleichzeitig denselben Kontext. Der Prepared-Einstieg erlaubt isolierte native Abnahmen ohne GitHub-Zugriff.
- Produktions-Einstieg verbindet den vorhandenen `SourceControlCommitExecutor` mit dem `SourceControlLeaseRunner` und verlangt einen vertrauenswürdigen Resolver für aktuelle Rollen. Registrierung in `InfrastructureModule.cs`; kein automatisch gestarteter Write-Worker.
- Fehler vor Belegablage: `Failed` beziehungsweise `Cancelled`; nach Belegablage oder bei laufender Reconciliation konservativ `ResultUnknown`. Sicher erkannter Ref-Konflikt bleibt `Conflict`. Bei verlorener Lease keine veraltete Abschlussmutation; Maintenance erkennt den abgelaufenen Claim. Keine automatische Schreibwiederholung.
- `PersistentSourceControlStore.FinishAsync`: persistiert ausschließlich einen stabilen Fehlercode, nie Providerexception, SQL, Serverpfad oder Credential. Erfolgreicher Abschluss löscht alte Fehlercodes. Keine Migration erforderlich, die Spalte bestand bereits.
- `ConfirmCompletedCommitAsync`: vergleicht nach einem Abschlussrennen den geschützten dauerhaften Commitbeleg einschließlich Actor/Tenant/Fence/Repository und vollständigem Receipt. Ein gleichzeitig mit dem erfolgreichen DB-Abschluss fehlgeschlagener Heartbeat darf dadurch nicht fälschlich einen erfolgreich abgeschlossenen Commit als unbekannt melden. Ein abweichender Beleg oder alter Fence gilt nicht als Erfolgsnachweis.

## Root Cause und Regression

Der erste lokale Lauf zeigte einen echten Fehler: Erfolg entfernt die Lease; ein gleichzeitig laufender Heartbeat meldete Leaseverlust und die erfolgreiche Operation wurde als unbekannt zurückgegeben. Der Erfolgs-Test blieb unverändert. Die Korrektur verwendet den gefenceten persistenten Beleg, nicht einen ignorierten Heartbeat oder eine abgeschwächte Assertion.

`tests/VertexBPMN.SourceControl.Tests/CommitExecutionAcceptanceTests.cs` ergänzt reale SQLite-/Git-Prüfungen:

- `Claimed_runner_renews_in_separate_scopes_and_finishes_real_commit`: Erstverlängerung, paralleler Heartbeat, echte Ref-Publikation, gespeicherter Abschluss ohne Lease/Fehler.
- `Claimed_runner_denial_before_intent_is_failed_without_git_effect`: kein Git-Write, persistentes Forbidden, Lease freigegeben.
- `Claimed_runner_denial_after_intent_preserves_unknown_effect_for_reconciliation`: Beleg erhalten, kein blindes Retry und kein behaupteter Erfolg.
- `Claimed_runner_wrong_actor_cannot_finish_another_actors_job`: fremder Actor erreicht weder Rollenresolver noch Fehlerabschluss; Originalclaim unverändert.
- `Claimed_runner_lost_lease_cannot_finish_or_publish_the_old_claim`: echte DB-Lease verfällt; kein Ref-Write/alter Abschluss, Maintenance und höherer Folgefence funktionieren.
- `Claimed_runner_recovers_real_ref_after_database_finish_failure`: DB-Trigger verhindert Abschluss nach echtem Git-Effekt; frischer Scope speichert ResultUnknown, neuer Claim erkennt denselben Commit. Alter Fence, fremder Actor und falscher Commitbeleg werden abgelehnt.

`PostgresAcceptanceTests.Real_postgres_claimed_runner_recovers_git_effect_and_durable_error_state` führt den letzten Fall zusätzlich mit echter PostgreSQL-Datenbank aus. Der vorhandene tatsächliche WSLC-DB-Neustarttest bleibt unverändert.

## Ausführung / Nachweise

Vorläufige Teilnachweise während der Umsetzung: lokale Suite 83 bestanden; PostgreSQL vier bestanden; Solution-Build 0 Fehler/71 bestehende Warnungen; CI-safe Suite 1.380 bestanden, 0 Fehler, acht bestehende externe Opt-in-Skips. Diese Läufe gingen der letzten Erstverlängerungs-/Leaseverlust-Ergänzung voraus. Finale Kandidatennachweise folgen unten; nicht als sauberer Checkout oder Produktionsabnahme ausgeben.

Finaler Solution-Build erfolgreich, bestehende Compiler-/Analyzerwarnungen bleiben. Anschließende unveränderte CI-safe Gesamtsuite: **1.388 gesamt, 1.380 bestanden, 0 Fehler, 8 bestehende externe Opt-in-Skips**, 100,328 Sekunden. Native-/PostgreSQL-Fälle weiterhin im getrennten lokalen Projekt; GitHub-Workflow unverändert.

Finaler gemeinsamer lokaler Adapterlauf einschließlich WSLC/PostgreSQL: **88 gesamt, 87 bestanden, 1 fehlgeschlagen, 0 übersprungen**, 64,527 Sekunden. Alle vier PostgreSQL-Fälle bestanden; bestehender HTTPS-Fall `Hostile_hooks_filters_and_submodules_are_not_executed_by_protected_fetch` scheiterte mit `TimedOut` bei unverändertem zehnsekündigem Fetchlimit. Dieser Gesamtlauf ist **nicht bestanden**. Separater unveränderter HTTPS-Diagnoselauf (`--no-build --no-restore --filter-class '*GitHttpsTransportTests'`): **7 bestanden, 0 Fehler/Skips**, 42,152 Sekunden. Kein Timeout erhöht, keine Assertion geändert oder Fall ausgeschlossen; Ressourcenlast als mögliche Ursache nicht abschließend bewiesen. Die Transportstabilität bleibt als beobachtetes Risiko dokumentiert.

Nach Abschluss der parallel laufenden Solution-/CI-Prüfung komplette unveränderte nicht-PostgreSQL-Auswahl erneut ausgeführt (`--no-build --no-restore --filter-not-class '*PostgresAcceptanceTests'`): **84 bestanden, 0 Fehler, 0 übersprungen**, 49,863 Sekunden. Alle sieben HTTPS-Fälle und der neue reale Leaseverlustfall enthalten. Das ersetzt nicht den fehlgeschlagenen gemeinsamen 88-Fälle-Nachweis und behauptet keine abschließend behobene Flakiness.

Beide eigenen WSLC-Containerläufe beendet und entfernt; zugeordnete anonyme Volumes `15ba2115c5e7ec61f051365b011643236f046c232b47f4a355ee42db40e0fa84` und `5b8251f86a9e8f7b810323e1cd2d2bd5dc9f064bd025221f06bef8e74224adfa` danach nicht mehr in der Volume-Liste. Bestehende ÜBV-Volumes erhalten. Kein SQL-Server-Test.

Lokaler Gesamtlauf des getrennten Adapterprojekts mit expliziten Voraussetzungen:

```powershell
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
$env:VERTEXBPMN_TEST_WSLC = 'C:/Users/yrodriguez/.wslc-compose/venv/Scripts/wslc.exe'
$env:VERTEXBPMN_TEST_POSTGRES_CONTAINER = 'vertex-source-control-phase2-20261006-runner'
# VERTEXBPMN_TEST_POSTGRES_ADMIN nur lokal für die isolierte Datenbank setzen.
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --max-parallel-test-modules 1
```

WSLC: bestehendes `postgres:17-alpine`, isolierter Container, nur Loopback. Der erste Lauf nutzte Port 55439; beim erneuten Aufbau war dieser durch einen Windows-Systemprozess belegt (WSAEACCES). Kein Systemprozess beendet: finaler Lauf auf freiem Port 55440. Keine bestehende Benutzerinstanz oder ÜBV-Daten geändert. Jede Abnahme erzeugt/löscht eine eigene zufällig benannte Datenbank. Container und ausschließlich seine zugeordneten anonymen Volumes werden nach Prüfung entfernt. Kein Image-/globaler Volume-Prune.

## G03-Kriterien: belastbarer Status

| Ursprüngliches Kriterium | Nachweis / verbleibende Grenze |
| --- | --- |
| Bindungen/ACLs, Sessions, Operationen und spätere Provenienz persistent | Bindungen/ACLs/Sessions/Operationen vorhanden und real geprüft. Typisierte Deployment-Provenienz und G08-Orchestrierung noch offen; geschützter Commitbeleg ersetzt sie nicht. |
| Migration/Upgrade aller unterstützten DB-Profile | SQLite/PostgreSQL nachgewiesen. SQL Server ausdrücklich vom Nutzer ausgenommen, nicht als bestanden markieren. |
| DB-Idempotenz, Claims/Leases/Fencing, Retry/Status | Persistente Mechanismen und endlicher Commit-Runner vorhanden. Automatische Queue-Auswahl/Dispatch und produktiver Actor-/Tenant-/Aktivstatus-/Rollenresolver noch offen; keine betriebsfertige automatische Ausführung behaupten. |
| Isolierte Clones/Generationen, Quoten/Cleanup/Retention | Vorhandene lokale Workspace-, Quota-, Cleanup- und Host-Maintenance-Nachweise erhalten. Root/Quota sind hostlokal; kein verteilter Workspace oder Wiederherstellung nach Datenträgerverlust nachgewiesen. |
| Neustart erkennen, tatsächliche IDs/Hashes abgleichen, nicht blind wiederholen | Maintenance, tatsächlicher PostgreSQL-Neustart und lokale Commit-Reconciliation geprüft. Gehosteter Worker-Prozessneustart, Remote-Push-Abgleich und spätere Deployment-Provenienz bleiben offen. |

Nächste Umsetzung: produktive vertrauenswürdige Identitätsauflösung und begrenzter Queue-Dispatch an den Runner; danach realer Worker-Prozessneustart. Providergebundener Remote-Write-Abgleich gehört weiterhin zu den G03/G04-Grenzen, Deployment-Provenienz zu G03/G08. G03 darf nicht mit einer neu definierten kleineren Abschlussbedingung geschlossen werden.
