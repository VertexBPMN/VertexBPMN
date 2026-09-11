# Austauschbarer OIDC-Provider: Keycloak Self-Hosting

Stand: 2026-09-11. Synchronisierte Basis: `ca5699e`.
Status: **K00–K04 abgeschlossen, K05 begonnen. T01–T08 sind gegen echten Keycloak vollständig bestanden. T09–T12, die vollständige Regression, K06, Betreiberwerte und das unabhängige Review bleiben offen.**
Branch: `codex/keycloak-k05-security-acceptance`.
Arbeitsverzeichnis: `C:/repo/VertexBPMN`.

## 1. Ziel und Grenzen

Keycloak als erstes geprüftes Self-Hosting-Referenzprofil für VertexBPMN anbieten. Studio und API bleiben an Standard-OIDC/OAuth2 angebunden. Engine und fachliche Dienste erhalten keine Keycloak-Admin-API-Abhängigkeit. Ein späterer Providerwechsel benötigt Konfiguration, Claim-Mapping, Identitätsmigration und neue Abnahme, aber keinen Engine-Umbau.

Erstes Lieferziel: echter Studio-Login mit Authorization Code + PKCE, API-Zugriff mit Access Token, wirksame Rollen-/Tenantgrenzen, definierte Session-/Refresh-/Logout-Semantik und reproduzierbare lokale Abnahme gegen Keycloak. Keycloak bleibt optional; bisherige ausdrücklich erlaubte Entwicklungs- und andere OIDC-Profile nicht entfernen.

Nicht im Umfang: Agent-Implementierung, Studio-Redesign, allgemeiner Benutzerverwaltungsbau, Keycloak-Fork, neue Engine-Semantik, vollständige HA-Zertifizierung oder automatische Produktionsinstallation. Machine-to-Machine-Identitäten sind vorzubereiten, aber neue Agent-Endpunkte werden nicht vorweggenommen.

Bezug zum [Produktionsqualitätsplan](2026-09-08_Produktionsqualitaet_Release-Abnahmeplan.md): Dieser Ausbau liefert den echten IdP-Nachweis für Phase 3. Er ersetzt nicht unabhängiges Security-Review oder die übrige Rollen-/Tenant-Matrix. Auch der separate Credential-OAuth2-Flow ist nicht automatisch durch einen Studio-Login abgenommen.

## 2. Beobachteter Ist-Stand

Pfade repository-relativ. Für die Planung wurden Dateien gelesen, keine neue Anwendung gestartet oder Tests ausgeführt.

| Bereich | Bestehender Code | Zu prüfen/ergänzen |
|---|---|---|
| Studio-OIDC | `src/VertexBPMN.Studio/Program.cs`: `StudioAuthentication:Authority`, `ClientId`, `ClientSecret`, `ApiScope`; Code + PKCE, `SaveTokens`, UserInfo, `MapInboundClaims=false` | Explizites Role-/Name-Mapping, Claimquelle, Session-Lifetime und sichere Token-Erneuerung. |
| Entwicklungsmodus | Dieselbe Datei aktiviert für lokale Entwicklung/Test den `UiTestAuthenticationHandler` | Separates echtes OIDC-Testprofil ermöglichen; lokale Keycloak-Abnahme darf nicht versehentlich Testauth benutzen. |
| API | `src/VertexBPMN.Api/Security/SecurityConfiguration.cs`: `Jwt:Authority`, `Issuer`, `Audience`; Policies verwenden `ClaimTypes.Role` | Konsistente Mappingstrategie und Signatur-/Issuer-/Audience-Negativtests; Flagwerte nicht isoliert als Beweis für fehlende Prüfung deuten. |
| Studio → API | `src/VertexBPMN.Studio/Services/StudioApiAuthorizationHandler.cs` liest Token aus `IHttpContextAccessor`, hat Development-API-Key-Fallback | Verfügbarkeit im langlebigen Blazor-Circuit, Refresh, Benutzerisolation und strikt eingeschränkten Development-Fallback verifizieren. |
| AppHost/WSLC | Bestehende AppHost-Konfiguration und `scripts/wslc-apphost.ps1` | Anschließbare Keycloak-Instanz ohne Docker-/Podman-Pflicht; alte Container-/Existing-Modi erhalten. |
| Identitätsbezüge | Rollen, `tenant_id`, User Tasks und SignalR-Empfänger im Projekt | `sub`/Issuer-/Tenant-Verwendung inventarisieren, keine fremden Identitäten bei Providerwechsel vermischen. |

