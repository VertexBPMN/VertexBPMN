# Keycloak-Passkeys für VertexBPMN: Delta-Umsetzungsplan für Codex

Stand: 2026-10-11. Basis: `master`; Commit vor Arbeitsbeginn mit `git rev-parse --short HEAD` eintragen.
Status: **Entwurf. Nichts davon ist implementiert oder ausgeführt.** Der Plan wurde aus gelesenem Code und Dokumentation abgeleitet, es wurde keine Anwendung gestartet und kein Test ausgeführt.
Vorgeschlagener Branch: `codex/keycloak-passkeys` (Namensmuster wie `codex/keycloak-k05-security-acceptance`).
Pfade sind repository-relativ.

---

## 0. Pflichtlektüre vor dem ersten Edit

1. `AGENTS.md`, besonders „Working rules“ und „Codex repository guidance“
2. `docs/reviews/2026-09-09_Keycloak-OIDC_Self-Hosting_Umsetzungsplan.md` (Claim-Vertrag, Abnahmelogik) und `docs/reviews/2026-09-10_Keycloak-OIDC_K05_Zwischenstand.md`
3. `docs/runbooks/studio-authentication.md` und `deploy/keycloak/README.md`
4. `src/VertexBPMN.Studio/Program.cs`: OIDC-Registrierung (ca. Zeilen 120–235) und Auth-Endpunkte (ca. Zeilen 320–390)
5. `src/VertexBPMN.ServiceDefaults/Security/VertexOidcClaims.cs`
6. `deploy/keycloak/vertexbpmn-realm.json`, `scripts/keycloak-oidc-test.ps1`, `scripts/keycloak-oidc-e2e.ps1`
7. `tests/VertexBPMN.Tests/Unit/Security/KeycloakRealmTemplateTests.cs`, `tests/VertexBPMN.Studio.UiTests/KeycloakOidcSecurityAcceptanceTests.cs`

Zeilennummern sind Orientierung. Gültig ist der Code, den du tatsächlich vorfindest.

## 1. Ziel und Grenzen

**Ziel:** Benutzer können sich am VertexBPMN Studio mit einem Passkey anmelden und Passkeys verwalten. Registrierung, Speicherung und Prüfung übernimmt Keycloak. Studio und API bleiben reine OIDC-/JWT-Clients.

**Nicht im Umfang. Diese Punkte dürfen nicht implementiert werden:**

- Keine lokalen ASP.NET-Core-Identity-Passkeys, kein `PassKeyController`, keine `/api/passkey`-Endpunkte, keine `passkey.js`, kein `AddIdentityApiEndpoints`, kein neues Identity-Schema oder Identity-NuGet-Paket.
- Kein eigener Token-Issuer, kein `ITokenService`, keine eigene WebAuthn-Kryptografie.
- Keine Änderung am Claim-Vertrag (`roles`, `tenant_id`, `sub`, `VertexOidcClaims`). Insbesondere keinen Claim `vertex_roles` und keine Rolle `modeler` einführen.
- `SaveTokens` bleibt `true`. Token-Store (`OidcSessionTokenStore`) und `OidcCookieRefreshEvents` bleiben unverändert.
- Kein Passkey-Zwang und keine Produktions-Realm-Konfiguration. Produktiv-`rpId`, Recovery-Prozess und Keycloak-Upgrade sind separate Entscheidungen des Betreibers.

**Erhalten bleiben:** Alle bestehenden OIDC-Profile, `UiTestAuthenticationHandler`, lokale Entwicklung und WSLC. Keycloak bleibt optional.

## 2. Ist-Stand (im Repo belegt)

