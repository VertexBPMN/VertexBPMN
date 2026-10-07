# Keycloak – lokale Abnahme vom 07.10.2026

## Ergebnis und Umfang

Die nachstehende lokale OIDC- und Git-Identitätsabnahme ist bestanden.
Keine Produktionsfreigabe, kein Azure-Deployment und kein externer Git-Push
wurden durchgeführt. G04/G05/G06 sind dadurch nicht vollständig abgeschlossen.

| Lauf | Ergebnis | Dauer |
| --- | --- | --- |
| Bestehende OIDC-Browser- und Security-Abnahme | 16 bestanden, 0 Fehler, 0 übersprungen | 350 s |
| Neuer Git-Identitätstest gegen echten Keycloak, abschließender Lauf | 1 bestanden, 0 Fehler, 0 übersprungen | 57 s |
| Native Git-/Adapterregression ohne PostgreSQL- und Live-Keycloak-Klassen | 126 bestanden, 0 Fehler, 0 übersprungen | 244 s |

Der zusätzliche vorangegangene Browserlauf mit zwei Tests bestand ebenfalls;
seine Tests sind in den 16 Tests enthalten und werden nicht doppelt gezählt.

## Tatsächliche Umgebung

- Windows-Host mit SDK aus `global.json` und echten lokalen API-/Studio-Prozessen.
- Keycloak **26.7.3** und PostgreSQL **17-alpine** unter WSLC im dedizierten Teststack.
- Öffentlicher lokaler Issuer: `http://localhost:58080/realms/vertexbpmn`.
- `OidcTest`, nicht `Production`; keine ausgeschaltete JWT-Signaturprüfung.
- Neu erzeugte Test-Secrets; keine produktiven Zugangsdaten.
- Builds mit ausdrücklich aktivierter diagnostischer Analyzer-Ausnahme. Die Ergebnisse sind kein Nachweis eines strikten Analyzer-Builds.

## Nachgewiesenes Verhalten

Der bestehende Playwright-/API-Lauf prüfte echte Anmeldung, API-Zugriff,
Session-Refresh und Logout; Rollen und getrennte Mandanten; manipulierte,
unsignierte, fehlerhafte und abgelaufene Tokens sowie falschen Issuer/Audience;
Rollenentzug und deaktivierte Konten; parallele Sitzungen und Refresh;
Keycloak-Ausfall/Neustart, Signing-Key-Rotation, TOTP, Proxy-/Redirect-Grenzen
und die vorhandenen Replica-/alternativen-Issuer-Szenarien.

Der neue `KeycloakGitLocalAcceptanceTests` verwendet den echten
`KeycloakSourceControlActorResolver`, den persistenten Credential-Service,
Data Protection, einen relationalen SQLite-Store und persistenten Audit-Service.
Es wurden direkte Rollen, geerbte Gruppenrollen, Rollenentzug, deaktivierte und
gelöschte Benutzer, Mandantenwechsel, Credential-Mandantenbindung, ungültige
Secrets und tatsächliche Service-Account-Secret-Rotation geprüft.

Besonders wichtig: Ein Commit wurde mit aktuell berechtigtem Keycloak-Benutzer
angenommen und geclaimt. Nach Entzug der Schreibrollen wies der echte
`SourceControlCommitExecutor` die Ausführung mit einer erneuten Keycloak-Abfrage
ab. Ohne Read wurde `NotFound`, mit ausschließlich `ReadOnly` wurde `Forbidden`
nachgewiesen. Kein Workspace und kein gespeichertes Commit-Ergebnis entstanden.
Die anfängliche Test-Erwartung `Forbidden` ohne jegliche Rolle war falsch;
sie wurde nach Prüfung von `RepositoryAccessPolicy` auf das ausdrücklich
vorgesehene Verbergen der Repository-Existenz korrigiert. Zusätzlich blieb
die getrennte `ReadOnly`-/`Forbidden`-Assertion bestehen. Produktlogik wurde
hierfür nicht abgeschwächt.

Der Service-Account funktionierte mit `view-users`, `query-clients` und
`view-clients`, ohne `realm-admin`. Absolute Minimalität dieses Berechtigungssatzes
wurde nicht bewiesen.

## Reproduzierbarkeit und Änderungen

Siehe [lokale Anleitung](../testing/git-keycloak-local-acceptance.md).
Der vorhandene Runner unterstützt jetzt `-GitIdentityAcceptance`; diagnostische
Builds erfordern ausdrücklich `-DiagnosticBuild`. Die normale GitHub-CI wurde
nicht erweitert. Der neue Live-Test benötigt ein explizites Opt-in.

Ausgeführte Abnahmeaufrufe:

```powershell
.\scripts\keycloak-oidc-e2e.ps1 -DiagnosticBuild -KeepKeycloak
.\scripts\keycloak-oidc-e2e.ps1 -DiagnosticBuild -SecurityAcceptance -KeepKeycloak

# Gegen den währenddessen bereitstehenden dedizierten Realm:
$env:VERTEXBPMN_KEYCLOAK_GIT_ACCEPTANCE = '1'
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj -c Release --no-build --no-restore --filter-class '*KeycloakGitLocalAcceptanceTests' --timeout 10m

# Separater lokaler Git-Regressionslauf:
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj -c Release --no-build --no-restore --filter-not-class '*PostgresAcceptanceTests' --filter-not-class '*KeycloakGitLocalAcceptanceTests' --max-parallel-test-modules 1 --timeout 10m
```

Die vorausgehenden Projekt-Builds verwendeten `RunAnalyzers=false`,
`EnforceCodeStyleInBuild=false`, `TreatWarningsAsErrors=false` und einen einzelnen
Build-Prozess. Editor-Assets waren unverändert und wurden nicht neu generiert.
Check-only-Whitespace-Prüfung des neuen C#-Tests, PowerShell-Syntaxprüfung des
Runners und Diff-Whitespace-Prüfung bestanden. Der Clean-Code-Skill wurde für
den neuen Test mit Kandidatensuchen D1–D7 und manueller Prüfung der acht Regeln
angewendet; keine bestätigten verbleibenden Clean-Code-Funde in diesem Umfang.

## Ausdrücklich weiterhin offen

- Alle neuen Source-Control-API-Endpunkte mit echtem JWT und vollständiger ACL-Matrix.
- Vollständiger Studio-Git-Ablauf inklusive erfolgreichem Commit und Push sowie späteren uncommitteten Editoränderungen.
- Push-spezifischer Rollenentzug nach Annahme eines bereits bestehenden Push-Auftrags.
- Verschachtelte Composite-Rollen und gesonderter Keycloak-Ausfall während eines Git-Auftrags.
- Der vollständige WSLC-Compose-Appstack: API/Studio wurden hier als Host-Prozesse getestet, nicht als Bridge-Netzwerk-Container.
- Produktionsprofil, TLS und Produktionsfreigabe: vom Benutzer ausdrücklich vorerst ausgenommen.

## Bereinigung

API und Studio wurden nach den Browserläufen beendet. Anschließend wurden
ausschließlich die in diesem Lauf erzeugten dedizierten Keycloak-/PostgreSQL-
Container, deren Datenvolume und Netzwerk entfernt. Die flüchtigen Testdaten
dieses Volumes sind damit gelöscht, nicht wiederherstellbar; Testberichte und
lokale Laufzeit-Logs bleiben vorhanden. Keine Commits oder Pushes durchgeführt.