## 3. Zielprofil und Claim-Vertrag

### Keycloak-Referenzprofil (Vorschlag)

- Ein Realm `vertexbpmn` pro Installation; nicht automatisch ein Realm pro Tenant. Mehrere gleichzeitige Issuer sind nicht Teil des ersten Profils.
- Vertraulicher Studio-Client `vertexbpmn-studio`, Authorization Code + PKCE S256. Secret ausschließlich serverseitig; kein Browsersecret, kein Implicit-/Password-Grant für Benutzerlogin.
- API-Ressource/Audience `vertexbpmn-api`. Access Tokens für API-Aufrufe müssen diese Audience besitzen. Kein ID Token als API-Berechtigung verwenden.
- Exakte Login-/Logout-Redirect-URIs und benötigte Origins, keine pauschalen Wildcards. Tatsächliche Callback-Pfade im Code verifizieren; den Credential-Callback `/oauth2/callback` nicht mit dem OIDC-Login-Callback verwechseln.
- Rollen `Admin`, `ProcessManager`, `ReadOnly` über explizite Mapper. Nicht sämtliche Realm-/Clientrollen oder Keycloak-Administrationsrollen als Vertex-Rechte übernehmen.
- `tenant_id` nur durch vertrauenswürdige Administration/Provisionierung setzen; kein vom Benutzer frei editierbares Profilfeld. Ein eindeutiger aktiver Tenant pro Identität im ersten Profil. Mehrfach-/leere/widersprüchliche Tenantclaims fail-closed behandeln; bestehende explizite Admin-Ausnahmen separat prüfen.
- Keine produktiven Benutzer, Passwörter oder Clientsecrets im Realm-Template. Isolierte Testbenutzer/Secrets zur Laufzeit erzeugen; Exporte vor Speicherung redigieren.

### Providerneutraler Anwendungskontrakt

Externer Vertrag: `iss`, `sub`, `aud`, `exp`, festgelegter Rollenclaim und `tenant_id`; Anzeigename ist keine Identität. Unterschiedliche Provider können über konfiguriertes, begrenztes Mapping denselben internen Vertrag liefern.

Interne Rollen so normalisieren, dass bestehende `ClaimTypes.Role`-Policies **und** Studio-Rollenprüfungen dasselbe Ergebnis erhalten. Mapping nur nach erfolgreicher Authentifizierung; keine Rechte aus Query/Headers/Body ergänzen. Zugriff auf Claims aus ID Token, Access Token und UserInfo getrennt prüfen; `Scope.Add(...)` allein garantiert keinen Claim.

Identitätsreferenzen logisch als `(issuer, subject)` behandeln. Vor Codeänderungen bestehende gespeicherte Benutzer-/Task-/Notification-IDs inventarisieren und nötige Migration planen. Kein globaler Austausch von IDs als Nebenarbeit. Bei späterem IdP-Wechsel explizite administrativ verifizierte Zuordnung, niemals automatisches Zusammenführen allein nach E-Mail.

### Konfiguration und Vertrauen

Vorhandene Konfigurationsschlüssel bevorzugen; zusätzliche Claim-/Sessionoptionen erst in K01 festlegen. Beispielwerte sind keine fertige Produktionskonfiguration:

```text
StudioAuthentication:Authority = https://identity.example.org/realms/vertexbpmn
StudioAuthentication:ClientId = vertexbpmn-studio
StudioAuthentication:ClientSecret = <Secret Store, nicht im Repository>
Jwt:Authority = https://identity.example.org/realms/vertexbpmn
Jwt:Audience = vertexbpmn-api
```

