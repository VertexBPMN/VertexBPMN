# Keycloak/OIDC – K00 Auth-Inventur und Baseline

Stand: 2026-09-09. Basis: `591655e6b75b17239b587eb5e50b4d97085d2ecd`.
Branch: `codex/keycloak-oidc-self-hosting`.
Status: **K00 abgeschlossen; noch keine Keycloak-Implementierung oder echte IdP-Abnahme.**

## 1. Ergebnis

VertexBPMN besitzt eine providerneutrale OIDC/JWT-Grundlage, ist aber noch nicht als Keycloak-Profil qualifiziert. Der bestehende Sicherheitsnachweis verwendet Testauthentifizierung beziehungsweise synthetische Claims. Er beweist Rollen- und Tenantregeln nach erfolgreicher Claim-Erzeugung, aber weder Keycloak-Login noch Keycloak-Claim-Mapping, Tokenrefresh, Logout oder IdP-Ausfall.

Die beiden wichtigsten technischen Lücken vor der echten Integration sind:

1. Rollen werden in API und Studio als `ClaimTypes.Role` ausgewertet. `MapInboundClaims=false` und das bloße Anfordern eines `roles`-Scopes normalisieren Keycloak-Rollen nicht automatisch in diesen Claimtyp.
2. Studio leitet das Access Token pro API-Request über `IHttpContextAccessor.GetTokenAsync("access_token")` weiter. Für einen langlebigen Interactive-Server-Circuit ist noch nicht belegt, dass der passende Requestkontext und ein aktuelles Token nach Ablauf/Refresh zuverlässig verfügbar sind.

K01 muss deshalb zuerst den Claim- und Sessionvertrag festlegen. Keycloak-Konfiguration ohne diese Anwendungskorrekturen wäre nur ein teilweise funktionierender Login.

## 2. Tatsächlicher Authentifizierungspfad

| Pfad | Implementierung | Verifizierter Stand | Lücke für Keycloak |
|---|---|---|---|
| Studio-Start | `src/VertexBPMN.Studio/Program.cs` | Außer UI-Test/lokaler Entwicklung sind Authority und ClientId Pflicht. | Kein benanntes echtes OIDC-Testprofil; Development schaltet aktuell vollständig auf `UiTestAuthenticationHandler`. |
| Benutzerlogin | `Program.cs`, `/authentication/login` | Challenge über OIDC; Return-URL muss mit `/` beginnen. Code Flow und PKCE sind konfiguriert. | Exakte Callback-/Proxy-/HTTPS-Topologie und Keycloak-Client noch nicht abgenommen. Die einfache Präfixprüfung der Return-URL ist separat gegen schemerelative Werte zu härten/testen. |
| Studio-Sitzung | Cookie + OIDC in `Program.cs` | Tokens werden im Auth-Ticket gespeichert; UserInfo ist aktiv; Inbound-Mapping deaktiviert. | Cookie-/Ticket-Lifetime, Refreshstrategie, Data-Protection bei mehreren Replikaten und Rollenmapping nicht definiert. |
| Logout | `Program.cs`, `/authentication/logout` | Lokales Cookie und OIDC-Scheme werden abgemeldet. | RP-initiated Logout, `id_token_hint`, Redirect und offener Blazor-Circuit nicht gegen echten IdP geprüft. Endpoint ist GET und damit als zustandsändernder Vorgang gegen CSRF/unerwünschte Navigation zu überprüfen. |
| Studio → API | `src/VertexBPMN.Studio/Services/StudioApiAuthorizationHandler.cs` | Access Token wird aus aktuellem `HttpContext` gelesen; sonst optional Development-API-Key. | Circuit-/Refreshfähigkeit und Sitzungsisolation nicht belegt. Fallback darf im echten OIDC-Profil niemals greifen. |
| API-Authentifizierung | `src/VertexBPMN.Api/Security/SecurityConfiguration.cs` | Authority oder lokaler SecretKey; Audience/Lifetime/Issuer werden konfiguriert geprüft. HTTPS-Metadaten außerhalb Development. | Keine explizite Keycloak-Claimtransformation oder getestete JWKS-/Keyrotation. `RoleClaimType`/Claims-Mapping ist nicht festgelegt. |
| API-Autorisierung | dieselbe Datei sowie Controller/Hubs/gRPC | Policies `AdminOnly`, `ProcessManager`, `ReadOnly`, `TenantReadOnly`; letzteres verlangt Rolle und bei Nicht-Admin einen nichtleeren `tenant_id`. | Positive/negative Regeln nutzen bereits normalisierte Testclaims; echte Tokenstruktur fehlt. Einige Controller besitzen eigene Tenantlogik und bleiben Teil der separaten vollständigen Matrix. |
| Studio-Autorisierung | `AuthorizeView Roles="Admin"`, `[Authorize(Roles="Admin")]` | Credentials, Extensions, Feature Flags und OAuth2-Callback erwarten eine Rollenidentität. | Muss dieselbe normalisierte Rollenquelle wie API verwenden; bloßes Anzeigen/Verbergen ersetzt API-Prüfung nicht. |
| Entwicklungsauth | Studio `UiTestAuthenticationHandler`; API-Key-Handler | Studio Development mit `LocalDevelopmentEnabled=true` verwendet Testidentität; API Development kann API-Key mit konfigurierten Rollen nutzen. | Für realen lokalen OIDC-Lauf müssen beide Pfade explizit deaktiviert sein; kein automatischer Sicherheitsfallback. |
| SignalR/gRPC/MCP | API-Hubs und MCP-/gRPC-Services | Autorisierungspolicies und Tenantchecks vorhanden; aktuelle Regressionen grün. | Echte Bearer-/WebSocket-/gRPC-Token gegen Keycloak noch nicht geprüft. |