| Bereich | Befund | Konsequenz |
|---|---|---|
| Realm | `deploy/keycloak/vertexbpmn-realm.json` mit Clients `vertexbpmn-studio` (confidential, PKCE S256, kein Direct Grant), `vertexbpmn-api`, `vertexbpmn-contract-reviewer`; Keycloak `26.7.3` | Basis steht |
| Passkeys im Realm | Kein WebAuthn-Eintrag, `requiredActions` leer | **Fehlt (P1)** |
| Realm-Import | Nur beim ersten Start über `--import-realm`; bestehende Instanzen übernehmen JSON-Änderungen nicht | Zusätzlich idempotenter `kcadm`-Schritt nötig (P1) |
| Testbenutzer | `Ensure-TestUser` in `scripts/keycloak-oidc-test.ps1`; MFA-Benutzer erhält `CONFIGURE_TOTP` | Muster für Passkey-Benutzer (P2) |
| Studio-OIDC | Code + PKCE, `SaveTokens = true`, Token-Store, Refresh, `/authentication/login` mit `IsLocalReturnUrl` | Wiederverwenden |
| `kc_action`, Account-Link | Kommt im Code nirgends vor | **Fehlt (P3)** |
| SSO-Seite | `src/VertexBPMN.Studio/Components/Pages/SSO.razor` ist eine statische Liste („Keycloak Integration“) | Möglicher Platz für Links (P3) |
| Tests | `KeycloakOidcSecurityAcceptanceTests` (T06–T12, Trait `KeycloakOidcSecurityAcceptance`), Hilfsmethoden `LoginAsync`, `CreateKeycloakAdminClientAsync`, `RequiredEnvironment`; Skript-Schalter `-SecurityAcceptance`, `-TestMethod` | Erweitern, nicht neu bauen |

## 3. Leitplanken

- `AGENTS.md` gilt vollständig. Keine Schwächung von Authentifizierung, Autorisierung, Token-Validierung oder Mandantengrenzen. Keine Secrets, Tokens oder Passwörter in Logs, Testausgaben oder Dateien.
- Keine neuen NuGet-Pakete. Das Paket `Microsoft.AspNetCore.Authentication.OpenIdConnect` ist bereits eingebunden.
- Keine Datenbankmigration. Kein Commit, Push, Deployment oder Anwenden von Migrationen ohne ausdrückliche Freigabe.
- **Nicht raten:** Keycloak-Attributnamen, UI-Selektoren und Parameter werden gegen die gepinnte Keycloak-Version verifiziert (siehe Abschnitt 8).
- Die Passkey-Abnahmetests sind lokal und opt-in wie die bestehenden Keycloak-Tests. Sie gehören nicht in `.github/workflows/ci.yml`.
- Die lokale Keycloak-Abnahme benötigt `wslc.exe` (Windows). Auf einer Ubuntu-VM ist sie nicht ausführbar. In diesem Fall gilt die Abnahme als **nicht durchgeführt**, nicht als bestanden.
- Studio-Texte sind Englisch (wie bestehende Seiten). `data-testid`-Attribute folgen dem vorhandenen Stil.

## 4. Arbeitspakete

Reihenfolge: P0, P1, P2, P3, P4, P5. P3 kann nach P1 parallel zu P2 beginnen.

### P0. Baseline und Verifikation (kein Produktivcode)

1. `git status`, aktuellen Branch und offene Änderungen sichern. Fremde Änderungen nicht anfassen.
2. Build und schnelle Tests laut `AGENTS.md` und `.github/workflows/ci.yml` (Schritt „Test (CI-safe suite)“). Ergebnis als Ausgangslage festhalten. Ist die Baseline rot, stoppen und melden.
3. Prüfen, ob `wslc.exe` verfügbar ist, und das im Bericht vermerken.
4. **Realm-Attributnamen ermitteln.** Mit verfügbarer lokaler Instanz (`scripts/keycloak-oidc-test.ps1 -Action Start`): in der Admin-Konsole unter *Authentication, Policies, WebAuthn Passwordless Policy* die Zielwerte aus P1 setzen, dann Realm per `kcadm get realms/vertexbpmn` exportieren. Die exakten JSON-Feldnamen von dort übernehmen. Ohne laufende Instanz: Namen aus der Admin-REST-Referenz der gepinnten Version ableiten und im Plan als „unverifiziert bis Import“ kennzeichnen.
5. Prüfen, ob für die gepinnte Version `26.7.3` ein neuerer Patch (z. B. `26.7.4`) existiert. Nur berichten, nicht ändern.

Abnahme: Baseline-Ergebnis, Verfügbarkeit von `wslc`, Liste der verifizierten Attributnamen.

### P1. Realm-Template und Provisionierung

Dateien: `deploy/keycloak/vertexbpmn-realm.json`, `scripts/keycloak-oidc-test.ps1`, `deploy/compose/keycloak-bootstrap.sh` (nur falls dort Realm-Einstellungen gesetzt werden), `deploy/keycloak/README.md`, `tests/VertexBPMN.Tests/Unit/Security/KeycloakRealmTemplateTests.cs`.

