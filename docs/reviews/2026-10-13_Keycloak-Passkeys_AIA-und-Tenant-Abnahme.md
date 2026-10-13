# Keycloak-Passkeys: P3b, T14 und T15

Stand: 2026-10-13. Branch: `codex/keycloak-passkeys`.
Fortsetzung nach Benutzerauftrag „mach weiter“. Basis unverändert `240d03f`;
der bereits lokal bestandene [MVP](2026-10-13_Keycloak-Passkeys_Umsetzung.md)
und fremde unversionierte Dateien bleiben erhalten. Kein Commit, Push oder Deployment.
Status: **P3b, T14 und T15 implementiert und lokal abgenommen.**

## Umsetzung

- Neue opt-in Einstellung `StudioAuthentication:PasskeyRegistrationEnabled`
  (Standard `false`). Sie vermeidet Keycloak-spezifische Aktionen bei anderen
  OIDC-Providern. UI-Testauth und lokale Entwicklungsidentitäten bleiben ausgeschlossen.
- SSO-Link **Add a passkey** und autorisierte Route
  `/authentication/passkey/register`. Fester Aktionsname, keine Benutzereingabe.
  Unautorisierte Aufrufe starten zuerst die normale Anmeldung. Rücksprünge
  werden gemeinsam mit dem Loginpfad nach denselben lokalen Regeln normalisiert.
- `OnRedirectToIdentityProvider` ergänzt die konstante `kc_action` ausschließlich
  für die interne Action-Property. State, Nonce, PKCE und PAR bleiben beim
  Handler. `SaveTokens`, Claims, Token-Store und Refresh bleiben unverändert.
  Kein Rückgabestatus oder Erfolgsbanner wird interpretiert.
- T14 prüft echten PAR, Registrierung, Abbruch ohne Credential, intakte Sitzung
  und Normalisierung von `//attacker.example/` auf `/sso`.
- T15 verwendet einen separaten Benutzer: Einschreibung mit gültigem Tenant,
  danach echte Passkeyanmeldung. Unmittelbar vor Code-Einlösung wird der Tenant
  über die Admin-API entfernt. Dadurch stellt Keycloak echte signierte Tokens
  ohne Tenant aus, ohne dass verpflichtende Profilprüfungen deaktiviert oder
  Claims gefälscht werden. Erwartung: kein Studio-Cookie, kein Dashboard,
  Refresh HTTP 401; der Signaturzähler belegt den Passkey. Tenant wird danach
  wiederhergestellt. Der bisherige No-Tenant-Testbenutzer bleibt unverändert.

Dateien: `src/VertexBPMN.Studio/Program.cs`, `StudioPasskeyRegistration.cs`,
`StudioAuthenticationRedirects.cs`, `Components/Pages/SSO.razor`,
`tests/VertexBPMN.Tests/Unit/Security/StudioPasskeyRegistrationTests.cs`,
`tests/VertexBPMN.Studio.UiTests/KeycloakOidcSecurityAcceptanceTests.PasskeyActions.cs`,
`StudioUiContractTests.cs`, beide Keycloak-Skripte, Runbook, README und Changelog.
Keine neuen Pakete oder Datenbankmigrationen.

## Verifikation

