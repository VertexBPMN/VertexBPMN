# Keycloak/OIDC – K01 Claim-, Session- und Betriebsvertrag

Stand: 2026-09-09. Basis: `591655e6b75b17239b587eb5e50b4d97085d2ecd`.
Status: **Technischer Pilotvertrag festgelegt; Implementierung und externe Review offen.**

Dieser Vertrag gilt für das erste Keycloak-Self-Hosting-Referenzprofil. Er ist providerneutral formuliert; Keycloak liefert ihn über explizite Protocol Mapper. Änderungen an den sicherheitsrelevanten Festlegungen benötigen aktualisierte Negativtests.

## 1. Identität und Claims

| Zweck | Vertrag | Fehlerverhalten |
|---|---|---|
| Issuer | Exakt konfigurierte HTTPS-Authority; lokal im isolierten Testprofil darf explizit HTTP verwendet werden. | Anderer/fehlender Issuer: 401. Keine Issuerwahl aus Request/Tenant. |
| Subject | Genau ein nichtleeres `sub`; kanonische externe ID ist `(iss, sub)`. | Fehlend/mehrdeutig: Authentifizierung ablehnen. |
| API-Audience | Access Token enthält `vertexbpmn-api` in `aud`. | Fehlend: 401. ID Token ist kein API-Token. |
| Rollenquelle | Top-Level-Claim `roles`, mehrwertig. Keycloak mappt ausschließlich Clientrollen von `vertexbpmn-api`. | Unbekannte Rollen werden ignoriert; keine Realm-/Managementrolle erzeugt Vertex-Rechte. |
| Interne Rollen | Whitelist `Admin`, `ProcessManager`, `ReadOnly`; nach Tokenprüfung zusätzlich als `ClaimTypes.Role` normalisieren, damit bestehende Policies/UI konsistent bleiben. | Keine Rolle: authentifiziert, aber für geschützte Produktoperationen 403. |
| Tenant | Genau ein nichtleerer Top-Level-Stringclaim `tenant_id`, administrativ gesetzt. | Mehrere, leere oder widersprüchliche Werte: Tokenvalidierung ablehnen. Admin darf nur dort ohne Tenant arbeiten, wo bestehende Policy das ausdrücklich vorsieht. |
| Anzeigename | `preferred_username`, danach `name`; nur Anzeige. | Keine Verwendung als stabile Autorisierungsidentität. |

Keycloak-Clientrollen: `Admin`, `ProcessManager`, `ReadOnly`. Rollen sind nicht hierarchisch im Token zu erweitern; vorhandene Policies definieren, welche Rolle welche Operation erlaubt. `realm_access.roles` und `resource_access` werden nicht direkt von VertexBPMN interpretiert. Der Realm-Mapper erzeugt den begrenzten Top-Level-Claim `roles`.

K01 führt noch keine globale Migration bestehender Assignee-/User-IDs durch. Neue OIDC-bezogene Sicherheitsentscheidungen verwenden `(iss, sub)`. Die Vereinheitlichung bestehender User-Task-/Notification-IDs wird als separates kompatibilitätsrelevantes Finding geführt und darf den Pilot nicht stillschweigend verändern.

## 2. Clients und Flows

### Studio

- Client-ID `vertexbpmn-studio`, confidential server-side client.
- Authorization Code Flow mit PKCE S256; Implicit Flow, Direct Access Grants/Password Grant und Device Flow deaktiviert.
- Exakte Redirect-URI `{StudioBaseUrl}/signin-oidc`; exakte Post-Logout-URI `{StudioBaseUrl}/signout-callback-oidc` beziehungsweise der tatsächlich konfigurierte Frameworkpfad.
- Web Origins nur konkrete Studio-Origin. Keine `*`-Redirects oder pauschalen Origins.
- Scopes: `openid`, `profile`, ein benannter Clientscope für `roles`, `tenant_id` und API-Audience. Das bestehende `StudioAuthentication:ApiScope` wird als konfigurierbarer Scope behandelt, nicht als Audience-Ersatz.
- Clientsecret ausschließlich Secret Store/User Secrets/Deploymentsecret, niemals Realm-JSON oder Repository.

### API

