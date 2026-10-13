# Studio Authentication

VertexBPMN Studio uses an external OpenID Connect provider. It does not provide
a local login, a development identity, or a default role.

Configure these values through deployment configuration or environment variables
(`StudioAuthentication__Authority`, `StudioAuthentication__ClientId`,
`StudioAuthentication__ClientSecret`, and optionally
`StudioAuthentication__ApiScope`):

- `Authority`: the issuer/authority URL of the OIDC provider.
- `ClientId`: the confidential client registered for Studio.
- `ClientSecret`: the client secret, stored outside the repository. Public OIDC
  clients may leave this empty when the provider supports PKCE without a secret.
- `ApiScope`: the provider scope that issues an access token accepted by the
  VertexBPMN API. Leave it empty only when the provider's default token already
  has the API audience.

Register the callback URL `{StudioBaseUrl}/signin-oidc` and the post-logout
redirect URL `{StudioBaseUrl}/` at the identity provider. The provider must
issue the `tenant_id` claim and the role claims expected by the API. Studio
stores the OIDC tokens in its encrypted cookie session and forwards the access
token to the API as a bearer token.

Studio fails during startup when `Authority` or `ClientId` is missing. This is
intentional: an unconfigured deployment must not silently become anonymous or
create a synthetic administrator.

## Passwortverwaltung

VertexBPMN speichert und prüft keine Benutzerpasswörter. Der API-Vertrag `/api/identity/password-management` weist den externen OIDC-Provider als zuständige Stelle aus; optional kann `Identity:PasswordManagementUrl` auf dessen Passwort-/Account-Seite zeigen. Der frühere Validate-User-Endpunkt akzeptiert keinen Passwortwert mehr.
Für Connector-Credentials muss der Data-Protection-Key-Ring in Produktion persistent gespeichert bzw. als Container-Volume eingebunden werden; ohne denselben Key-Ring können geschützte Werte nach einem Neustart nicht entschlüsselt werden.

## Passkeys über Keycloak

Keycloak registriert, speichert und prüft Passkeys. Studio und API bleiben
OIDC-/JWT-Clients; Claim-Vertrag (`roles`, `tenant_id`, `sub`), Token-Store,
Refresh und `SaveTokens=true` bleiben unverändert. Es gibt keine lokale
WebAuthn-/Identity-Implementierung im Studio.

Mit `StudioAuthentication:AccountManagementUrl` (Umgebungsvariable
`StudioAuthentication__AccountManagementUrl`) zeigt die SSO-Seite den Link
**Manage sign-in methods**. Er öffnet die konfigurierte Account Console in einem
neuen Tab mit `noopener noreferrer`. Ohne Einstellung bleibt der Link verborgen.
Studio validiert den Wert beim Start: absolute HTTPS-URL ohne eingebettete
Benutzerdaten; ausschließlich im `OidcTest`-Profil ist HTTP auf Loopback erlaubt.
Ein ungültiger Wert führt zu einer eindeutigen `InvalidOperationException`.
Die URL wird nicht aus Requests oder einer Benutzereingabe übernommen.

Rollout:

1. Betreiber legt den produktiven RP ID passend zum öffentlichen Keycloak-Host,
   HTTPS-Origin, Recovery-Prozess und Unternehmensrichtlinien fest. Das lokale
   Realm-Template verwendet ausschließlich `localhost`.
2. WebAuthn Passwordless Policy aktivieren: User Verification `required`,
   Discoverable credential `required`, Attestation `none`, Authenticator
   Attachment nicht einschränken. Kein automatischer Registrierungszwang.
3. Die aktivierte Required Action `webauthn-register-passwordless` zunächst
   nur Pilotbenutzern zuweisen; Passwortanmeldung bleibt verfügbar.
4. Benutzer meldet sich an, registriert einen Passkey bei Keycloak und prüft
   eine neue Anmeldung. Weitere Passkeys werden in der Account Console verwaltet.
5. Erst nach lokaler Abnahme und Betreiberfreigabe den Pilotkreis erweitern.

Bei Verlust aller Passkeys benötigt der Betreiber einen definierten Prozess
mit verifizierter Identität, Widerruf verlorener Credentials und kontrollierter
erneuter Registrierung. Passwort und unabhängiger Ersatz-Authenticator können
im Pilot helfen; sie ersetzen kein betriebliches Recovery-Verfahren. Die
Anwendung stellt weder Recovery-Codes noch einen eigenen Identitätsnachweis aus.

Bei Unternehmens-SSO kann ein lokaler Keycloak-Passkey einen zusätzlichen
Anmeldeweg neben dem externen Unternehmens-IdP eröffnen. Offboarding, Kontosperren
und Richtlinien müssen diesen Weg abdecken. Bei Providerwechseln erfolgt die
Zuordnung über Issuer plus `sub`, niemals allein über eine E-Mail-Adresse;
Passkeys werden nicht automatisch übertragen.

Ob Passkeys TOTP ersetzen oder TOTP zusätzlich verlangt werden soll, bleibt
eine Betreiberentscheidung (E1). Der Keycloak-Standardflow kann nach einer
Passkeyanmeldung weitere 2FA überspringen. Diese Implementierung verändert den
Browserflow nicht und definiert dieses Verhalten nicht als gewünschte MFA-Policy.
Für Keycloak kann zusätzlich `StudioAuthentication:PasskeyRegistrationEnabled=true`
(Umgebungsvariable `StudioAuthentication__PasskeyRegistrationEnabled`) gesetzt
werden. Standard ist `false`; andere Provider behalten ihre konfigurierte Account
Console. Nur mit echtem OIDC und aktivem Schalter zeigt die SSO-Seite **Add a
passkey** und registriert `/authentication/passkey/register`. UI-Testauth und
lokale Entwicklungsidentitäten erhalten diesen Endpunkt nicht.

Die autorisierte Route startet einen normalen OIDC-Challenge mit der festen
Aktion `webauthn-register-passwordless`. State, Nonce, PKCE und PAR bleiben beim
OIDC-Handler. Requestparameter können den Aktionsnamen nicht ersetzen.
`returnUrl` akzeptiert lokale Pfade; fremde oder schemerelative Ziele fallen auf
`/sso` zurück. Die Rückkehr zeigt keinen Registrierungs-Erfolgshinweis; ein
Credential wird serverseitig bei Keycloak verifiziert. Keycloak entscheidet
über die Aktualität der Authentifizierung; es gibt kein eigenes Reauth-Protokoll.

Lokale Opt-in-Abnahme und Provisionierung:
[Keycloak test realm](../../deploy/keycloak/README.md).
Ausgeführte Ergebnisse und verbleibende Abnahmen werden im
[Umsetzungsbericht](../reviews/2026-10-13_Keycloak-Passkeys_Umsetzung.md) geführt.
Ergänzender Flow und Negativfall ohne Tenant:
[AIA-/Tenant-Abnahme](../reviews/2026-10-13_Keycloak-Passkeys_AIA-und-Tenant-Abnahme.md).
