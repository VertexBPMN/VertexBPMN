# Lokale Git-/Keycloak-Identitätsabnahme

Diese Abnahme ist ausdrücklich lokal und kein Produktionsfreigabe-Nachweis.
Der vorhandene Runner startet einen isolierten Keycloak-/PostgreSQL-Stack unter
WSLC und echte API-/Studio-Prozesse auf dem Windows-Host. Die schnelle
GitHub-Testpipeline benötigt diese Infrastruktur nicht und bleibt unverändert.

## Voraussetzungen

- SDK gemäß `global.json`, WSLC und die vorhandenen Browser-Testabhängigkeiten.
- Freie lokale Ports 58080, 51870 und 5263; zusätzliche Security-Tests verwenden weitere lokale Ports.
- Die vier im Runner genannten Prozess-Secrets müssen gesetzt sein. Keine echten Produktions-Secrets verwenden oder im Chat angeben.
- Ausschließlich der dedizierte Stack `vertexbpmn-keycloak-test`; kein produktiver Realm.
- Unveränderte beziehungsweise separat gebaute BPMN-Editor-Assets.

## Wiederholbarer Lauf

```powershell
.\scripts\keycloak-oidc-e2e.ps1 -SecurityAcceptance -GitIdentityAcceptance
```

Wenn bestehende Analyzer-Schulden den strikten Build blockieren, kann die
**funktionale lokale** Abnahme ausdrücklich mit `-DiagnosticBuild` laufen.
Dieser Schalter deaktiviert Analyzer nur für die gestarteten Builds und
ändert die Repository-Regeln nicht. Er ist kein erfolgreicher strikter Build.

Ohne `-KeepKeycloak` entfernt der Runner am Ende seine dedizierten Container,
das Test-Datenvolume und das Testnetzwerk. API und Studio werden auch bei einem
Fehler beendet. Vorhandene, fremde Ressourcen dürfen nicht diese Testnamen verwenden.

## Neuer Git-Identitätstest

`KeycloakGitLocalAcceptanceTests` läuft nur mit explizitem Prozess-Opt-in
`VERTEXBPMN_KEYCLOAK_GIT_ACCEPTANCE=1`, das der Runner für diesen Schritt setzt
und anschließend zurücksetzt.

Der Test erstellt eindeutig benannte Benutzer, Gruppe und Service-Account im
dedizierten Realm. Die Abfragen erfolgen durch den echten
`KeycloakSourceControlActorResolver`, nicht durch einen HTTP-Stub.
`PersistentCredentialService`, Data Protection, relationaler SQLite-Store und
persistenter Audit-Service werden tatsächlich verwendet. Die SQLite-Datenbanken
sind für diesen isolierten Test im Speicher; ein Neustartnachweis ist damit nicht erbracht.

Geprüft werden:

- Direkte API-Client-Rollen sowie eine geerbte Gruppenrolle.
- Rollenentzug ohne Rollen-Cache und erneute Prüfung eines bereits angenommenen,
  geclaimten Commit-Auftrags durch den echten `SourceControlCommitExecutor`.
- Ablehnung ohne Leserecht (`NotFound`, um Repository-Existenz zu verbergen) und
  Ablehnung mit ausschließlich `ReadOnly` (`Forbidden`). Kein Workspace oder
  Commit-Ergebnis darf dabei entstehen.
- Deaktivierter und gelöschter Benutzer sowie geändertes `tenant_id`.
- Mandantenbindung und verschlüsselte Speicherung des Service-Account-Secrets.
- Ungültiges Secret, Wiederherstellung sowie echte Secret-Rotation in Keycloak
  und anschließende Aktualisierung des geschützten Credentials.

Der Service-Account erhält `view-users`, `query-clients` und `view-clients`,
keine `realm-admin`-Rolle. Das belegt die Funktionsfähigkeit dieses lesenden
Berechtigungssatzes, nicht dessen mathematisch minimale Rechtevergabe.

## Grenzen

Der neue Test führt absichtlich keinen erfolgreichen Remote-Push aus und
kontaktiert kein GitHub-Repository. Die Auftragsprüfung muss **vor** Git-I/O
abbrechen. Erfolgreicher Push und seine abschließende Autorisierung benötigen
eine separate Git-Transport-/Auftragsabnahme. Die vorhandenen nativen lokalen
Transporttests sind ebenfalls kein Nachweis des kompletten Studio-Push-Ablaufs.

Der Browser-/API-Security-Lauf prüft den bestehenden OIDC-Anschluss. Er ist
keine vollständige Abnahme aller neuen `/api/source-control`-Endpunkte oder
aller Bedienelemente des Git-Panels. Auch verschachtelte Composite-Rollen,
Refresh während eines Git-Auftrags und Push-spezifischer Rollenentzug sind
gesondert nachzuweisen.