API-Scope und Keycloak-Clientscope/Audience-Mapper zusammen definieren, nicht `ApiScope` und Audience gleichsetzen. Wenn `Jwt:Issuer` gesetzt ist, muss er mit dem erwarteten Discovery-Issuer übereinstimmen. Kein freier Issuer aus Tenant-/Browserdaten.

Studio und API müssen denselben gültigen Issuer über die vorgesehenen Netzpfade erreichen. Lokale HTTPS-Zertifikate kontrolliert vertrauen; keine global deaktivierte Zertifikatsprüfung. Reverse-Proxy-/Forwarded-Header nur von bekannten Proxies akzeptieren.

## 4. Session-, Refresh- und Logout-Vertrag

- Access-/Refresh-Tokens serverseitig pro Sitzung verwalten, nicht in localStorage oder Logs. `SaveTokens=true` nicht mit fertigem Refreshmanagement verwechseln.
- Authentifizierte API-Aufrufe auch nach längerem Blazor-Circuit nachweisen. `HttpContext` nicht als dauerhaft garantierte Tokenquelle behandeln; gegebenenfalls sitzungsgebundenen Tokenservice einführen.
- Refresh pro Sitzung synchronisieren, Rotation und verlorene Antworten behandeln; nie Token eines Benutzers in Singleton-/gemeinsamen Handlerzustand speichern.
- Shared Data-Protection und gegebenenfalls gemeinsamer Ticket-/Tokenstore für zugesagte Studio-Replikate. Keine stillen Logout-Schleifen beim Wechsel der Replikate.
- Cookie-/OIDC-Logout und Ende der lokalen Sitzung implementieren; API-Token nicht länger erneuern. Serverinitiierter Session-/Rollenentzug muss auch einen offenen Circuit innerhalb des vereinbarten Fensters einschränken.
- Offline validierte JWTs können bis zu ihrem Ablauf gültig bleiben. Vorschlag für den Pilot: Access Token maximal 5 Minuten, explizit begrenzter Clock-Skew, dokumentiertes Entzugsfenster; final vor K03 bestätigen. Sofortiger Widerruf erfordert zusätzliche Mechanismen und ist keine Standardzusage.
- Bei IdP-Ausfall keine API-Key-/Anonymous-/Testauth-Rückfallstrategie. Verhalten mit bereits gültigen Tokens und gecachten Schlüsseln versus neuem Login/Refresh explizit testen.
- Login und Connector-Credential-OAuth2 bleiben getrennte Vertrauenspfade. Eine privat erreichbare Keycloak-Authority ist kein Anlass, den allgemeinen Connector-SSRF-Schutz zu lockern.

## 5. Arbeitspakete

Größen: S ≈ 1–2, M ≈ 3–5, L ≈ 6–10 Entwicklertage inklusive Tests; keine garantierte Modelllaufzeit. Reihenfolge K00 → K01 → K02/K03 → K04 → K05 → K06. Parallel nur ohne gemeinsame Dateiänderungen und nach freigegebenem Vertrag.

| ID | Paket | Aufwand | Abhängigkeit |
|---|---|---|---|
| K00 | Auth-Inventur und Baseline | M | keine |
| K01 | OIDC-/Claims-/Sessionvertrag festlegen | M | K00 |
| K02 | Reproduzierbares Keycloak-Self-Hosting-Profil | M | K01 |
| K03 | Studio/API providerneutral vervollständigen | L | K01 |
| K04 | AppHost/WSLC/Existing und lokale Anmeldung integrieren | M | K02, K03 |
| K05 | Echte IdP-, Tenant- und Sessionabnahme | L | K04 |
| K06 | Betrieb, Austauschbarkeit und Übergabe | M | K05 |

### K00 – Inventur

