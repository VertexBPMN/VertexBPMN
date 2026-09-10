# Keycloak/OIDC – K03 Zwischenstand Claimnormalisierung

Stand: 2026-09-09. Basis: `591655e6b75b17239b587eb5e50b4d97085d2ecd`.
Status: **Claimnormalisierung und abgesichertes lokales `OidcTest`-Profil implementiert; K03 insgesamt offen.**

## Implementiert

- Providerneutraler Validator `VertexOidcClaims` in `VertexBPMN.ServiceDefaults`.
- Authority-basierte API-JWTs verlangen genau ein nichtleeres `sub` und `tenant_id`.
- Nur die Top-Level-Claimquelle `roles` kann die Whitelist `Admin`, `ProcessManager`, `ReadOnly` nach `ClaimTypes.Role` normalisieren.
- Bereits vorhandene, nicht aus der freigegebenen Quelle stammende `ClaimTypes.Role`-Claims werden für diesen OIDC-Pfad entfernt. Unbekannte und verschachtelte Keycloak-Rollen verleihen keine Rechte.
- Studio verwendet denselben Validator nach OIDC-Tokenprüfung und explizite Name-/Role-Claimtypen sowie 30 Sekunden Clock-Skew.
- Der lokale API-Modus mit `Jwt:SecretKey` bleibt vom neuen OIDC-Vertrag unberührt; die Normalisierung wird dort nicht aktiviert.
- API und Studio besitzen ein separates `OidcTest`-Profil für die Loopback-Authority. Unsichere HTTP-Metadaten sind außerhalb von Development ausschließlich in diesem Profil und ausschließlich für eine Loopback-Authority erlaubt.
- Der Development-API-Key kann im `OidcTest`-Profil nicht zur Standardscheme werden. Der vertrauliche Studio-Client verlangt dort ein Secret aus externer Konfiguration.
- Studio kann einen providerneutral benannten Claims-Scope konfigurieren; bestehende Deployments ohne diesen Wert behalten die bisherigen Scope-Anforderungen.

Produktionsdateien:

- `src/VertexBPMN.ServiceDefaults/Security/VertexOidcClaims.cs`
- `src/VertexBPMN.Api/Security/SecurityConfiguration.cs`
- `src/VertexBPMN.Studio/Program.cs`

Tests:

- `tests/VertexBPMN.Tests/Unit/Security/VertexOidcClaimsTests.cs`
- `tests/VertexBPMN.Tests/Unit/Security/OidcTestProfileSecurityTests.cs`
- `tests/VertexBPMN.Tests/Unit/Security/KeycloakRealmTemplateTests.cs`

## Tatsächlich ausgeführt

Erster Lauf: 52 Tests, zwei Fehler. Root Cause war die Mutation der ClaimsIdentity während einer lazy Enumeration der Rollen. Der Produktionscode materialisiert die normalisierten Rollen nun vor dem Hinzufügen; kein Test wurde abgeschwächt.

Abschließender gezielter Lauf nach Korrektur:

- Release-Build: **0 Fehler, 0 Warnungen** im inkrementellen Lauf.
- `VertexOidcClaimsTests`, `TenantReadOnlyPolicyTests`, `TenantIsolationPhase3SecurityTests`, `PrivilegeGateSecurityTests`: **52/52 bestanden, 0 Fehler, 0 Skips**, 4,321 s.
- Lokales Ergebnis: `TestResults/keycloak-k03-claims-final.xml` (nicht eingechecktes Artefakt).
- Nach der anschließenden Kompatibilitätsbegrenzung auf Authority-basierte JWTs: erneuter Release-Build **0 Fehler/0 Warnungen** und Claimtests **8/8 bestanden**, 0 Fehler/0 Skips (`TestResults/keycloak-k03-claims-post-compat.xml`).
- Nach Ergänzung des Realm-Templates und `OidcTest`-Profils: **15/15** gezielte Tests im konfliktfreien temporären Checkout bestanden. Der normale Worktree war währenddessen durch fremde parallele MSBuild-Knoten gesperrt; vorhandene Warnungen außerhalb dieses Scopes wurden nicht verändert.
- Reale K02-Abnahme gegen WSLC und `quay.io/keycloak/keycloak:26.7.3`: frischer Realm-Import, erwartete Discovery, zwei JWKS-Schlüssel, vertraulicher Code+PKCE-Client, zwei Testnutzer, erzwungene MFA-Einrichtung und admin-only verwaltetes `tenant_id` bestanden.
- Ein real signierter Keycloak-Access-Token enthielt den Vertrag `iss`, `sub`, Audience `vertexbpmn-api`, Rolle `ProcessManager` und `tenant_id=tenant-a`. Ein zweiter Bootstrap gegen dieselben Ressourcen war erfolgreich und nicht destruktiv. Die temporären Container, Datenbank, das Volume und Netzwerk wurden danach entfernt.
- AppHost-Topologietests: **7/7 bestanden**. Der bisherige External-Services-Entwicklungsmodus bleibt unverändert; `OidcTest` setzt API und Studio explizit auf OIDC, deaktiviert lokale Auth/API-Key, begrenzt unsicheres HTTP auf Loopback und übergibt das Studio-Secret als geheimen Aspire-Parameter.
- Reale lokale K04-Verbindungsprobe mit WSLC, Keycloak 26.7.3 und Aspire 13.5.3: Discovery **200**, API `/api/ready` **200/Healthy**, Studio `/` und `/authentication/login` jeweils **302** zum echten Keycloak-Authorization-Endpunkt mit erfolgreicher PAR-Clientauthentifizierung. Das ist noch kein abgeschlossener Benutzerlogin.
- Der Logout-Pfad ist inzwischen ausschließlich POST, verlangt Autorisierung und validiert Antiforgery vor dem lokalen beziehungsweise OIDC-Sign-out. GET ist nicht geroutet und POST ohne Token liefert 400.

## Noch offen in K03

- JwtBearer-/OpenIdConnect-Handler mit real signierten Tokens und echter Eventausführung prüfen; der echte Tokenvertrag wurde gegen Keycloak geprüft, aber noch nicht end-to-end durch beide ASP.NET-Handler geschickt.
- Sitzungsgebundenen Token-/Refreshpfad für langlebige Blazor-Circuits implementieren und mit parallelen Sitzungen prüfen.
- Bestehende OIDC-, API-, Studio-, SignalR-/gRPC-Regressionen nach den letzten Änderungen vollständig ausführen.

Aus dem bestandenen K02-Tokennachweis folgt noch keine vollständige Studio-Login-, Session- oder Produktionsfreigabe.