## 3. Claims und Identitätsreferenzen

### Aktuell konsumierte Claims

- Rollen: `ClaimTypes.Role`; Werte `Admin`, `ProcessManager`, `ReadOnly`.
- Tenant: Stringclaim `tenant_id`.
- Benutzer: uneinheitlicher Fallback aus `ClaimTypes.NameIdentifier`, `sub`, `ClaimTypes.Name` oder `Identity.Name`.
- Audit: bevorzugt `NameIdentifier`, danach `Name`.
- User Tasks und Benachrichtigungen verwenden String-IDs/Assignees. Studio bevorzugt den Anzeigenamen und danach `NameIdentifier`; SignalR bevorzugt `NameIdentifier`, danach `sub`/Name.

### Konsequenzen

- K01 muss eine kanonische Benutzer-ID definieren, vorzugsweise issuergebundenes `sub`. Anzeigenamen sind veränderbar und nicht als Autorisierungsidentität geeignet.
- Eine globale Umschreibung bestehender Task-/Benutzer-IDs ist nicht Teil von K00. Vor Änderung sind Datenmigration und Abwärtskompatibilität festzulegen.
- Keycloak-Rollen können in Realm- oder Clientrollen liegen. Nur freigegebene Vertex-Rollen dürfen normalisiert werden; Keycloak-Adminrollen dürfen keine Vertex-Rechte erzeugen.
- `tenant_id` muss ein einzelner, nichtleerer, administrativ gepflegter Claim sein. Doppelte/widersprüchliche Werte müssen fail-closed behandelt werden.
- API-Audience und OIDC-Scope sind getrennte Konzepte. Beides muss im Keycloak-Mapper und in der Anwendung explizit konfiguriert werden.

## 4. Infrastruktur- und Konfigurationsinventur