Zielwerte der *WebAuthn Passwordless Policy*:

- Passkeys aktiviert
- User Verification: `required`
- Discoverable credential: `required` (in 26.7 löst diese Einstellung die frühere Ja/Nein-Resident-Key-Einstellung ab; Feldname aus P0 übernehmen)
- Attestation: `none`
- Authenticator Attachment: nicht einschränken (Hardware-Keys bleiben erlaubt)
- `rpId`: für das lokale Profil `localhost`; **keine** Beispieldomain wie `auth.example.com` im Template. Produktiv-`rpId` ist Betreiberwert und kommt in die README.

Schritte:

1. Policy-Felder in `vertexbpmn-realm.json` ergänzen (Namen aus P0).
2. Die Required Action `webauthn-register-passwordless` im Realm bekannt und aktiviert machen. **Nicht** als Default Action für alle Benutzer setzen (Rollout nur über gezielte Zuweisung).
3. In `scripts/keycloak-oidc-test.ps1` einen idempotenten Schritt ergänzen, der dieselben Policy-Werte per `kcadm update realms/...` auf eine bereits importierte Instanz anwendet. Muster: vorhandene `Ensure-*`-Funktionen. Mehrfachaufruf darf nichts verändern.
4. `KeycloakRealmTemplateTests` erweitern: Policy-Werte, Required-Action-Eintrag, und der bestehende Test `RealmTemplate_IsSecretFreeAndContainsNoUsers` bleibt grün (keine Secrets, keine Benutzer).
5. README: Passkey-Abschnitt (Zielwerte, `rpId` als Betreiberwert, Hinweis „aktivieren ist nicht erzwingen“).

Abnahme: Realm-Template-Tests grün; frischer Import und wiederholter `Start` ergeben dieselbe Policy (per `kcadm get` belegt, sofern `wslc` verfügbar).

### P2. Testbenutzer für Passkeys

Datei: `scripts/keycloak-oidc-test.ps1` (nach dem Muster von `-RequireMfaEnrollment`).

1. Neuen Benutzer `vertexbpmn-passkey-user` (Tenant `tenant-a`, Rolle `ReadOnly`) anlegen. Optionaler Schalter `-RequirePasskeyEnrollment` setzt die Required Action `webauthn-register-passwordless`.
2. Bei jedem `Start` Credentials und Required Actions zurücksetzen (idempotent, wie beim MFA-Benutzer), damit Läufe isoliert sind.
3. Benutzername und Passwort an die Tests so durchreichen, wie es beim MFA-Benutzer geschieht (Mechanismus im Skript nachlesen, keinen neuen Weg erfinden).
4. Passwörter nur aus den bestehenden Prozess-Umgebungsvariablen, nie in Dateien.

Abnahme: Mehrfaches `Start` erzeugt genau einen Benutzer ohne Altlasten.

### P3. Studio: Passkey-Verwaltung

Dateien: `src/VertexBPMN.Studio/Program.cs`, ggf. neue interne Hilfsklasse unter `src/VertexBPMN.Studio/`, `src/VertexBPMN.Studio/Components/Pages/SSO.razor`.

**P3a. Account-Console-Link (MVP, zuerst umsetzen)**

- Neue optionale Einstellung `StudioAuthentication:AccountManagementUrl`. Ist sie leer, wird nichts angezeigt (Provider-Neutralität bleibt). Beim Start validieren: absolute URL, `https`, nur im `OidcTest`-Profil zusätzlich `http` auf Loopback (analog zur vorhandenen Authority-Prüfung). Ungültige Werte führen zu `InvalidOperationException` mit eindeutiger Meldung.
- Anzeige auf der SSO-Seite (oder an einer Stelle, die du im aktuellen Layout als passender erkennst; Entscheidung im Bericht begründen): Link „Manage sign-in methods“ mit `target="_blank"` und `rel="noopener noreferrer"`, `data-testid="sso-manage-sign-in"`.

**P3b. Application Initiated Action (optional, nach P3a und P4-Test)**

Zweck: „Add a passkey“ führt über den normalen OIDC-Flow zur Keycloak-Registrierung und zurück.

Rahmen (Skizze, an den vorhandenen Code anpassen):

