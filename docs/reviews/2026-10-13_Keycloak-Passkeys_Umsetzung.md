# Keycloak-Passkeys: Umsetzung und Abnahme

Stand: 2026-10-13. Branch: `codex/keycloak-passkeys`.
Basis: `240d03ff81e5950251803fbbee9ff3d6068633e8` (`master`).
Auftrag: [Delta-Umsetzungsplan](../architecture/2026-10-11_Keycloak-Passkeys_Umsetzungsplan.md).
Status: **MVP implementiert und lokal abgenommen.** Keine Produktionsfreigabe.
Fortsetzung des ursprünglichen MVP: [P3b/T14/T15](2026-10-13_Keycloak-Passkeys_AIA-und-Tenant-Abnahme.md).
Dieser Bericht dokumentiert den ursprünglichen MVP-Stand; P3b, T14 und T15
sind inzwischen im verlinkten Folgepaket ebenfalls lokal abgenommen.

## Umfang und Status

- P0: SQLite-Baselinefehler nach ausdrücklichem Auftrag behoben; danach 2000
  erfolgreiche CI-safe Tests und 8 erwartungsgemäße externe Skips.
- P1: Passwordless Policy und aktivierte, nicht verpflichtende
  `webauthn-register-passwordless`-Aktion im lokalen Realm. Idempotenter Bootstrap
  liest dieselbe Policy aus dem Template und ergänzt die Aktion bei Bedarf.
- P2: Dedizierter `vertexbpmn-passkey-user`, Tenant `tenant-a`, Rolle `ReadOnly`.
  Passwort aus bestehender Prozessvariable, Reset der WebAuthn-Credentials und
  Required Actions; opt-in Einschreibung über `-RequirePasskeyEnrollment`.
  Nach bestandenem Browserlauf erneut provisioniert: genau ein Benutzer,
  Registrierung wieder erforderlich und keine alten Passkey-Credentials.
- P3a: Startvalidierte optionale `StudioAuthentication:AccountManagementUrl`;
  SSO-Link **Manage sign-in methods** mit `_blank`, `noopener noreferrer` und
  `data-testid=sso-manage-sign-in`. Ohne Konfiguration kein Link.
- P3b/T14: Im ursprünglichen MVP nicht umgesetzt (optionaler Ausbau im Plan). Account Console
  ist die Verwaltung im MVP; kein eigener Registrierungsendpunkt, kein
  `kc_action`-/PAR-Vertrag oder Erfolgsversprechen nach einem Redirect.
- P4: T13 mit virtuellem Chromium-Authenticator und serverseitigem
  Credential-Nachweis implementiert und lokal gegen Keycloak 26.7.3 bestanden
  (1/1, 12,876 Sekunden). Separater Schalter `-PasskeyAcceptance`;
  bestehende Security-Läufe schließen die neue Kategorie ohne diesen Schalter
  aus. T15 war im ursprünglichen MVP offen; die ergänzte Abnahme steht im Folgebericht.
- P5: README, Runbook, Changelog und dieser Bericht aktualisiert. Finale
  Build-/CI-safe-Prüfung und kombinierte lokale Browserabnahme bestanden.

Die SSO-Seite ist gewählt, weil dort die vorhandenen Providerintegrationen
aufgeführt sind. Der neue Link verwendet einen generischen Konfigurationswert
und bindet Studio nicht an Keycloak. Claims, `SaveTokens`, Token-Store und
Cookie-Refresh bleiben unverändert. Keine neuen Pakete oder produktiven
Datenbankmigrationen; keine Commits, Pushes oder Deployments.

## Geänderte Dateien

