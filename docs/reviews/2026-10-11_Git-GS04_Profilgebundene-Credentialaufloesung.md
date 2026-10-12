# Git GS04: profilgebundene Credentialauflösung

Stand: 11. Oktober 2026. Lokale Änderungen auf `codex/git-generic-servers`, Basis `02ebe35`. Fortsetzung des [generischen Git-Plans](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md) und der [HTTPS-Netzwerkgrenze](2026-10-11_Git-GS03_Netzwerkgrenze-und-generischer-HTTPS-Pfad.md).

## Ergebnis und Grenze

Der neue `ProfiledGitCredentialBroker` ist als Scoped-Service registriert. Er löst Basic-/PAT-Credentials aus der Referenz des eingefrorenen Verbindungsprofils auf. Vor der Ausgabe einer kurzlebigen Lease müssen das aktuelle Profil, die Repositoryrevision, Rollen, Grants und eine profilgebundene Netzwerkfreigabe passen. Anonymes Lesen führt dieselbe Berechtigungsprüfung ohne Credentiallookup aus.

Der Broker ist **vorbereitet und lokal getestet, noch nicht an die aktive Produktausführung angeschlossen**. Die bisherigen GitHub-Ausführungspfade und die Sperre für profilgebundene Jobs bleiben erhalten. GS04 ist nicht abgeschlossen. Die Registrierung ist kein Nachweis für Azure DevOps, GitLab, eigene Server oder SSH im Produkt.

## Geänderte Dateien

| Datei | Änderung |
| --- | --- |
| `src/VertexBPMN.Infrastructure/SourceControl/ProfiledGitCredentialBroker.cs` | Profilgebundene Basic-/PAT-Auflösung, anonyme Authentifizierung, aktuelle Autorisierung, Deadline und bereinigte Fehler |
| `src/VertexBPMN.Infrastructure/SourceControl/PersistentSourceControlStore.ConnectionBindings.cs` | `DemandConnectionTargetAsync` prüft frische Grants und den gesamten eingefrorenen Zielvertrag |
| `src/VertexBPMN.Infrastructure/SourceControl/ApprovedGitHttpsTarget.cs` | Netzwerkfreigabe hält neben Remote und kopierten IP-Adressen das unveränderliche Profil |
| `src/VertexBPMN.Infrastructure/SourceControl/GitHttpsNetworkBoundary.cs` | Freigabe enthält das tatsächlich geprüfte Profil |
| `src/VertexBPMN.Infrastructure/InfrastructureModule.cs` | Registrierung des neuen Brokers; bestehende Providerauswahl unverändert |
| `tests/VertexBPMN.Tests/Unit/Application/SourceControlConnectionBindingTests.cs` | Partial-Deklaration für Wiederverwendung der bestehenden SQLite-Fixture |
| `tests/VertexBPMN.Tests/Unit/Application/SourceControlConnectionBindingTests.Credentials.cs` | 15 neue Testfälle einschließlich realem verschlüsseltem Credentialdienst |

## Ablauf und Credentialvertrag

1. Aktuelle Rollen über `ISourceControlActorResolver` ermitteln; Tenant, Grants, Bindungsrevision, Profilrevision und Fähigkeiten erneut im Store prüfen.
2. Netzwerkfreigabe muss denselben exakten Remote und dasselbe vollständige Profil einschließlich Git-/Hosting-Endpointidentität tragen. Eine Freigabe einer anderen Revision kann keine Secrets freigeben.
3. Nur das explizite Verfahren `HttpBasic` wird aufgelöst. Credentialtyp ist `GitHttpBasic`, mit Schlüsseln `username` und `password`. Ein PAT wird als Passwort dieses Verfahrens verwendet; das ist kein eigener Azure-Authentifizierungsadapter. Credential-ID und Tenant müssen exakt stimmen. Keine Rückfallauflösung über die Legacy-Bindung oder den GitHub-App-Broker.
4. Nach Auflösung des Benutzernamens aktuelle Autorisierung erneut prüfen; anschließend Passwort auflösen, Metadaten nochmals lesen und vor Ausgabe die Autorisierung erneut prüfen.
5. Die Lease ist an den exakten Repository-URI gebunden, gilt zwei Minuten und verwendet den vorhandenen IPC-Port. Auf dem internen Kanal stehen Benutzername und Passwort in zwei rohen Zeilen; erst der separate Helper übersetzt dies in das Git-Credentialprotokoll.