- AppHost besitzt `Container`, `Project`, `ExternalServices`/`ExternalWslc`. Alle starten Studio/API aktuell als Development und setzen `StudioAuthentication:LocalDevelopmentEnabled=true`; daher erfolgt dort **kein echter OIDC-Login**.
- `scripts/wslc-apphost.ps1` verwaltet derzeit ausschließlich PostgreSQL und RabbitMQ. Namen/Volumes sind nicht laufisoliert. Keycloak darf nicht unbemerkt in dieses bestehende Lifecycle-/Cleanup-Verhalten aufgenommen werden.
- Studio-Standardkonfiguration enthält leere Authority/Clientdaten. Development enthält einen Klartext-Development-API-Key; dieser gehört nicht in das reale OIDC-Profil.
- API-Development verwendet lokalen API-Key und SQLite. Produktion verlangt Datenbankkonfiguration und JWT-Authority/SecretKey; konkrete Keycloak-Werte fehlen.
- Keycloak benötigt eine eigene persistente Datenbank/DB-Rolle. Engine-Migrationen dürfen diese Datenbank nicht verwalten.
- Callback für Studio-Login ist der Framework-OIDC-Callback (standardmäßig `/signin-oidc`, sofern K01 ihn nicht explizit konfiguriert). `/oauth2/callback` ist der getrennte Credential-OAuth2-Flow und darf nicht als Login-Redirect registriert werden.

## 5. Testinventur und Baseline

Ausgeführt im separaten Worktree:

```powershell
dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj -c Release -m:1 -p:SkipBpmnIoAssetBuild=true -v:q
tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.exe `
  -parallelMode none `
  -class '*TenantReadOnlyPolicyTests' `
  -class '*TenantIsolationPhase3SecurityTests' `
  -class '*TaskAndAnalyticsSecurityTests' `
  -class '*PrivilegeGateSecurityTests' `
  -class '*GrpcContractTests' `
  -class '*SignalRSecurityTests' `
  -result-xml TestResults/keycloak-k00-auth-baseline.xml
```

Ergebnis: **59/59 bestanden, 0 Fehler, 0 Skips**, 17,065 s. Release-Build erfolgreich, aber mit vorhandenen Compiler-/Analyzerwarnungen; keine Warnungsfreiheit behauptet. Das lokale XML ist ein nicht eingechecktes Testartefakt.

Die geprüften Tests decken Policy-/Tenant-/Adaptergrenzen mit Testidentitäten ab. Nicht vorhanden beziehungsweise nicht als echter Nachweis identifiziert:

- Browserlogin gegen einen realen OIDC-Provider.
- Keycloak-Rollen-/Tenant-/Audience-Mapper.
- Tokenablauf und Refresh im langlebigen Studio-Circuit.
- RP-initiated Logout, Kontosperre, Rollenentzug und MFA.
- Key-/JWKS-Rotation, IdP-Ausfall und Reverse-Proxy-/HTTPS-Fälle.
- Zwei Studio-Sitzungen/Replikate mit echter Cookie-/Tokenisolation.

## 6. K01-Entscheidungen in empfohlener Reihenfolge

1. Kanonischen providerneutralen Claimvertrag festlegen: `iss`, `sub`, Audience, Rollenquelle/-normalisierung und `tenant_id`.
2. Studio-Sessionmodell festlegen: Tokenservice, Refresh, Cookie-/Ticket-Lifetime, Logout und Replikazahl/Data-Protection.
3. Echtes lokales OIDC-Abnahmeprofil schaffen, das Development bleibt, aber Testauth/API-Key sicher deaktiviert.
4. Keycloak-Realm-/Client-/Mappervertrag definieren, erst danach Realm-Template und Bootstrap implementieren.
5. Konkrete gepinnte Keycloak-/PostgreSQL-Version sowie öffentliche/innere URL-, TLS- und Proxy-Topologie anhand aktueller Herstellerdokumentation festlegen.

Empfehlung für K01: `sub` zusammen mit geprüftem `iss` als Autorisierungsidentität, explizite Clientrollen des API-Clients als Quelle der drei Vertex-Rollen und ein administrativ gesetzter einzelner `tenant_id`-Claim. Das ist noch zu reviewen und nicht implementiert.

## 7. Grenzen und nächster Schritt

K00 hat keine Produktionsdatei und keine Infrastruktur verändert. Es wurde kein Keycloak installiert, gestartet oder konfiguriert. Die vorhandene Baseline darf nicht als echter IdP-Nachweis in Phase 3 markiert werden.

Nächster Schritt ist **K01 – OIDC-/Claims-/Sessionvertrag festlegen und mit Testspezifikationen freigeben**. Erst danach sollten Application-Code, Realm-Template oder WSLC/AppHost-Topologie geändert werden.