- Keine Keycloak-Adapterbibliothek und kein Clientsecret für normale JWT-Validierung.
- Discovery/JWKS von der konfigurierten Authority; Signatur, exakter Issuer, Audience und Lifetime verpflichtend.
- Clock-Skew Pilot: maximal 30 Sekunden. Endwert nach echter Zeit-/Proxyprüfung bestätigen.
- HTTPS-Metadaten im Produktionsprofil obligatorisch. Lokales OIDC-Testprofil muss die HTTP-Ausnahme ausdrücklich und ausschließlich dort konfigurieren.

### Machine-to-Machine

Nicht Teil der ersten Login-Abnahme. Spätere Worker erhalten eigene Clients/Service Accounts, Scopes und Policies. Kein Admin-Token als generischer Maschinenzugang.

## 3. Sitzung und Token

- Pilotwerte: Access Token 5 Minuten; SSO Session Idle 30 Minuten; SSO Session Max 8 Stunden. Offline Tokens nicht anfordern.
- Studio-Authentifizierungscookie: `Secure=Always` im HTTPS-Profil, `HttpOnly=true`, `SameSite=Lax`; Sliding Cookie ersetzt keine OIDC-Tokenerneuerung.
- Token bleiben serverseitig im geschützten Authentifizierungsticket beziehungsweise einem sitzungsgebundenen Serverstore. Keine Tokens in localStorage/sessionStorage, URL oder Anwendungslogs.
- Ein sitzungsgebundener Tokenservice muss Access-Token-Ablauf erkennen und Refresh serialisieren. Gleichzeitige API-Aufrufe einer Sitzung dürfen nur eine Refreshoperation auslösen. Rotation aktualisiert Access-/Refresh-/ID-Token atomar im Ticketstore.
- `IHttpContextAccessor.GetTokenAsync` im DelegatingHandler ist nicht der Zielvertrag für langlebige Blazor-Circuits; K03 ersetzt oder kapselt diesen Pfad nach einem reproduzierbaren Test.
- Refreshfehler `invalid_grant`, Kontosperre, Sessionende oder fehlender Refresh Token beenden die lokale Sitzung und führen kontrolliert zum Login. Kein Development-API-Key-Fallback.
- Zwei Benutzer-/Tenantsitzungen teilen weder Tokenzustand noch HttpClient-Authentifizierungsheader. Kein Token in Singletonfeldern.

Für zwei Studio-Replikate benötigt das Zielprofil gemeinsame Data-Protection-Keys und einen gemeinsamen Ticket-/Tokenstore. Der lokale Pilot darf eine Studio-Replikat verwenden; T11 bleibt bis Mehrreplikatkonfiguration offen und darf nicht als bestanden markiert werden.

## 4. Logout, Entzug und Ausfall

- Logout ist eine antiforgery-geschützte POST-Aktion im Studio. GET darf keine Sitzung verändern.
- Zuerst lokale Sitzung invalidieren, danach standardkonformen RP-Initiated Logout mit `id_token_hint` und erlaubter Post-Logout-URI auslösen.
- Nach Logout keine Tokenerneuerung oder API-Nutzung aus dem alten Circuit. Offene Verbindungen schließen beziehungsweise verlieren Autorisierung.
- Rollenentzug/Kontosperre wirkt bei bereits ausgestellten JWTs spätestens nach Access-Token-Lifetime plus Clock-Skew. Zielwert Pilot: höchstens 5 Minuten 30 Sekunden. Sofortwiderruf wird nicht versprochen.
- Bei Keycloak-Ausfall: kein neuer Login/Refresh; bereits gültige API-Tokens können bis Ablauf und bei verfügbarer Signaturmetadatenbasis funktionieren. Kein Wechsel auf API-Key/Testauth. Fehler müssen endlich bleiben, ohne Redirectschleife.
- Signing-Key-Rotation: regulärer JWKS-Refresh; neue und während der Keycloak-Überlappungsphase noch gültige alte Tokens prüfen. Entfernte Schlüssel dürfen nicht unbegrenzt akzeptiert werden.

## 5. Profile und Konfiguration

### Lokaler echter OIDC-Pilot

