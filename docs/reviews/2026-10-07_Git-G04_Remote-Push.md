# G04 – Remote-Push mit dauerhaftem Ergebnisabgleich

Basis: `39decbe`, Branch `codex/git-source-control-phase-1`.
Status: interner Push-/Jobpfad implementiert und lokal unter Windows/HTTPS/SQLite
abgenommen. Die automatisierte Gesamtintegration bleibt separat offen.
Dies ist keine Freigabe des gesamten Git-Providers, der Studiointegration oder
einer realen GitHub-Installation.

## Implementierung

- `Application/SourceControl/PushJobSubmission.cs`: separates Push-Kommando
  mit ID eines abgeschlossenen lokalen Commitauftrags und erwartetem Remote-Stand.
  Kein beliebiger vom Client gewählter Commit, Branch oder Dateisystempfad.
- `Infrastructure/SourceControl/PersistentSourceControlStore.Push.cs`:
  relationale, tenant-/actorgebundene Annahme; aktuelles Push-Recht und
  Bindungsrevision; persistente Idempotenz; Prüfung des lokalen Commitbelegs.
  Der verschlüsselte Pushauftrag kopiert die unveränderlichen Quelldaten und
  den ursprünglichen Requesthash. Spätere Sessionänderungen beeinflussen ihn nicht.
  Der Push-Envelope hat eine feste Grenze von 6 MiB, weil er den bereits auf
  3 MiB begrenzten Commitauftrag plus dessen Beleg enthält. Andere Auftragsarten
  bleiben auf 3 MiB begrenzt; eine SQLite-Regression prüft beide Grenzen.
- `SourceControlPushExecutor.cs`: eigener signierter Jobworkspace; feste Basis
  nachladen und identischen Commit mit ursprünglichem Zeitpunkt, Nachricht,
  Parent und Snapshotbytes rekonstruieren. Eine abweichende Commit-ID blockiert.
  Vor der Wirkung werden Rollen, ACL, Bindungsrevision und Claim erneut geprüft.
- `ControlledGitProcess.Push.cs`: echte authentifizierte HTTPS-Headabfrage und
  Push, vollständige OIDs, genau eine `refs/heads/vertex/<session>`-Zielref.
  Default-/Releasebranch, Löschung, beliebige Ref-Ausdrücke und Null-OIDs
  sind nicht zugelassen. Transport-/DNS-/TLS-/Helpergrenzen bleiben erhalten.
- `SourceControlClaimedPushRunner.cs`: endlicher gefenceter Runner mit
  unabhängigen DB-Scopes für Leaseverlängerung und Fehlerabschluss. In DI
  registriert, aber kein automatisch aktivierter Write-Scheduler.
- `PersistentSourceControlStore.cs`: Annahmedaten von `CommittedLocal` werden
  nicht mehr allein wegen abgelaufener Retention gelöscht. Unveröffentlichte
  Commits dürfen dadurch nicht ihren späteren Push-/Sicherungspfad verlieren.

## Atomarer Konkurrenzschutz

Ein vorheriger Headvergleich allein ist ausdrücklich nicht der Schutz.
Zusätzlich übergibt der Adapter eine **explizite** Lease für genau die Zielref
und deren erwartete vollständige OID beziehungsweise Abwesenheit an Git.
Die zugrunde liegende Gitoption heißt `--force-with-lease=<ref>:<expect>`.
Weil diese Option an sich auch Historienumschreiben erlauben könnte, prüft
der Adapter vorher anhand unveränderlicher OIDs, dass der erwartete Commit
Vorfahre des Zielcommits ist. Es gibt kein frei zugängliches Force-Push,
kein `+`-Refspec, kein `--all` und keinen Write auf Default/Release.

Die reale Race-Prüfung verändert die Remote-Ref **nach** der Advertisement
und **vor** Verarbeitung des Updatekommandos durch `git http-backend`.
Sowohl Rücksetzung als auch konkurrierender neuer Commit müssen erhalten
bleiben. Eine konkurrierende Branch-Neuanlage wird ebenfalls getestet.
Ein separater Test deckt erlaubtes Fast-Forward und blockiertes Rewind ab.