Für anonyme Profile wird keine Lease ausgegeben. Unsupported-Verfahren werden abgelehnt. Caller-Cancellation bleibt erhalten; Ablauf der Brokerdeadline ergibt `TimedOut`. Fehler des Credentialbackends werden ohne dessen mögliche sensitive Meldung als `CredentialUnavailable` weitergegeben.

Die Metadatenprüfung über `LastModified` und Secretkeys erkennt beobachtete Rotation beziehungsweise Löschung während der Auflösung. Der bestehende Credentialdienst besitzt **keine atomare Secretrevision**: Diese Prüfung ist keine atomare Mehrsecret-Leseoperation und keine sofortige Widerrufsgarantie für bereits ausgegebene Leases. Secretwerte werden vom bestehenden Dienst als Strings geliefert; der Broker fügt keine Speicherung oder Protokollierung hinzu. Für eine spätere stärkere Lifecyclegarantie ist ein versionierter Secret-Snapshot beziehungsweise eine atomare Provideroperation erforderlich.

## Lokale Prüfung

Die neuen Fälle prüfen die Profilreferenz statt der Legacy-Referenz, exakte Repositorybindung, fremde Credentialmetadaten und Ablehnung vor Secretlookup. Änderungen während der Auflösung umfassen Profilwiderruf, neue Profilrevision, entfernte Grants, entzogene Rolle, Credentialrotation und Löschung. Zusätzlich geprüft: anonymes Lesen, deaktivierter Broker, falscher Remote oder falsche Freigaberevision, bereinigter Backendfehler und Caller-Cancellation.

Der Test mit `PersistentCredentialService`, SQLite und Data Protection erstellt ein geschütztes Basic-Credential, löst es auf, rotiert das Passwort und prüft die neue Lease sowie verschlüsselte Speicherung. Er verbindet den Broker noch nicht mit einem nativen Gitprozess oder einem externen Server.

| Prüfung | Ergebnis |
| --- | --- |
| Release-Build der gesamten Lösung | Erfolgreich, 0 Warnungen, 0 Fehler; 52,93 s |
| Gezielte Profil-/Ziel-/Netzwerk-/Security-/Fixturetests | 218 bestanden, 0 Fehler, 0 übersprungen; 10,539 s |
| Formatprüfung der vier neuen/geänderten Broker-/Boundary-Dateien und Credentialtests | Erfolgreich, Exit 0; ausschließlich Checkmodus |
| Native Git-/Helper-/GitHub-Regressionsauswahl | 160 bestanden, 0 Fehler, 0 übersprungen; 219,238 s |
| CI-safe gemäß `.github/workflows/ci.yml` | 1.609 bestanden, 0 Fehler, 8 externe Tests übersprungen; 1.617 gesamt, 1 min 26,562 s |

Die acht übersprungenen Tests benötigen externe Voraussetzungen; diese Abnahmen sind nicht ausgeführt. Der im vorherigen GS03-Bericht dokumentierte sporadische C8-FEEL-Fehler trat in diesem Lauf nicht auf; dessen Ursache wurde in dieser Fortsetzung nicht verändert oder geklärt.

Der erste gezielte Credentiallauf hatte zwei fehlgeschlagene Assertions: Die Tests erwarteten fälschlich Git-Helper-Präfixe auf dem internen IPC-Kanal. Die Assertions wurden auf den tatsächlichen Zwei-Zeilen-Vertrag korrigiert; der Produktvertrag blieb unverändert. Die erste Formatprüfung beanstandete fünf Whitespacestellen eines neuen Recordinitializers; diese wurden manuell korrigiert.