- [x] Login-/Logout-Controller, Middleware, Proxyhandling, Tokenweitergabe und vorhandene Sessiondienste vollständig verfolgen.
- [x] API-Policies, Studio-Rollen, UserInfo-Claim-Mapping, SignalR/gRPC und Benutzerreferenzen inventarisieren.
- [x] Bestehende IdP-/Security-/Browser-Tests lesen; Mockauth, signierte Testtokens und echten OIDC-Flow unterscheiden.
- [x] Neu auftauchende Findings reproduzieren und abgrenzen; vorhandene funktionierende Provider nicht entfernen.

Abnahme: Matrix Pfad → Konfiguration → Claimquelle → Autorisierung → Test → offene Grenze. Keine pauschale Aussage „OIDC fertig“, nur weil Login konfiguriert ist.

### K01 – Verträge freigeben

- [x] Abschnitte 3/4 präzisieren: Claimnamen, Mapper, Rollenquelle, Tenantadministration, Issuer-/Subject-Identität.
- [x] Callback-/Logout-/API-Scopes, Tokenlaufzeiten, Clock-Skew, Sessionentzug, Replikazahl und Refreshstrategie festlegen.
- [x] Produktionshostname, TLS-Verantwortung, Keycloak/PostgreSQL-Versionen und Backupverantwortung klären. Lokale synthetische Profile dürfen unabhängig vorbereitet werden.
- [x] Konfigurationsprüfung festlegen: unvollständige Produktionskonfiguration bricht erklärend ab; kein automatischer Wechsel in Development.

Abnahme: versionierter Vertrag plus positive/negative Beispiele; offene Betreiberentscheidungen sichtbar. Wahl einer konkreten gepinnten unterstützten Keycloak-Version anhand aktueller offizieller Dokumentation, kein `latest`.

### K02 – Keycloak-Profil

- [x] Realm-/Clientscope-/Rollen-/Mapper-Template unter vorgeschlagenem `deploy/keycloak/` anlegen, ohne echte Secrets oder Benutzer.
- [x] Idempotenten Bootstrap für eine **dedizierte Testinstanz** erstellen: Secret-Injektion, Testbenutzer, MFA-Testkonto, Rollen/Tenants. Kein destruktiver Import über bestehende Realms.
- [x] Eigene Keycloak-Datenbank/DB-Rolle; keine Engine-Tabellen oder Engine-Migrationen für Keycloak verwenden.
- [x] Start/readiness, gepinnte Version und Konfiguration reproduzierbar dokumentieren. Dev-Start nicht als Produktionsprofil ausgeben.

Abnahme: frische isolierte Instanz liefert Discovery/JWKS und korrekt gemappte echte Tokens; Wiederholung zerstört keine Daten. Template ohne Secretwerte versionierbar.

### K03 – Anwendung vervollständigen

- [x] Claimnormalisierung und explizite Rollen-/Namenszuordnung für Studio/API implementieren; Tokenissuer, Audience, Signatur, Lebensdauer und fehlende Claims negativ testen.
- [x] Tokenservice und Refresh/Logout aus Abschnitt 4 implementieren oder vorhandene Funktionen nachweisen. Circuit-Lebensdauer und Mehrbenutzerisolation berücksichtigen.
- [x] Development-API-Key-Fallback nur im ausdrücklich aktivierten Entwicklungsprofil erlauben; im realen OIDC-Abnahmemodus ausschließen.
- [x] Konfigurierbares echtes OIDC-Profil für lokale Tests schaffen, ohne vorhandene UI-Testauth oder normale lokale Entwicklung zu brechen.
- [x] Bestehende API-, Rollen-, Tenant- und Credential-OAuth2-Verträge regressionsprüfen. Keine Keycloak-spezifischen SDK-Aufrufe in Domain/Engine.

Abnahme: deterministische positive/negative Integrationstests und Sessiontests; externe reale Login-Abnahme folgt erst K05.

### K04 – Lokale Topologie und AppHost