- ASP.NET-Environment `OidcTest`, nicht `Development` und nicht `UiTest`.
- Dedizierte Keycloak-Instanz `vertexbpmn-keycloak-test` auf Loopback-Port `58080`; dedizierte PostgreSQL-Datenbank/DB-Rolle. HTTP ist ausschließlich für den Loopback-Test erlaubt.
- API und Studio laufen mit echter Authority/Clientkonfiguration. `StudioAuthentication:LocalDevelopmentEnabled=false`, `UiTestEnabled=false`, `Jwt:UseDevelopmentApiKey=false`; kein `X-API-Key`.
- Gepinnte Pilotversion: Keycloak `26.7.3`, laut offizieller Downloadseite am 2026-09-09 aktuell. Vor Merge/Release erneut prüfen; kein `latest`-Tag.

### Produktion

- Öffentliche Authority `https://<identity-host>/realms/vertexbpmn`, feste Hostname-/Proxykonfiguration und TLS.
- Keycloak-Adminoberfläche wenn möglich auf getrenntem Host/Pfad und nicht öffentlich exponiert.
- Eigene unterstützte PostgreSQL-Datenbank, Backups, Key-/Secretrotation, Readiness `/health/ready`, Ressourcenlimits und Monitoring.
- Konkrete Hostnamen, Secret Store, HA-Replikate und Backupverantwortung bleiben betreiberspezifische Pflichtwerte. Fehlende Produktionswerte führen zu Start-/Deploymentfehlern, nicht zu Development-Fallback.

## 6. Keycloak-Mappervertrag

Realm `vertexbpmn`:

1. Client `vertexbpmn-api` definiert die drei Clientrollen.
2. Gemeinsamer Clientscope `vertexbpmn-claims` erzeugt:
   - Audience `vertexbpmn-api` im Access Token.
   - Top-Level `roles` als mehrwertige Liste ausschließlich der Rollen des API-Clients.
   - Top-Level `tenant_id` als einzelner String aus administrativ verwaltetem Benutzerattribut.
3. Studio-Client erhält diesen Scope. Claims werden mindestens ins Access Token aufgenommen; die Studio-UI benötigt die normalisierten Rollen auch in ihrer authentifizierten Identität, daher zusätzlich ID Token/UserInfo gemäß getesteter .NET-Verarbeitung.
4. `sub`, `iss`, `aud`, `exp`, `iat` bleiben Standardclaims. E-Mail ist weder Tenant noch stabile ID.

Das Realm-Template enthält keine echten Nutzer oder Secrets. Testbootstrap erzeugt mindestens Admin/ProcessManager/ReadOnly für Tenant A sowie ReadOnly für Tenant B und ein MFA-Testkonto aus zur Laufzeit injizierten Werten.

## 7. Verbindliche K03-Testvorgaben

Vor realem Keycloak werden providerneutrale Tests ergänzt:

- Flat-`roles`-Claim normalisiert exakt die drei erlaubten Rollen; unbekannte/Keycloak-Adminrollen nicht.
- Realm-/verschachtelte Rollen ohne Mapper erzeugen keine Berechtigung.
- Null, leerer und mehrfacher `tenant_id`, fehlendes/mehrfaches `sub` werden abgelehnt.
- Audience/Issuer/Lifetime bleiben durch JWT-Middleware geprüft; Transformation darf unvalidierten Claims niemals vertrauen.
- Studio und API erhalten für denselben Tokenvertrag dieselben Rollenwerte.
- Development-Fallback ist im `OidcTest`-Profil deaktiviert.
- Zwei simulierte Sitzungen und parallele Refreshanforderungen vermischen keine Tokens; nur eine Refreshoperation pro Sitzung.
- Logout ist POST/antiforgery-geschützt; alter Circuit kann anschließend nicht weiter auf API zugreifen.

Erst K05 bewertet diese Verträge mit echten Keycloak-Tokens und Browsernavigation.

## 8. Offene Punkte und Reviewgrenze

- Betreiber muss Produktionshostnamen, Zertifikate, Secret Store, Keycloak-/Studio-Replikatzahl und Backupverantwortung festlegen.
- Eine Studio-Replikat ist der lokale Pilot; T11/HA bleibt offen, bis gemeinsamer Ticket-/Keyring-Store gewählt ist.
- Migration vorhandener Assignee-/Notification-IDs auf issuergebundene Subjects wird separat entworfen.
- Dieser Eigenentwurf ersetzt kein unabhängiges Security-Review. K02/K03 können auf diesem Pilotvertrag beginnen; Änderungen am Rollen-/Tenantmodell benötigen erneute Freigabe.