| Bereich | Dateien |
| --- | --- |
| Baseline-Reparatur | `tests/VertexBPMN.Tests/Infrastructure/SharedSqliteDbFixture.cs`, `CustomWebApplicationFactory.cs`, `SharedSqliteDbFixtureIsolationTests.cs` |
| Realm/Provisionierung | `deploy/keycloak/vertexbpmn-realm.json`, `scripts/keycloak-oidc-test.ps1` |
| Studio | `src/VertexBPMN.Studio/Program.cs`, `StudioAccountManagement.cs`, `Components/Pages/SSO.razor` |
| Schnelle Regression | `tests/VertexBPMN.Tests/Unit/Security/KeycloakRealmTemplateTests.cs`, `StudioAccountManagementTests.cs` |
| Browserabnahme | `tests/VertexBPMN.Studio.UiTests/KeycloakOidcSecurityAcceptanceTests.cs` (`partial` und veraltete T06-Refresh-Erwartung), `KeycloakOidcSecurityAcceptanceTests.Passkeys.cs`, `StudioUiContractTests.cs`, `scripts/keycloak-oidc-e2e.ps1` |
| Dokumentation | `deploy/keycloak/README.md`, `docs/runbooks/studio-authentication.md`, `CHANGELOG.md`, dieser Bericht und P0-Bericht |

Der vorgefundene unversionierte Plan sowie die beiden Compilerargument-Dateien
wurden nicht überschrieben. `.github/workflows/ci.yml` bleibt unverändert.

## Versions- und Realmprüfung