- [x] Vorhandene Keycloak-Instanz über Authority konfigurieren; das ist der Standard für `Existing`, nicht zwangsläufig ein durch AppHost verwalteter Container.
- [x] Optionalen WSLC-Teststart mit dedizierten Namen/Ports/Volumes ergänzen. Bestehende Docker-/Podman- und lokale PostgreSQL-/RabbitMQ-Profile unverändert nutzbar halten.
- [x] Browser, Studio, API und Keycloak erreichen die vorgesehenen URLs; externe Issuer-URL darf nicht gegen einen beliebigen internen Hostnamen ausgetauscht werden.
- [x] Isolierte Testports/DBs nutzen; Ressourcen nach Eigentum tracken. Cleanup löscht ausschließlich vom Testlauf erzeugte Ressourcen.
- [x] OIDC-Abnahmelauf bewusst ohne Testauth/Development-API-Key; Browser muss tatsächlich zur Keycloak-Anmeldung navigieren.

Abnahme: lokaler vollständiger Login → Studio → echte API; keine Docker-Pflicht oder Änderung an aktiven Benutzer-Diensten.

### K05 – Sicherheitsabnahme

- [ ] T01–T12 ausführen, Loginbrowser gegen echten Keycloak; keine reine Token-Mock-Suite als Ersatz.
- [ ] Testhost bei fehlendem Keycloak als nicht abgenommen/fehlgeschlagen melden, nicht still auf Testauth wechseln.
- [ ] Schlüsselrotation, IdP-Unterbrechung, Rollenentzug und Sessionablauf kontrolliert mit isolierter Instanz testen; echte Requests nach Ereignis prüfen.
- [ ] Lokale Regression anderer Authprofile, SDK-/gRPC-/SignalR-Zugriffe im relevanten Umfang durchführen.

Abnahme: keine Fehler/übersprungenen Pflichtfälle; Berichte nennen Commit, Provider-/DB-Version, Modus, Zahlen und Grenzen. Keine zusätzlichen GUI-Gates in GitHub CI.

### K06 – Betrieb und Austauschbarkeit

- [ ] Runbook für Installation, Secrets, MFA, Loginfehler, Zertifikats-/Clientsecretrotation, Backup und Upgrade erstellen.
- [ ] Realm-Template ist kein vollständiges Backup: DB-/Schlüssel-/Konfigurationswiederherstellung isoliert testen oder als offene Betriebsabnahme ausweisen.
- [ ] Providerneutrales Konfigurationsbeispiel und Claim-Vertrag veröffentlichen. Deterministischer alternativer OIDC-Testissuer belegt fehlende Keycloak-Codekopplung, aber nicht vollständigen Support eines anderen realen Produkts.
- [ ] Produktionsprofil und Phase-3-Plan nur anhand tatsächlich bestandener Nachweise aktualisieren. Unabhängiges Review bleibt separat offen.
- [ ] Finalen Commit aus sauberem Checkout nachqualifizieren; Dirty-Lauf nicht als finale Releaseabnahme ausgeben.

Abnahme: zweiter Ausführender kann das lokale Referenzprofil anhand Runbook einrichten; Restgrenzen und Betreiberentscheidungen sichtbar.

## 6. Pflicht-Testfälle

