# Git GS02: Profilpersistenz und Schema-Upgrade

Stand: 11. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`. Fortsetzung von [GS01](2026-10-11_Git-GS01_Zielbindung-Legacy-und-Credentialkanal.md) und des [Implementierungsplans](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md).

Status: erster GS02-Baustein implementiert; Gesamtpaket bleibt offen. Dieser Baustein speichert Profilmetadaten, aktiviert aber noch keine generischen Git-Aufträge, API oder Studio-Anbieterwahl.

## Implementierung

- `SourceControlConnectionProfileRecord` und `SourceControlConnectionProfiles`: eigener zusammengesetzter Primärschlüssel `(TenantId, Id)`. Derselbe Profilbezeichner in zwei Mandanten bezeichnet zwei voneinander unabhängige Datensätze.
- `PersistentSourceControlStore.Profiles.cs`: Erstellen mit Revision 1, administrative Abfrage, atomarer Austausch mit erwarteter Revision und dauerhafter Widerruf. Administrative Methoden verlangen die vom Host authentifizierte Rolle `Admin` und expliziten Mandanten. Sie ersetzen keine Repository-Grants oder Credentialautorisierung.
- Änderungen verwenden relationale Compare-and-swap-Updates; veraltete Revisionen können neuere Konfiguration nicht überschreiben. Widerruf erhöht Revision und JSON-Revision gemeinsam; widerrufene IDs können nicht wieder aktiviert oder neu angelegt werden.
- Profile enthalten ausschließlich Referenzen auf Credentials, Netzwerk- und Trustkonfiguration; die Ablage löst keine Credentials auf. Größenlimit 32 KiB UTF-8. Beim Lesen werden Schema, Policy, Mandant, ID und Revision erneut validiert; beschädigte Metadaten ergeben einen sicheren Fehler ohne Rohdaten.
- Neue Migration `20261011134324_SourceControlConnectionProfiles`: nur zusätzliche Tabelle, keine Umdeutung alter Bindungen und keine Änderung geschützter Auftragsbelege, Sessions oder Leases. SQLite-generierter Designer/Snapshot plus explizite PostgreSQL-/SQL-Server-Spaltentypen. Die eng begrenzte MA0048-Ausnahme entspricht der notwendigen EF-Konvention für Zeitstempel-Dateinamen; keine fachlichen Analyzer deaktiviert.

Migration mit vorhandenem EF-Werkzeug aus dem frisch gebauten Modell erzeugt. Werkzeug 10.0.11 meldete die vorhandene Runtime 10.0.12; keine Installation oder Aktualisierung vorgenommen. Keine Migration auf eine Benutzer- oder Produktionsdatenbank angewendet. Tests verwenden ausschließlich eigene SQLite-In-Memory-Datenbanken; SQL-Erzeugung für andere Provider benötigt keine Verbindung.

## Prüfung

`SourceControlConnectionProfilePersistenceTests` prüfen tatsächliche SQLite-Migration vom vorherigen SourceControl-Schema, Erhalt alter Binding-/Grant-/Session-/Request-/Result-/Fence-/Lease-Daten, Zugriff nach neuem DbContext, Mandantentrennung mit gleicher ID, konkurrierende Revisionsstände aus getrennten Scopes, Widerruf und Wiederanlageverbot, Administratorrolle, beschädigte Metadaten, Größe, Cancellation und ausgeschalteten Store. PostgreSQL-/SQL-Server-Fälle prüfen erzeugtes SQL; sie sind keine Runtime-Abnahme dieser Datenbanken.

Strenger Solution-Build: **0 Warnungen, 0 Fehler**, 31,75 Sekunden. Gezielte Profil-/Ziel-/Legacy-/Sicherheitsregression inklusive 18 neuer Persistenz-/SQL-Fälle: **159 bestanden**, keine Fehler/Skips, 8,307 Sekunden. Check-only-Formatprüfung aller vier neuen handgepflegten GS02-C#-Dateien und `git diff --check`: Exit 0. Erster CI-safe-Lauf: 1.549 bestanden, ein Fehler in `C_8_0_Test.Test_C_8_0_Bpmn` (fehlende FEEL-Variable `Vacation Approval`), acht externe Skips; 1 Minute 31,050 Sekunden. Der unveränderte isolierte C.8.0-Nachlauf bestand mit zwei Tests (2,674 Sekunden). Derselbe Fehler wurde bereits während GS01 beobachtet; eine Last-/Timeoutabhängigkeit ist nur eine Hypothese, nicht nachgewiesen. Keine Engineänderung, Assertionsabschwächung oder zusätzliche Ausschlussregel. Der vollständige, unveränderte Nachlauf ohne konkurrierenden Format-/Buildprozess ist bestanden: **1.550 bestanden, 0 Fehler, 8 externe Skips**, 1 Minute 27,091 Sekunden, Exit 0. Der erste Lauf bleibt in `gs02-ci-safe-first.log` erhalten. Lokale Logs liegen in `artifacts/git-generic-servers/gs02-*.log`.

Clean-Code-Skill `.agents/skills/dotnet-clean-code-review/SKILL.md` mit beiden Referenzen angewendet: D1–D7 über alle 24 betroffenen, nicht generierten C#-Dateien, Exitcodes kontrolliert. D8 über neue Dateien und geänderte Mitglieder gelesen; Migration und generierte Modelländerungen zusätzlich geprüft. Kein bestätigter Wartbarkeitsbefund. Async-I/O reicht Cancellation weiter; bool-Rückgaben sind CAS-Ergebnisse, `IsRevoked` ist fachlicher Zustand. Keine neuen Interfaces, Pakete oder Vererbungshierarchien. Log `gs02-clean-code.json`.

Build und gezielter Testbefehl:

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*SourceControlConnectionProfilePersistenceTests' -class '*RepositoryConnectionProfileTests' -class '*SourceControlExecutionTargetTests' -class '*LegacyGitHubConnectionMappingTests' -class '*SourceControlOptionsTests' -class '*SourceControlSecurityTests'
```

CI-safe-Befehl und unveränderte Ausschlussliste: `.github/workflows/ci.yml`, Schritt **Test (CI-safe suite)**. Alle neuen Persistenzfälle gehören zur normalen Suite.

## Weiterhin offen

GS02 benötigt anschließend die Zuordnung zwischen Bindung und Profil sowie den persistierten Ausführungszielkontext in versionierten Aufträgen. Alte Worker müssen neue Versionen vor dem Claim erkennen und ablehnen; fehlende Profile/Widerruf dürfen keine angenommene Operation auf ein anderes Ziel umleiten. Upgrade-/Drain-/Claim-/Recoverytests sind dafür Pflicht. Dieser Baustein aktiviert keine neue Jobversion.

GS01 bleibt für Bearer-Sicherheitsnachweis und Hosting-/Adapterversionsverträge offen. Danach folgen freigegebene interne Netzwerk-/Trustpolicy und vollständiger HTTPS-Gitpfad. Azure DevOps, GitLab, SSH und eigene Produktionsserver sind weiterhin nicht extern qualifiziert. Wiki/Website erhalten erst mit tatsächlich angeschlossenem Backend eine neue Supportaussage.

Fortsetzung: [Profilzuordnung und Worker-Vertragsgrenze](2026-10-11_Git-GS02_Profilzuordnung-und-Worker-Vertragsgrenze.md). Die bisherigen Testergebnisse oben betreffen den vorherigen Baustein; aktuelle Nachweise stehen im Fortsetzungsbericht.