```csharp
// Konstante, niemals aus Benutzereingaben:
const string PasskeyAction = "webauthn-register-passwordless";
const string ActionItemKey = "vertex:keycloak-action";

// Nur registrieren, wenn echtes OIDC aktiv ist (nicht isUiTest/isLocalDevelopment):
app.MapGet("/authentication/passkey/register", (string? returnUrl) =>
{
    var target = IsLocalReturnUrl(returnUrl) ? returnUrl! : "/sso";
    var properties = new AuthenticationProperties { RedirectUri = target };
    properties.Items[ActionItemKey] = PasskeyAction;
    return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
}).RequireAuthorization();

// In den bestehenden AddOpenIdConnect-Events ergänzen, ohne OnTokenValidated/OnTicketReceived zu ändern:
options.Events.OnRedirectToIdentityProvider = context =>
{
    if (context.Properties.Items.TryGetValue(ActionItemKey, out var action) && action == PasskeyAction)
    {
        context.ProtocolMessage.SetParameter("kc_action", PasskeyAction);
    }
    return Task.CompletedTask;
};
```

Regeln:

- Keine manuell zusammengebaute Authorization-URL (State, Nonce, PKCE und PAR müssen vom Handler kommen).
- Der Parameterwert ist eine feste Konstante, kein durchgereichter Request-Parameter.
- Nach der Rückkehr zeigt das Studio höchstens eine neutrale Meldung. **Die Rückkehr gilt nicht als Beweis**, dass ein Passkey angelegt wurde. Verifiziert wird nur serverseitig in Tests über Keycloak.
- Ob ein Rückgabeparameter (z. B. `kc_action_status`) existiert und wie er heißt, gegen die Doku der gepinnten Version prüfen, bevor er ausgewertet wird.
- ASP.NET Core nutzt PAR, wenn der IdP es per Discovery anbietet. Der Test in P4 muss belegen, dass `kc_action` im PAR-Pfad ankommt.
- Prüfen, ob Keycloak für diese Aktion eine frische Authentifizierung erzwingt. Falls nicht, im Bericht als Restrisiko festhalten, nicht stillschweigend ein eigenes Verfahren erfinden.

Unit-/Integrationstests: neben den vorhandenen Studio-Auth-Tests in `tests/VertexBPMN.Tests` (Fundort per Suche nach `authentication/login` ermitteln). Abdecken: Konfigurationsvalidierung von `AccountManagementUrl`, Endpunkt nur mit aktivem OIDC erreichbar, anonymer Zugriff wird zur Anmeldung geleitet, `returnUrl`-Normalisierung (`//attacker.example/` landet auf der eigenen Origin).

Abnahme: Link erscheint nur bei gültiger Konfiguration; Endpunkt existiert nur im echten OIDC-Betrieb; bestehende Studio-Auth-Tests unverändert grün.

### P4. Lokale Browser-Abnahme mit virtuellem Authenticator

Dateien: `tests/VertexBPMN.Studio.UiTests/KeycloakOidcSecurityAcceptanceTests.cs` (nur `partial` ergänzen), neue Datei `tests/VertexBPMN.Studio.UiTests/KeycloakOidcSecurityAcceptanceTests.Passkeys.cs`, `scripts/keycloak-oidc-e2e.ps1`.

Vorgehen:

1. Klasse `KeycloakOidcSecurityAcceptanceTests` von `public sealed class` auf `public sealed partial class` ändern (eine Zeile). So werden vorhandene `private static` Hilfsmethoden (`LoginAsync`, `CreateKeycloakAdminClientAsync`, `RequiredEnvironment`, `AssertTenantContextAsync`) ohne Refactoring wiederverwendet.
2. Neue Tests mit `[Trait("Category", "KeycloakOidcSecurityAcceptance")]`, Benennung im Muster `T13_…`.
3. Virtueller Authenticator über das Chrome DevTools Protocol (nur Chromium, wie bereits genutzt). Skizze, Aufruf gegen die installierte Playwright-Version verifizieren:

```csharp
var cdp = await context.NewCDPSessionAsync(page);
await cdp.SendAsync("WebAuthn.enable");
await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>
{
    ["options"] = new Dictionary<string, object>
    {
        ["protocol"] = "ctap2",
        ["transport"] = "internal",
        ["hasResidentKey"] = true,
        ["hasUserVerification"] = true,
        ["isUserVerified"] = true,
        ["automaticPresenceSimulation"] = true
    }
});
```