| ID | Ablauf | Erwartung |
|---|---|---|
| T01 | Echte Browseranmeldung mit Code + PKCE, API-Aufruf | Keycloak-Redirect sichtbar, API akzeptiert Access Token; keine Testauth/API-Key-Nutzung. |
| T02 | Admin/ProcessManager/ReadOnly, gleiche/fremde Tenantdaten | Bestehende Rollenmatrix wirksam in API und UI; fehlender/widersprüchlicher Tenant ohne Rechtegewinn. |
| T03 | Falscher Issuer/Audience, manipulierte/unsignierte/abgelaufene Tokens | 401 ohne fachliche Nebenwirkung; ID Token nicht als API-Zugang akzeptiert. |
| T04 | Offener Blazor-Circuit über Access-Token-Ablauf, parallele API-Aufrufe | Kontrollierter Refresh oder erneuter Login; keine Race-bedingte Tokenverwechslung. |
| T05 | Zwei gleichzeitige Benutzersitzungen/Tenants | Kein Token-/Cookie-/Claims-Leak zwischen Sitzungen oder HttpClient-Handlern. |
| T06 | Logout lokal/IdP, Rollenentzug oder Kontosperre | Erneuerung unterbunden; offener Circuit und API innerhalb des vereinbarten Entzugsfensters eingeschränkt. |
| T07 | MFA-Testkonto, falscher/fehlender zweiter Faktor | Kein Studio/API-Zugang vor erfolgreichem Faktor; keine Umgehung durch alternative lokale Route. |
| T08 | Signing-Key-Rotation, neue Tokens, gecachte alte Schlüssel | Kontrollierter JWKS-Refresh; gültige Übergangsphase und ungültige Tokens korrekt behandelt. |
| T09 | Keycloak stoppen, später starten | Definiertes Verhalten für bestehende Tokens versus Login/Refresh; kein Sicherheitsfallback, keine endlose Redirectschleife. |
| T10 | Proxy, HTTPS, manipulierte Redirect-/Forwarded-Header, falsche Callback-URI | Kein Open Redirect oder fremder Issuer, verständlicher Fehler statt Zertifikatsbypass. |
| T11 | Zwei Studio-Replikate bzw. Neustart mit zugesagtem Sessionstore | Sitzung/Refresh konsistent oder bewusst erneute Anmeldung; keine unbemerkte Rechtefortschreibung. |
| T12 | Anderer konfigurierter Testissuer mit demselben Claim-Vertrag | Keine Keycloak-spezifische Domain-/Engine-Abhängigkeit; fremde Issuer weiterhin abgewiesen. |

Im ersten Profil Machine-to-Machine nur dann real abnehmen, wenn ein vorhandener Verbraucher/Scope konkret benannt ist. Kein Admin-Token als Platzhalter für künftige Agent-Worker. Credential-OAuth2-Consent separat testen, falls hierfür ebenfalls Keycloak verwendet werden soll.

## 7. Betriebs- und Übergaberegeln

- K05 ausschließlich auf `codex/keycloak-k05-security-acceptance` in `C:/repo/VertexBPMN` bearbeiten; Änderungen anderer Arbeitszweige nicht übernehmen oder überschreiben.
- Worktrees isolieren Dateien, **nicht** Ports, Datenbanken, Secrets oder Git-Refs. Eigene Testressourcen verwenden; keine gemeinsamen User-Secrets verändern. Updates vom Hauptbranch nur kontrolliert integrieren.
- Pro Paket: Status, Basiscommit, Dateien, Entscheidungen, Testbefehle/-zahlen, Artefaktpfade und offene Punkte dokumentieren. Produktionssecrets nie in Übergaben kopieren.
- Keine automatischen Commits/Pushes/PRs ohne Nutzerauftrag. Bei paralleler Arbeit an `Program.cs`, SecurityConfiguration oder AppHost Integration abstimmen; ein separater Branch verhindert spätere Mergekonflikte nicht vollständig.
- Falls Codex/anderer Agent im Hauptcheckout gestartet wird, vor jeder Änderung explizit in diesen Worktree wechseln; Branchname allein ändert nicht das Arbeitsverzeichnis der App.

Kopierbarer Auftrag:

> Arbeite in `C:/repo/VertexBPMN` auf dem für das freigegebene K-Paket angelegten `codex/`-Branch. Lies diesen Plan vollständig sowie die dort geltenden Repository-Anweisungen. Prüfe vorhandene Implementierung und sichere Claims/Sessiongrenzen mit positiven und negativen Tests ab. Keycloak bleibt austauschbarer OIDC-Provider; keine Enginekopplung, keine schwächere Authentifizierung für grüne Tests. Verwende isolierte lokale Testressourcen, keine echten Secrets in Dateien. Aktualisiere den Status und benenne fehlende echte Abnahmen ausdrücklich. Kein Commit/Push ohne Auftrag.

