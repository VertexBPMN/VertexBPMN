# Keycloak-Passkeys: P0-Baseline

Stand: 2026-10-13. Branch: `codex/keycloak-passkeys`.
Basis: `240d03ff81e5950251803fbbee9ff3d6068633e8` (`master`, Merge von PR #54).

## Status

Historischer Erstbefund: Die unveränderte Baseline hatte einen Fehler in der CI-safe Testsuite.
Gemäß P0 des [Umsetzungsplans](../architecture/2026-10-11_Keycloak-Passkeys_Umsetzungsplan.md)
wurde die Produktimplementierung P1–P5 nicht begonnen. Keine Commits, Pushes,
Deployments, Migrationen oder Änderungen an Claims, Realm, Studio und Tests.
Die bereits vorhandenen unversionierten Plan- und Compilerargument-Dateien bleiben erhalten.

## Ausgeführte Prüfungen

- Windows/PowerShell; .NET SDK `10.0.303` (zugelassener Patch zu `global.json` mit `10.0.302`).
- `wslc.exe` verfügbar, Version `3.0.3.0`.
- `dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers`: bestanden, 0 Warnungen, 0 Fehler, 4:07,67 Minuten. Editorassets unverändert; kein Restore notwendig.
- CI-safe Testsuite mit dem unveränderten Befehl und sämtlichen Ausschlüssen aus `.github/workflows/ci.yml`, Schritt **Test (CI-safe suite)**: 2007 Tests, 1998 erfolgreich, 1 fehlgeschlagen, 8 übersprungen. Dauer 2:26,687 Minuten; `dotnet test` Exitcode 1, Testplattform Exitcode 2.

Ausgeführter Testbefehl (PowerShell, repository-relativ):

```powershell
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-not-trait "Category=Phase3ExternalAcceptance" --filter-not-trait "Category=Phase4OutageAcceptance" --filter-not-trait "Category=Phase5RecoveryAcceptance" --filter-not-trait "Category=Phase6LoadAndSoakAcceptance" --filter-not-trait "Category=Phase7ConformanceAcceptance" --filter-not-trait "Category=Phase8MonitoringAcceptance" --filter-not-trait "Category=ExternalTaskPostgres" --filter-not-trait "Category=ExternalAgentProcessRecovery" --filter-not-trait "Category=ContractReviewOllama" --filter-not-trait "Category=ContractReviewBenchmark" --filter-not-class "VertexBPMN.Tests.Conformance.DistributedConformanceTests" --max-parallel-test-modules 1
```

Fehler im Test
`VertexBPMN.Tests.Integration.Api.BpmnEdgeCaseApiTests.Deploy_And_Execute_Bpmn_With_EventSubprocess_And_BoundaryError_Works`:

```text
Microsoft.Data.Sqlite.SqliteException:
SQLite Error 5: 'unable to delete/modify user-function due to active statements'.
```

Der Stack führt über `SqliteConnection.CreateFunctionCore`,
`SqliteRelationalConnection.InitializeDbConnection` und
`ProcessDefinitionRepository.GetLatestByKeyAsync` zum Prozessstart in
`BpmnEdgeCaseApiTests.cs:32`. Das ist ein Baselinebefund vor Passkeyänderungen;
eine abschließende Ursachenanalyse oder Reparatur wurde nicht vorgenommen.

## Fortsetzung nach ausdrücklichem Reparaturauftrag

Der anschließende Benutzerauftrag „mach es“ autorisierte die Behebung und
Fortsetzung. Ursache: Mehrere EF-Kontexte nutzten dieselbe offene
SQLite-Verbindung. Ein noch aktiver Reader konnte dadurch die
Funktionsregistrierung bei Initialisierung eines weiteren Kontexts blockieren.
Die Fixture hält jetzt eine benannte In-Memory-Datenbank über eine Ankerverbindung
offen; die Kontexte und der Testhost verwenden eigene Verbindungen zu dieser
Datenbank. Getrennte Fixture- und Kontextdatenbanken sowie explizite persistente
Testprofile bleiben erhalten. Connection Pooling ist für die Fixture deaktiviert,
damit deren Lebensdauer beim Freigeben der Ankerverbindung endet.

Ein neuer Regressionstest öffnet einen Reader im ersten Kontext und liest mit
einem zweiten Kontext dieselben Daten. Der vorhandene API-Test wurde nicht
abgeschwächt. Release-Build: 0 Warnungen, 0 Fehler. Gezielte Tests: 3/3 bestanden.
Erneute CI-safe Baseline: 2008 Tests, 2000 bestanden, 0 Fehler, 8 externe Fälle
übersprungen, 2:10,356 Minuten. Damit wurde P0 freigegeben und die
Passkeyimplementierung begonnen. Weitere Ergebnisse stehen im
[Umsetzungsbericht](2026-10-13_Keycloak-Passkeys_Umsetzung.md).

## Versionsverifikation und lokale Abnahme

Die Policy-Feldnamen wurden direkt gegen den [Keycloak-26.7.3-Quellcode](https://github.com/keycloak/keycloak/blob/26.7.3/core/src/main/java/org/keycloak/representations/idm/RealmRepresentation.java)
und die [Admin-UI derselben Version](https://github.com/keycloak/keycloak/blob/26.7.3/js/apps/admin-ui/src/authentication/policies/WebauthnPolicy.tsx) geprüft:

- `webAuthnPolicyPasswordlessPasskeysEnabled`: Boolean.
- `webAuthnPolicyPasswordlessUserVerificationRequirement`: `required`.
- `webAuthnPolicyPasswordlessResidentKey`: `required` (Discoverable credential).
- `webAuthnPolicyPasswordlessAttestationConveyancePreference`: `none`.
- `webAuthnPolicyPasswordlessAuthenticatorAttachment`: `not specified` (keine Einschränkung).
- `webAuthnPolicyPasswordlessRpId`: lokaler Zielwert `localhost`.

**Noch nicht durch Realm-Import oder Admin-Export bestätigt.** Die lokale Instanz
wurde nicht gestartet. Beide dedizierten Keycloak-Testcontainer sind nicht vorhanden;
das vorhandene Volume `vertexbpmn-keycloak-postgres-test-data` wurde nicht verändert.
Die vier benötigten Prozess-Secrets sind nicht gesetzt. Ein bestehendes Volume
darf nicht mit erfundenen Ersatzpasswörtern übernommen oder zur Abnahme gelöscht werden.

[Keycloak 26.7.4](https://www.keycloak.org/2026/09/keycloak-2674-released)
ist als neuerer Patch bestätigt. Keine Änderung an der gepinnten Version.

## Nächster Schritt

Die Baseline-Reparatur und erneute P0-Prüfung sind abgeschlossen. P1–P5 und die
separat isolierte Keycloak-Abnahme werden im oben verlinkten Umsetzungsbericht
geführt. Das vorgefundene Datenbank-Volume bleibt unangetastet. E1 (TOTP bei
Passkeylogin) bleibt eine Betreiberentscheidung; es wurde kein Verhalten festgelegt.