4. UI-Selektoren der Keycloak-Seiten (Registrierungsseite, Schaltfläche „Sign in with Passkey“) **zur Laufzeit gegen die echte Keycloak-26.7-Seite ermitteln** und bevorzugt über Rolle/Text oder stabile IDs ansprechen. Keine Selektoren raten.

Tests:

- **T13 Registrierung und passwortloser Login.** Benutzer `vertexbpmn-passkey-user` meldet sich einmal mit Passwort an, registriert über die Required Action einen Passkey, meldet sich ab und danach ohne Passworteingabe per Passkey an. Erwartung: Dashboard sichtbar, `AssertTenantContextAsync` bestätigt `tenant-a`, ein API-gestützter Seitenaufruf (z. B. Process Definitions) lädt ohne Fehler. Das belegt, dass Rollen und `tenant_id` auch beim Passkey-Login ankommen.
- **T14 Application Initiated Action (nur wenn P3b umgesetzt).** Eingeloggter Benutzer startet `/authentication/passkey/register`, registriert einen Passkey und landet wieder im Studio. Beweis über die Keycloak-Admin-API: Benutzer besitzt ein Credential vom Typ `webauthn-passwordless`. Zusätzlich Abbruchpfad: Abbruch erzeugt kein Credential, die Studio-Sitzung bleibt intakt. Der Test muss auch den PAR-Pfad abdecken.
- **T15 Mandantenschutz.** Benutzer ohne `tenant_id` (`vertexbpmn-no-tenant`) erhält auch mit Passkey keine Studio-Sitzung (fail-closed durch `VertexOidcClaims.TryNormalize`). Nur umsetzen, wenn die Provisionierung dafür vertretbar einfach ist; sonst als offener Punkt melden.

Nicht ohne Entscheidung des Eigentümers testen (siehe Abschnitt 6, E1): Verhalten des MFA-Benutzers beim Passkey-Login. Das beobachtete Verhalten wird berichtet, aber nicht als gewünscht festgeschrieben.

Skript: in `scripts/keycloak-oidc-e2e.ps1` einen Schalter `-PasskeyAcceptance` nach dem Muster von `-SecurityAcceptance` ergänzen (Klassenfilter, Benutzer-Provisionierung aus P2). `-TestMethod` muss weiter funktionieren. Bestehendes Verhalten ohne neuen Schalter bleibt unverändert.

Abnahme: T13 (und T14, falls P3b) bestehen lokal gegen echtes Keycloak. Die bestehenden Läufe (`-SecurityAcceptance`) bleiben grün. Ohne `wslc`: als „nicht ausgeführt“ berichten.

### P5. Dokumentation und Abschlussbericht

1. `docs/runbooks/studio-authentication.md`: Abschnitt „Passkeys über Keycloak“ (Zuständigkeiten, Konfigurationsschlüssel, `rpId` als Betreiberwert, schrittweiser Rollout, Recovery bei Verlust aller Passkeys, Hinweis auf Unternehmens-SSO, siehe Abschnitt 5).
2. `deploy/keycloak/README.md` aktualisieren (aus P1).
3. `CHANGELOG.md`: Eintrag im vorhandenen Format, falls dort ein Abschnitt für Unveröffentlichtes existiert.
4. Neue Planstatus-Datei oder Statuszeile in diesem Dokument: Dateien, ausgeführte Befehle mit tatsächlichen Ergebnissen, offene externe Abnahmen.
5. Abschlussreview der geänderten C#-Dateien mit dem Skill aus `.agents/skills/dotnet-clean-code-review/SKILL.md` (nur geänderte Dateien). Fehlt der Skill-Mechanismus, die SKILL.md direkt lesen und den Fallback berichten.
6. Prüfen mit Format-Check-only, Build und den relevanten Tests laut `AGENTS.md`.

## 5. Sicherheitsaspekte, die dokumentiert und nicht wegimplementiert werden