## 8. Quellen und Fortschritt

Offizielle Quellen, geprüft zur Planung am 2026-09-09; konkrete versionsabhängige Einstellungen vor Umsetzung erneut prüfen:

- [Keycloak OIDC-Endpunkte und Discovery](https://www.keycloak.org/securing-apps/oidc-layers): Standard-Discovery, Token, UserInfo, JWKS und Logout.
- [Keycloak Produktionskonfiguration](https://www.keycloak.org/server/configuration-production): TLS, Hostname/Proxy, Produktionsdatenbank und Verfügbarkeit.
- [Realm-Import/Export](https://www.keycloak.org/server/importExport): reproduzierbare Konfiguration und Grenzen bei bestehenden Realms; kein Ersatz für ein geprüftes vollständiges Betriebsbackup.

- [x] Plan erstellt und separaten Arbeitsbranch/Worktree angelegt.
- [x] K00 – Inventur/Baseline. Ergebnis: [K00 Auth-Inventur](2026-09-09_Keycloak-OIDC_K00_Inventur.md); vorhandene Auth-/Tenant-Regressionen 59/59 grün, jedoch kein echter OIDC-/Keycloak-Nachweis.
- [x] K01 – Technischer Pilotvertrag. Ergebnis: [K01 Claim-/Sessionvertrag](2026-09-09_Keycloak-OIDC_K01_Vertrag.md). Produktionshost, Secret Store, HA und Backupverantwortung bleiben Betreiberentscheidungen; unabhängiges Review offen.
- [x] K02 – Keycloak-Referenzprofil. Frischer Import, Discovery/JWKS, echter signierter Token und zweiter idempotenter Bootstrap am 2026-09-09 lokal gegen Keycloak 26.7.3 bestanden.
- [x] K03 – Studio/API-Anbindung. Claimnormalisierung, `OidcTest`-Konfiguration, echter JWT-Handlervertrag, sitzungsgebundener Tokenrefresh, Mehrsitzungsisolation und OIDC-Logout sind implementiert. 27 gezielte Security-/Sessiontests sowie die vollständige serielle Kernsuite mit 975 Tests und 0 Fehlern bestanden am 2026-09-10; 18 explizit externe Infrastrukturtests wurden übersprungen. Der frühere Zwischenstand bleibt als Implementierungsprotokoll erhalten: [K03 Zwischenstand](2026-09-09_Keycloak-OIDC_K03_Zwischenstand.md).
- [x] K04 – Lokale AppHost-/WSLC-/Existing-Topologie. Der reale lokale Browserlauf hat Keycloak-Redirect, Login, API-gestützte Studio-Seiten, echten Refresh und IdP-Logout mit 1/1 Tests bestanden. Nach dem Master-Sync bestanden Release-Build und 60 Studio-Vertragstests; zwei weitere lokale Opt-in-Tests wurden erwartungsgemäß übersprungen. Der reale Keycloak-Lauf wurde nach dem Sync mangels gesetzter lokaler Secrets nicht erneut gestartet.
- [ ] K05 – Reale IdP-/Sicherheitsabnahme.
  - Zwischenstand: [K05 Sicherheitsabnahme – Zwischenstand 1](2026-09-10_Keycloak-OIDC_K05_Zwischenstand.md). Reale Browser-/API-Suite 11/11 und fokussierte API-Regression 10/10 grün; T01–T08 sind vollständig belegt. T09–T12 und die vollständige Regression bleiben offen.
- [ ] K06 – Runbook/Austauschbarkeit/Übergabe.

**Nächster Umsetzungsschritt: K05 fortsetzen.** T09 mit echter Keycloak-Unterbrechung und Wiederanlauf belegen; danach T10–T12 sowie die Auth-/SDK-/gRPC-/SignalR-Regression ausführen. K06 folgt erst danach; eine Produktionsfreigabe wird aus diesem Zwischenstand nicht abgeleitet.