Grundlage: [Git-Push: explizite Lease und Ref-Update](https://git-scm.com/docs/git-push).
Nur der ausdrücklich angegebene Erwartungswert wird verwendet, keine lokale
Remote-Tracking-Ref als implizite Konkurrenzannahme.

## Dauerhaftes Intent und ungewisser Ausgang

Vor dem Remote-Write wird der verschlüsselte erwartete Ergebnisbeleg dauerhaft
gespeichert. Binding-/Claim-Schreibsperren bleiben über den einzelnen begrenzten
Remoteversuch und dessen DB-Abschluss erhalten. Der Versuch wird zusätzlich
an die verbleibende Leasezeit gebunden. Eine zurückgerollte Abschlusstransaktion
entfernt das vorher gespeicherte Intent nicht.

- Vollständige bestätigte CAS-Ablehnung: `Conflict`, fremder Head bleibt erhalten.
- Erfolgreicher Remote-Write und DB-Abschluss: `Pushed`.
- Transportfehler nach möglichem Write: nur Remote-Head lesen, nicht erneut pushen.
  Ziel-Head bestätigt das gewünschte Ergebnis; ansonsten `ResultUnknown`.
- Neue Claim-Generation mit vorhandenem Intent: ausschließlich lesen/abgleichen.
  Abwesenheit, alter erwarteter Head oder anderer Head erlauben keinen Replay.
- Rollen-/ACL-/Bindingänderung bei vorhandener ungewisser Wirkung: kein neuer
  Write; unbekannten Ausgang nicht als nachweislich wirkungslosen Konflikt ausgeben.
- Frischer Claim auf unbekannten Ausgang ohne Intent: kein blindes Schreiben.

Beim tatsächlichen Antwortverlusttest wurde ein automatischer erneuter POST
auf einer wiederverwendeten HTTP-Verbindung sichtbar. Push verwendet deshalb
HTTP/1.1 mit `Connection: close`; konfigurierte 429-Retries sind deaktiviert.
Der Test bricht die Antwort **nach** abgeschlossenem Server-Commit und **vor**
Antwortübertragung ab und fordert weiterhin genau einen echten Update-Request.
Keine Testassertion wurde dafür aufgeweicht.

[Git-Konfiguration](https://git-scm.com/docs/git-config) dokumentiert HTTP-Version,
zusätzliche Header und 429-Retries. Die Verbindungsregel ist projektspezifisch
und wird durch den lokalen echten HTTPS-Test nachgewiesen, nicht als allgemeine
Garantie über sämtliche Git-/libcurl-Versionen behauptet.

## Konkrete Abnahmen

Alle neuen native-/DB-Fälle liegen im separaten lokalen Adaptertestprojekt,
nicht in der schnellen GitHub-Hauptsuite:

| Methode | Nachweis |
| --- | --- |
| `Https_push_creates_only_workbranch_and_independent_clone_reads_exact_bytes` | Echter HTTPS-Push; unveränderter Defaultbranch; unabhängiger zweiter Clone liest exakte Snapshotbytes; Fast-Forward erlaubt, Rewind und falsche Branchziele blockiert |
| `Https_push_server_CAS_rejects_reset_or_competing_commit_after_advertisement` | Zwei reale serverseitige Racevarianten, kein Überschreiben fremder Änderungen |
| `Durable_https_push_lost_response_is_confirmed_without_resending` | Abgebrochene HTTP-Antwort nach realem Write, exakt ein Update-Request und dauerhafter Status `Pushed` |
| `Durable_https_push_DB_finish_failure_recovers_in_new_scope_without_another_push` | SQLite-Trigger verhindert Finish nach Git-Effekt; Maintenance invalidiert alten Fence; neue DB-/Executorscope bestätigt Remote ohne weiteren Push |
| `Durable_push_role_revocation_after_intent_blocks_remote_write` | Rechteentzug zwischen Belegspeicherung und Wirkung; kein Remote-Write, Intent bleibt |
| `Durable_push_acceptance_is_owned_idempotent_and_retains_unpublished_commit_after_pruning` | Idempotenz-/Actorgrenzen; Retention erhält unveröffentlichten Commit |
| `Claimed_push_cancel_after_server_commit_is_unknown_and_recovers_without_resend` | Echte laufende Git-Ausführung wird nach nachgewiesenem Server-Commit abgebrochen; Runner speichert unbekannten Ausgang; neuer Claim bestätigt ohne Write |
| `Claimed_push_uncertain_intent_and_absent_head_never_replays_write` | Persistentes Intent ohne vorhandenen Ziel-Head bleibt unbekannt, null Update-Requests |
| `Claimed_push_concurrent_creation_is_conflict_and_preserves_other_head` | Vollständige Ablehnung konkurrierender Branch-Anlage wird als Konflikt gespeichert |

Testfixtures: temporäre private Git-Repositories, Kestrel-TLS mit eigener
Test-CA/CRL, realer Auth-Helper, echtes `git http-backend`, isolierte SQLite-DB.
Keine Produktbranches, echten GitHub-Tokens oder Cloudressourcen wurden geändert.
Recovery ist mit neuen DB-/Executorscopes nachgewiesen; ein vollständiger
Neustart des Vertex-Hostprozesses ist damit nicht als durchgeführt behauptet.

## Prüfbefehle und Ergebnisse

SDK aus `global.json`, Microsoft.Testing.Platform/xUnit v3.

```powershell
dotnet build tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -p:RunAnalyzers=false -p:EnforceCodeStyleInBuild=false -p:TreatWarningsAsErrors=false -m:1 --disable-build-servers
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-build --no-restore --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1 --timeout 10m
```

- Diagnosebuild Adapter: erfolgreich, 0 Fehler, acht bestehende Warnungen bei
  Neukompilation. Analyzer nur per Befehlszeile deaktiviert.
- Diagnosebuild Haupttestprojekt: erfolgreich, 0 Fehler; letzter Abschlussbuild
  mit 337 bestehenden Warnungen aus den mitgebauten Projekten. Frühere reine
  Haupttest-Neukompilation: 330 Warnungen; inkrementelle Builds teils ohne Warnungen.
- Strikter Adapterbuild mit unveränderter Analyzerkonfiguration: **nicht
  erfolgreich**, 284 bestehende Fehler im Application-Projekt; keine globale
  Buildfreigabe. Keine Analyzerregeln abgeschaltet oder im Repository unterdrückt.
- Gezielte Runnerabnahme: 3 bestanden, 0 Fehler, 0 Skips, 35,622 Sekunden.
- Vollständige lokale Adaptersuite: **115 bestanden, 0 Fehler, 0 Skips**,
  3 Minuten 26,384 Sekunden. PostgreSQL-Opt-in-Klasse explizit nicht ausgewählt.
- Nach den abschließenden Format- und Envelope-Grenzkorrekturen:
  **13 gezielte Push-Fälle bestanden, 0 Fehler, 0 Skips**, 1 Minute 51,980 Sekunden.
  Die volle 115er-Suite lief vor der letzten Envelope-Grenzkorrektur;
  danach wurden Push-Fälle und die gesamte CI-sichere Hauptsuite wiederholt.
- Hauptsuite mit exakt den Ausschlüssen aus `.github/workflows/ci.yml`:
  zuletzt 1.401 entdeckt, **1.393 bestanden, 0 Fehler, 8 bestehende Opt-in-Skips**,
  1 Minute 28,322 Sekunden. Darin enthalten ist die neue SQLite-Envelope-Grenzprüfung.
  Keine Ausschlussliste erweitert.
- Whitespace-Check der neuen Infrastructure- und Testdateien: erfolgreich;
  `git diff --check` erfolgreich. Nur manuelle Formatkorrekturen eigener Dateien.

## Clean-Code-Review

Skill `dotnet-clean-code-review`: Scope sind die zwölf geänderten/neuen C#-Dateien,
bei bestehenden Dateien nur geänderte Mitglieder/DI und relevante Umgebung.
D1–D7 ausgeführt, betroffene Call Sites und Lesbarkeit manuell geprüft.

| Regel | Ergebnis / begründete Grenzen |
| --- | --- |
| Async/Cancellation | Geänderte I/O awaitable und mit Token; Exceptionabschluss hat eigenen begrenzten Token, damit ursprünglicher Cancel nicht Recovery verhindert |
| Collection-Verträge | Rollen werden vor asynchroner Annahme kopiert; persistente DTOs enthalten begrenzte Snapshots, keine neue nullbare Collection-Rückgabe |
| Bool-Modi | Neue Produktionsoperationen nach Zweck getrennt; `reset` ist ausschließlich eine Testvariante, kein Produktmodusschalter |
| Verantwortungsnamen | Push-Transport, Persistenz, Executor und Runner ausdrücklich getrennt |
| Regionen | Keine neuen Regionen |
| Schnittstellen | Bestehende Ports erhalten; Credential-Testdouble verweigert jede echte Secretauflösung |
| Vererbung | Nur redigierte Exception und vorhandene Framework-/Portverträge, keine neue komplexe Vererbungskette |
| Lesbarkeit | Eigene Formatfehler korrigiert; Claim-, Intent- und Remote-Grenzen dokumentiert. Viele Parameter betreffen bestehende gefencete interne Verträge, nicht neue öffentliche Boolean-Modi |

Keine verbliebenen bestätigten Clean-Code-Funde im geprüften Änderungsumfang.
Das ist kein Nachweis, dass der gesamte Repository-Analyzerstand bereinigt ist.

## Grenzen und nächster Schritt

- Kein automatischer Hosted-Write-Dispatch ohne vertrauenswürdige aktuelle
  Actor-/Tenant-/Aktivstatus-/Rollenauflösung. Die offene IdP-/Vertex-Rollenentscheidung
  wird nicht durch erfundene Workerrollen übergangen.
- Providerregistrierung, Source-Control-HTTP-Endpunkte und Studio-Pushaktion
  gehören weiterhin zu den offenen G04-/G05-/G06-Paketen.
- Kein neuer PostgreSQL-/SQL-Server-/Linux-/Live-GitHub-Nachweis in diesem Lauf.
  PostgreSQL-Testverbindung nicht konfiguriert; SQL Server auf Nutzerwunsch ungeprüft.
- Originale `CommittedLocal`-Aufträge und Workspaces werden konservativ erhalten;
  Aufbewahrung/Archivierung nach nachgewiesenem Push ist spätere Lifecyclearbeit,
  kein stilles Löschen ungesicherter Arbeit.
- Sperren über den Remoteversuch können die Leaseverlängerung blockieren.
  Die verbleibende Lease begrenzt den Versuch; Verlust bleibt fail-closed/ungewiss.
  Längere Transfers und Replikatkonkurrenz sind separat unter PostgreSQL zu messen.

Kein Commit/Push/PR wurde durch diesen Implementierungsauftrag autorisiert.