- **Unternehmens-SSO:** Ein lokaler Keycloak-Passkey kann einen zweiten Loginweg neben einem externen Unternehmens-IdP öffnen. Offboarding und Richtlinien des Unternehmens müssen diesen Weg abdecken. Nur dokumentieren, nicht lösen.
- **Bestehende Benutzer:** Zuordnung über Issuer plus `sub`, nicht über die E-Mail-Adresse. Passkeys wandern nicht automatisch mit.
- **Recovery:** Verlust aller Passkeys braucht einen definierten Prozess (Betreiberentscheidung).
- **Keycloak-Default-Flow:** Seit 26.4 enthält der Standard-Browser-Flow die Bedingung „Conditional – credential“, die 2FA nach einem Passkey-Login überspringt. Das betrifft die TOTP-Abnahme (T07) und die MFA-Policy, siehe E1.

## 6. Entscheidungspunkte für den Eigentümer (nicht selbst entscheiden)

| ID | Frage | Vorgabe bis zur Entscheidung |
|---|---|---|
| E1 | Soll ein Passkey-Login TOTP ersetzen (Passkey als Multi-Faktor) oder TOTP zusätzlich verlangt werden? | Verhalten beobachten und berichten, nichts festschreiben |
| E2 | Passkey zusätzlich zum Passwort oder Passkey-only? | Zusätzlich, nur für Pilotbenutzer |
| E3 | Attestation weiter `none` oder strengere Hardware-Vorgaben? | `none` |
| E4 | Wo im Studio erscheinen die Links (SSO-Seite oder Benutzermenü)? | SSO-Seite, begründeter Vorschlag im Bericht |

## 7. Gesamtabnahme

- [ ] Baseline aus P0 dokumentiert; Build und schnelle Tests nach den Änderungen grün.
- [ ] Realm-Template-Tests enthalten Passkey-Policy; Secret-Freiheit bleibt erhalten.
- [ ] Idempotenter Realm-Update-Schritt für bestehende Instanzen vorhanden.
- [ ] Claim-Vertrag unverändert (`VertexOidcClaims` und seine Tests ohne Änderung).
- [ ] P3a umgesetzt und getestet; P3b nur mit Test T14 inklusive PAR-Pfad.
- [ ] T13 (und ggf. T14, T15) lokal gegen echtes Keycloak bestanden, oder klar als nicht ausgeführt gemeldet.
- [ ] Bestehende Keycloak-Läufe unverändert grün.
- [ ] Keine neuen Pakete, keine Migrationen, keine Secrets in Dateien oder Logs.
- [ ] Dokumentation aktualisiert; offene Entscheidungen E1–E4 benannt.

## 8. Annahmen, die du selbst verifizieren musst

Diese Punkte stammen aus Dokumentation oder Erinnerung und wurden nicht gegen die gepinnte Version ausgeführt:

| Annahme | Quelle | Prüfen durch |
|---|---|---|
| Passkeys sind ab Keycloak 26.4 unterstützt und werden in der *WebAuthn Passwordless Policy* aktiviert | Keycloak-26.4-Release-Notes | P0, Admin-Konsole |
| Required-Action-Alias `webauthn-register-passwordless`, Credential-Typ `webauthn-passwordless` | Keycloak-Dokumentation und Tutorials | P0/P4, Admin-API |
| Exakte JSON-Feldnamen der Policy (inkl. „Discoverable credential“) | nicht verifiziert | P0, `kcadm get realms/…` |
| `kc_action=webauthn-register-passwordless` löst die Registrierung im normalen OIDC-Flow aus | Keycloak-Doku zu Application Initiated Actions | P4/T14 |
| Name und Existenz eines Rückgabeparameters der Aktion | nicht verifiziert | Keycloak-Doku der gepinnten Version, T14 |
| PAR überträgt `kc_action` korrekt | nicht verifiziert (K05-T10 prüft PAR grundsätzlich) | T14 |
| `IBrowserContext.NewCDPSessionAsync` und `SendAsync`-Signatur der eingesetzten Playwright-Version | nicht ausgeführt | Kompilieren, Kurztest |
| Selektoren der Keycloak-Anmelde- und Registrierungsseite | nicht verifiziert | Laufzeitinspektion in P4 |

## 9. Berichtsformat am Ende

Auf Deutsch: geänderte Dateien, tatsächlich ausgeführte Befehle mit Ergebnis, was **implementiert**, **lokal getestet** und **extern abgenommen** ist (getrennt), offene Punkte, nächster konkreter Schritt. Nicht ausgeführte Abnahmen ausdrücklich als nicht ausgeführt kennzeichnen.