Reproduzierbare Befehle:

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*GitHttpsNetworkBoundaryTests' -class '*SourceControlConnectionBindingTests' -class '*SourceControlConnectionProfilePersistenceTests' -class '*RepositoryConnectionProfileTests' -class '*SourceControlExecutionTargetTests' -class '*LegacyGitHubConnectionMappingTests' -class '*SourceControlSecurityTests' -class '*SourceControlOptionsTests' -class '*SharedSqliteDbFixtureIsolationTests'
```

Editorassets unverändert, daher Asset-Skip. Native Auswahl und CI-safe-Ausschlüsse entsprechen dem vorherigen GS03-Bericht beziehungsweise dem unveränderten Workflow. Lokale Logs: `artifacts/git-generic-servers/gs04-credential-build-final.log`, `gs04-credential-targeted-final.log`, `gs04-format-final.log`, `gs04-credential-native.log`, `gs04-credential-ci.log`.

## Clean-Code-Review

Skill: `.agents/skills/dotnet-clean-code-review/SKILL.md`. Scope: die sieben oben aufgeführten C#-Dateien, für bereits bestehende Dateien geänderte Mitglieder und betroffene Aufrufer; Basis `02ebe35`, lokale Änderungen. Keine generierten Dateien. D1–D7 mit `rg` durchgeführt, vollständige Kandidatenausgabe in `gs04-clean-code-candidates.log`; alle Suchläufe Exit 0 oder 1, kein Suchfehler. Neue Dateien und geänderte Mitglieder manuell gegen alle zwölf Lesbarkeitskategorien gelesen.

| Regel | Ergebnis / Evidenz |
| --- | --- |
| 1 Async/Cancellation | PASS: awaitable Verträge, Token für Rollen-, Store- und Credential-I/O; kein neues `async void` |
| 2 Collection-Verträge | PASS: bestehender Rollenport und materialisierte Adresskopie; keine Null-Collections |
| 3 Boolean-Modi | PASS: keine neuen Modusschalter; Authentifizierung über Enum |
| 4 Verantwortungsnamen | PASS: Credentialbroker, Zielprüfung und Netzwerkfreigabe haben getrennte Aufgaben |
| 5 Regions | PASS: keine im neuen Scope |
| 6 Interfaces | PASS: bestehende Credential-/Actorports dienen Providergrenzen und Testsubstitution; keine neue Interfaceabstraktion |
| 7 Vererbung | PASS: keine neue Klassenhierarchie; Testdoubles implementieren bestehende Ports |
| 8 Lesbarkeit | PASS: frühe Ablehnung, konkrete Fehler, Formatcheck erfolgreich; DI-Zeile an umgebende Einrückung angepasst |

Keine bestätigten verbleibenden Clean-Code-Befunde in diesen Änderungen. Review ersetzt keine Sicherheits- oder externe Providerabnahme.

## Nächste notwendige Schritte

1. Administrativen Netzwerk-/Trustprofilresolver samt stabilen Revisionen bereitstellen und echte private Peers/TLS-/DNS-Negativfälle qualifizieren. Custom-Trustreferenzen bleiben im vorbereiteten Fetch abgelehnt.
2. Broker und kontrollierten Gitprozess über dieselbe geprüfte Netzwerkfreigabe verbinden; keine erneute DNS-Auflösung nach Secretfreigabe. Lease-/Widerrufsgrenzen im tatsächlichen Produktpfad prüfen.
3. Dauerhafte Annahme, Idempotenz, Executor und Reconciliation vollständig an den geschützten Zielvertrag anschließen. Vor Aktivierung neue Worker ausrollen, alte Worker drainen/stoppen; sie verstehen neue Verträge nicht.
4. Lesen, Commit, Push, Konflikt und Recovery auf einem eigenen HTTPS-Server durch den Produktpfad abnehmen. Erst danach Supportclaims in Wiki und Website erweitern, Hostingadapter/SSH/Studio gemäß Plan liefern.

Keine Veröffentlichung, kein Commit/Push, keine externe Testschreiboperation und keine Migration einer Benutzerdatenbank ausgeführt.