Feldnamen und Werte wurden gegen den
[Keycloak-26.7.3-Quellcode](https://github.com/keycloak/keycloak/blob/26.7.3/core/src/main/java/org/keycloak/representations/idm/RealmRepresentation.java),
die [Admin-UI derselben Version](https://github.com/keycloak/keycloak/blob/26.7.3/js/apps/admin-ui/src/authentication/policies/WebauthnPolicy.tsx)
und die tatsächlich gestartete Instanz geprüft. Der Export bestätigte:
Passkeys `true`, RP ID `localhost`, User Verification und Resident Key
`required`, Attestation `none`, Authenticator Attachment `not specified`.

Ein explizites `requiredActions`-Array ersetzt beim frischen Import die
Keycloak-Standardaktionen. Deshalb enthält das Template die 14 unveränderten
Standardaktionen der gepinnten Version, aus einem neuen, anschließend gelöschten
Probe-Realm exportiert. Insbesondere bleiben `CONFIGURE_TOTP`, Profil-/E-Mail-
Prüfung und Passwortverwaltung aktiviert. Regressionstests schützen diese
Abgrenzung. Referenz:
[DefaultRequiredActions.java](https://github.com/keycloak/keycloak/blob/26.7.3/server-spi-private/src/main/java/org/keycloak/models/utils/DefaultRequiredActions.java).

Der erste Browserversuch wurde nach Erkennen dieser Importsemantik bewusst
abgebrochen und seine isolierten Ressourcen entfernt; er gilt nicht als
bestandene Abnahme. Die Wiederholung verwendet einen frischen Import des
korrigierten Templates.

Ein weiterer kombinierter Versuch endete ohne vollständige Zusammenfassung.
Seine protokollierten Fehler wurden bearbeitet:

- T13: Der virtuelle Authenticator simulierte ständig Anwesenheit, wodurch
  Conditional UI unmittelbar nach Logout erneut anmelden konnte. Anwesenheit
  wird jetzt vor Logout ausgesetzt und vor dem bewusst gestarteten neuen
  WebAuthn-Aufruf wieder aktiviert. Eine Aktivierung erst nach dem Aufruf ließ
  dessen Anwesenheitsprüfung im fokussierten Versuch blockiert. Der finale Test
  prüft zusätzlich, dass das virtuelle Credential discoverable ist, den RP ID
  `localhost` besitzt und sein Signaturzähler beim erneuten Login steigt.
  Registration, serverseitiger Credential-Nachweis,
  Cookie-Entzug und API-/Tenant-Prüfungen bleiben Pflicht.
- T06 Kontosperre: Der vorhandene Test erwartete noch `opaqueredirect`/Status 0.
  Der bereits auf `master` vorhandene Background-Refresh-Endpunkt gibt für
  verworfene Sitzungen ausdrücklich 401 ohne OIDC-Challenge zurück. Der Test
  verlangt nun `basic`, HTTP 401 und unveränderte Refresh-URL; Cookie-Entzug,
  abgewiesener Passwortgrant und abgewiesene interaktive Neuanmeldung bleiben
  unverändert. Kein Produkt-Refreshcode wurde geändert oder abgeschwächt.

Die Wiederholung prüft T13 zuerst fokussiert und danach die kombinierte Suite.
Diese älteren Versuche gelten nicht als bestandene Abnahme.

Ein neuerer Patch [26.7.4](https://www.keycloak.org/2026/09/keycloak-2674-released)
ist verfügbar; die Version wurde gemäß Plan nicht geändert.

## Tatsächliche Prüfungen

- Windows/PowerShell, .NET SDK `10.0.303`, WSLC `3.0.3.0`.
- Release-Build für Haupttests nach Baseline-Reparatur: bestanden, 0 Warnungen
  und 0 Fehler. Gezielte SQLite-/API-Tests: 3/3 bestanden.
- CI-safe Baseline nach Reparatur: 2000 bestanden, 8 externe Skips, 0 Fehler.
- Gezielte Realm-, Account-URL-, Claims-, Session- und SQLite-Prüfungen:
  40/40 bestanden (vor Ergänzung der Standardaktions-Regression im selben Test).
- UI-Test `Sso_OmitsAccountManagementLink_WhenUnconfigured`: 1/1 bestanden;
  echtes Chromium mit lokalem Studio und Fixture-API.
- T13 fokussiert: **1/1 bestanden**, 0 Fehler/Skips, 12,876 Sekunden.
  Die tatsächliche Keycloak-Seite bestätigte `#registerWebAuthn` (**Register**)
  und `#authenticateWebAuthnButton` (**Sign in with Passkey**). Die Assertionen
  belegen ein serverseitiges `webauthn-passwordless`-Credential, einen
  steigenden Signaturzähler, `tenant-a`, eine geladene API-gestützte
  Process-Definitions-Seite und den korrekt konfigurierten Account-Link.
- PowerShell-Parserprüfung beider Skripte: keine Syntaxfehler.
- `git diff --check` mit `cr-at-eol` für vorhandene CRLF-Dateien: bestanden.
- Ein CI-safe Lauf während paralleler Keycloak-Arbeit: 2017 bestanden,
  1 FEEL-Zeitüberschreitungsfehler in `C_8_0_Test.Test_C_8_0_Bpmn`, 8 Skips.
  Keine Assertions, Fixtures oder Laufzeitlimits abgeschwächt; Wiederholung
  wurde getrennt von der ressourcenintensiven Browser-/VM-Abnahme ausgeführt.
- Finale serielle Wiederholung mit korrigierter Standardaktionsliste:
  Release-Build 0 Warnungen/Fehler (54,72 Sekunden); CI-safe Suite 2026 Tests,
  **2018 bestanden, 0 Fehler, 8 externe Skips**, Dauer 2:15,577 Minuten.
  Der FEEL-Zeitüberschreitungsfehler trat dabei nicht erneut auf.
- Kombinierte reale Keycloak-Suite (bestehende K05-Fälle, drei lokale
  OIDC-/Sessionfälle und T13): **18/18 bestanden**, 0 Fehler/Skips, 347,768
  Sekunden. Insbesondere TOTP-Einschreibung und Folge-Login, Kontosperre mit
  HTTP 401/Cookie-Entzug, Rollenentzug, Tenantisolation, Signaturrotation,
  IdP-Ausfall/Wiederanlauf, Proxy/PAR, Replikaneustart und unabhängiger
  OIDC-Issuer bestanden. Die TOTP-Prüfung betrifft ein Konto ohne Passkey;
  sie entscheidet E1 nicht.
- Abschließender vollständiger Release-Build nach allen Browser-Testkorrekturen:
  bestanden, 0 Warnungen/Fehler, 9,62 Sekunden. Das zentrale CI-safe-Testprojekt
  wurde seit seinem bestandenen seriellen Lauf nicht mehr geändert.

Befehle und Artefakte (repository-relativ):

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
# CI-safe dotnet-test-Befehl mit allen Ausschlüssen exakt aus .github/workflows/ci.yml;
# vollständiger Befehl auch im P0-Bericht.
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-class '*StudioAccountManagementTests' --filter-class '*KeycloakRealmTemplateTests' --filter-class '*VertexOidcClaimsTests' --filter-class '*OidcSessionTokenStoreTests' --filter-class '*SharedSqliteDbFixtureIsolationTests' --filter-class '*BpmnEdgeCaseApiTests'
dotnet tests/VertexBPMN.Studio.UiTests/bin/Release/net10.0/VertexBPMN.Studio.UiTests.dll -method '*Sso_OmitsAccountManagementLink_WhenUnconfigured' -parallelMode none
./scripts/keycloak-oidc-e2e.ps1 -PasskeyAcceptance -TestInstance passkeys-20261013c -TestMethod '*T13_*' -KeepKeycloak
./scripts/keycloak-oidc-e2e.ps1 -SecurityAcceptance -PasskeyAcceptance -TestInstance passkeys-20261013c -KeepKeycloak
```

Lokale Logs liegen unter `tests/VertexBPMN.Tests/TestResults/passkeys-*` und
`tests/VertexBPMN.Studio.UiTests/TestResults/keycloak-oidc/` (ignorierte
Testartefakte). Es wurden ausschließlich ephemere Prozess-Secrets erzeugt;
keine Secretwerte in Quellcode, Konfiguration oder Bericht gespeichert.
`-TestInstance` wurde ergänzt, um das vorgefundene Volume
`vertexbpmn-keycloak-postgres-test-data` zu erhalten. Der Suffix gilt für
Container, Netzwerk und Volume und wird an Readiness-Subprozesse vererbt.
Der direkte xUnit-Runner unterstützt laut seiner lokalen Hilfe
`-result-xml`; der E2E-Lauf schreibt deshalb zusätzlich
`tests/VertexBPMN.Studio.UiTests/TestResults/keycloak-oidc/acceptance.xml`.
Der bestandene fokussierte Lauf ist zusätzlich als
`tests/VertexBPMN.Tests/TestResults/passkeys-t13-final.xml` gesichert.
Die vollständige kombinierte Zusammenfassung liegt zusätzlich unter
`tests/VertexBPMN.Tests/TestResults/passkeys-combined-final.xml`.
Die finale Realmprüfung (sechs Policy-Werte und 14 Standardaktionen) ist unter
`tests/VertexBPMN.Tests/TestResults/passkeys-realm-final-verification.json`
festgehalten.
Nach dem kombinierten Lauf bestand der erneute Bootstrap mit Credential-Reset.
Alle vier Ressourcen der finalen Instanz `passkeys-20261013c` sind nachweislich
entfernt (zwei Container, Netzwerk, Volume). Die beiden früheren Instanzen wurden
ebenfalls bereinigt. Das vorgefundene Volume
`vertexbpmn-keycloak-postgres-test-data` ist weiterhin mit ursprünglichem
Erstellungszeitpunkt `2026-10-07T10:14:13Z` vorhanden.

## C#-Abschlussreview

Skill: `.agents/skills/dotnet-clean-code-review/SKILL.md` direkt gelesen und
angewendet; Scope: die zehn geänderten C#-Dateien, Basis `240d03f`.
Nur neue/geänderte Mitglieder bewertet; keine generierten oder fremden Dateien.
D1–D7 mit `rg` durchgeführt, D8 durch Lesen der Änderungen und Aufrufstellen.
Keine bestätigten Clean-Code-Befunde in den eigenen Änderungen.

| Regel | Ergebnis / Evidenz |
| --- | --- |
| 1. Async/Cancellation | PASS: awaitable Tests und Hilfsmethoden; HTTP mit Test-CancellationToken; Playwright besitzt eigene Timeouts. Dialogcallback ist ein echter Eventhandler. |
| 2. Collections | PASS: bestehender lazy JSON-Namensiterator unverändert; keine neuen unklaren Collectionverträge. |
| 3. Boolean-Modi | PASS: keine neuen unklaren bool-Parameter in C#; Authenticatorfähigkeiten sind Protokolldaten. |
| 4. Namen | PASS: `StudioAccountManagement` kapselt ausschließlich die Account-URL-Validierung. |
| 5. Struktur | PASS: keine neuen Regions, kleine Validator- und Testhilfsmethoden. |
| 6. Interfaces | N/A: keine neuen oder geänderten Interfaceverträge. |
| 7. Vererbung | PASS: bestehende xUnit-/WebApplicationFactory-Verträge beibehalten. |
| 8. Lesbarkeit | PASS für Änderungen; keine opportunistische Bereinigung bestehenden Codes. |

`dotnet format whitespace VertexBPMN.sln --verify-no-changes --no-restore
--include <zehn Scope-Dateien>`: Exitcode 2, 29 ausschließlich vorbestehende
Whitespace-Befunde in `src/VertexBPMN.Studio/Program.cs`. Die dortige einzige neue
Registrierungszeile ist nicht betroffen. Alle übrigen Scope-Dateien ohne Befund.
Formatcheck ist daher **nicht vollständig grün**, und es wurde kein Formatter
im Schreibmodus ausgeführt. Das Review ersetzt keine produktive Securityabnahme.
Die drei betroffenen Browser-Testdateien wurden nach den Korrekturen nochmals
separat im Check-only-Modus geprüft: Exitcode 0, keine Formatbefunde. Ihr
Release-Build bestand mit 0 Warnungen und Fehlern (14,44 Sekunden).

## Betreiberentscheidungen und Grenzen

- E1 bleibt offen: Passkey als Ersatz für TOTP oder zusätzliche TOTP-Pflicht.
  Kein eigener Browserflow und kein Test, der eine MFA-Entscheidung vorwegnimmt.
- E2: zusätzlicher Loginweg für Pilotbenutzer; kein Passkey-only-Zwang.
- E3: Attestation `none`, kein neuer Hardwarezwang.
- E4: SSO-Seite als oben begründeter MVP.
- Produktiver RP ID, Recovery, Unternehmens-SSO/Offboarding, Upgrade und externe
  Freigabe bleiben Betreiberaufgaben. Keine Produktionsabnahme oder Publikation.
- T14/PAR und optional T15 sind nicht durchgeführt, weil sie hier nicht
  im ursprünglichen MVP implementiert wurden; inzwischen im Folgepaket bestanden.
  T13 und die bestehenden K05-Fälle sind lokal bestanden.
  Der vorhandene generische PAR-Fall T10 ist bestanden; ein Passkey-AIA-/PAR-
  Nachweis T14 wird daraus nicht abgeleitet.

## Nächster konkreter Schritt

Den lokalen Branch-Diff einschließlich der dokumentierten Baseline-/T06-
Testkorrekturen prüfen. Commit, Push und Produktionsrollout benötigen einen
gesonderten Auftrag. Vor produktivem Pilotbetrieb legt der Betreiber RP ID,
Recovery, Unternehmens-SSO/Offboarding und E1 fest; die Account-Console-URL wird
über Deploymentkonfiguration gesetzt. Der optionale AIA-Ausbau benötigt die
separate T14-Abnahme einschließlich Abbruchpfad und PAR.