Die gepinnte [ASP.NET-Core-10.0.12-Implementierung](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs)
führt das Redirect-Event vor PAR aus. Die
[Keycloak-26.7.3-Required-Action](https://github.com/keycloak/keycloak/blob/26.7.3/services/src/main/java/org/keycloak/authentication/requiredactions/WebAuthnRegister.java)
unterstützt Application Initiated Actions. Quellcode geprüft; reale Abnahme separat.

- Erster Release-Build: 0 Warnungen/Fehler, 1:02,58 Minuten.
- Gezielte Routing-, Account- und Claims-Prüfung: 35/35 bestanden, 0 Skips.
  Zehn neue Fälle mit echtem ASP.NET-OIDC-Handler auf TestServer und statischen
  Metadaten: Endpoint-Gating, anonymes Challenge, fester Aktionsname, PKCE/Nonce
  und geschützter State mit lokaler RedirectUri. Kein Ersatz für echtes PAR.
- Aktualisierter Release-Build: 0 Warnungen/Fehler, 6,88 Sekunden.
- Abschließender vollständiger Release-Build nach allen Browserkorrekturen:
  0 Warnungen/Fehler, 6,77 Sekunden; `git diff --check` mit `cr-at-eol` bestanden.
- CI-safe Suite gemäß unverändertem Workflow: **2028 bestanden, 0 Fehler,
  8 externe Skips**, insgesamt 2036 Tests, 2:06,688 Minuten.
- PowerShell-Syntaxprüfung: beide Skripte ohne Fehler.
- UI-Vertrag ohne konfigurierte Links: 1/1 bestanden (9,723 Sekunden).
- Erster neuer Browserlauf: T13 und T14 bestanden, T15 scheiterte am
  browserseitigen Navigationsrennen. Keine Abnahme des Gesamtlaufs daraus
  abgeleitet. Die überarbeitete T15-Prüfung verwendet einen nativen Browserklick
  und entzieht den Tenant vor der Code-Einlösung; die Sicherheitsassertionen
  bleiben unverändert. Aktueller UI-Build: 0 Warnungen/Fehler, 14,04 Sekunden.
- Überarbeitete Passkey-Suite: **3/3 bestanden**, 0 Fehler/Skips,
  48,251 Sekunden. T13, T14 und T15 wurden gegen echtes Keycloak ausgeführt.
  Die tatsächliche AIA-Abbruchschaltfläche ist `#cancelWebAuthnAIA` (**Cancel**).
  Zwei beobachtete Authorization-Requests mit `request_uri` und die erreichte
  Registrierungsaktion belegen PAR einschließlich `kc_action` bei beiden Pfaden.
  T15 belegt Passkey-Verwendung, fehlendes Studio-Cookie/Dashboard und Refresh 401.
- Kombinierte Sicherheitsregression: **20/20 bestanden**, 0 Fehler/Skips,
  377,589 Sekunden. T13–T15 und sämtliche bisherigen Keycloak-/Sessionfälle
  bestanden gemeinsam. Der frische Studio-Log enthält bei T15 die erwartete
  Tenant-Normalisierungsablehnung; damit ist der konkrete fail-closed-Grund
  neben Cookie-/Dashboard-/Refresh-Assertions nachgewiesen.

## C#-Abschlussreview

Skill `.agents/skills/dotnet-clean-code-review/SKILL.md`, Scope: die sechs
geänderten C#-Dateien dieses Folgepakets, Basis `240d03f`, nur geänderte Mitglieder.
D1–D7 mit `rg` ausgeführt; D8 und Aufrufstellen manuell gelesen.
Keine bestätigten Clean-Code-Befunde in den eigenen Änderungen.

| Regel | Ergebnis / Begründung |
| --- | --- |
| Async/Cancellation | PASS: HTTP und Testhost mit Test-CancellationToken; Playwright übernimmt seine Timeouts und wartet Route-Callbacks ab. |
| Collections | N/A: keine neuen Collection-Rückgabeverträge. |
| Boolean-Parameter | PASS: benannte Konfigurations-/OIDC-Verfügbarkeitsdaten; keine unklaren Betriebsmodi. |
| Namen | PASS: Registrierung und lokale Redirects getrennt nach Verantwortung. |
| Struktur | PASS: kleine Typen, keine neuen Regions. |
| Interfaces | N/A: keine neuen Interfaceverträge. |
| Vererbung | PASS: bestehender xUnit-Fixture-Vertrag unverändert. |
| Lesbarkeit | PASS in Änderungen; keine Bereinigung fremden oder bestehenden Codes. |

`dotnet format whitespace VertexBPMN.sln --verify-no-changes --no-restore
--include <sechs Scope-Dateien>`: Exitcode 2; ausschließlich dieselben 29
vorbestehenden Whitespace-Stellen in `Program.cs`. Die neuen Dateien ohne
Befund. Kein Formatter im Schreibmodus. Nach T15-Korrektur bestand der erneute
Check der Browser-Testdatei mit Exitcode 0.

Ausgeführte Browserbefehle (mit ephemeren Prozess-Secrets):

```powershell
./scripts/keycloak-oidc-e2e.ps1 -PasskeyAcceptance -TestInstance passkeys-aia-20261013b -KeepKeycloak
./scripts/keycloak-oidc-e2e.ps1 -PasskeyAcceptance -SecurityAcceptance -TestInstance passkeys-aia-20261013b -KeepKeycloak
```

Artefakte: `tests/VertexBPMN.Tests/TestResults/passkeys-aia-browser-final.xml`,
`passkeys-aia-browser-final.log`, `passkeys-aia-ci-safe.log` und die Review-/
Formatlogs mit demselben Präfix. Das kombinierte Ergebnis liegt unter
`tests/VertexBPMN.Tests/TestResults/passkeys-aia-combined-final.xml`.
Alle vier isolierten Ressourcen der finalen Instanz sind nachweislich entfernt;
das vorgefundene Volume `vertexbpmn-keycloak-postgres-test-data` besteht mit
ursprünglichem Erstellungszeitpunkt `2026-10-07T10:14:13Z` weiter.

## Handoff

Die lokalen technischen Abnahmen sind abgeschlossen. Der Branch ist noch nicht
committed oder gepusht. Als nächster Schritt kann der vollständige Diff geprüft
und mit gesondertem Auftrag committed werden. Für einen produktiven Pilot setzt
der Betreiber den opt-in Schalter, Account-Console-URL und RP ID und entscheidet
über Reauth-Policy, Recovery und E1; die lokale Abnahme ersetzt keine Freigabe.

## Grenzen

E1 (TOTP zusätzlich oder durch Passkey ersetzt), produktiver RP ID, Recovery
und Unternehmens-SSO bleiben Betreiberentscheidungen. Kein eigenes Verfahren
zur frischen Authentifizierung. Die reale Abnahme hält beobachtetes Verhalten
fest. Keine Produktionsabnahme. Die gepinnte
[RequiredActionProvider-Implementierung](https://github.com/keycloak/keycloak/blob/26.7.3/server-spi-private/src/main/java/org/keycloak/authentication/RequiredActionProvider.java)
liest `max_auth_age` aus der Required-Action-Konfiguration. Ohne Override gilt
laut [Constants.java](https://github.com/keycloak/keycloak/blob/26.7.3/server-spi-private/src/main/java/org/keycloak/models/Constants.java)
300 Sekunden; `0` fordert stets Reauthentifizierung. Im bestandenen T14-Teillauf
akzeptierte Keycloak die unmittelbar zuvor authentifizierte Sitzung ohne
erneuten Passwortdialog. Es wird deshalb keine frische Authentifizierung pro
Klick zugesagt; Betreiber können die Keycloak-Policy gesondert festlegen.
