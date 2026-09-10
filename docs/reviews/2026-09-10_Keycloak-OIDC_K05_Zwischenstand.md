# K05 Sicherheitsabnahme – Zwischenstand 1

Stand: 2026-09-10  
Basis-Commit: `ca5699e`  
Branch: `codex/keycloak-k05-security-acceptance`  
Modus: lokaler Opt-in-Lauf mit WSLC; keine Aufnahme in GitHub CI

## Ausgeführte Infrastruktur

- Keycloak `26.7.3`
- PostgreSQL `17-alpine`
- VertexBPMN API mit realer JWT-Bearer-Validierung
- VertexBPMN Studio aus einem lokalen Release-Publish
- Ephemere, kryptografisch zufällige Prozess-Secrets; keine Werte protokolliert oder gespeichert
- Dedizierte Container, Netzwerk und Datenbank-Volume nach dem Lauf entfernt

## Bestandene Nachweise

- Reale Browseranmeldung über Keycloak mit Authorization Code und PKCE
- API-Zugriff über das reale Access Token
- Sitzungsgebundener Token-Refresh und IdP-Logout im bestehenden Browserfall
- Reale Keycloak-Access-Tokens für Admin, ProcessManager und ReadOnly
- Fehlendes Token, fehlender Tenant-Claim und ID Token als API-Zugang werden mit 401 abgewiesen
- ReadOnly darf lesen, aber keine ProcessManager-Mutation ausführen
- ProcessManager darf nur auf den eigenen Tenant zugreifen
- Admin darf tenantübergreifende Verwaltungsoperationen ausführen
- Tenant-Auflistung ist für Nicht-Administratoren auf den Claim-Tenant beschränkt
- Fokussierte zentrale API-Regression: 10/10 Tests bestanden
- Reale Keycloak-/Browser-Suite nach dem sechsten K05-Paket: 10/10 Tests bestanden
- Zwei getrennte reale Browsersitzungen belegen, dass ReadOnly keine Tenant-Admin-Aktionen sieht und Admin die Verwaltungsoberfläche erhält
- Manipulierte, unsignierte, syntaktisch ungültige und tatsächlich abgelaufene Tokens werden von der realen API mit 401 abgewiesen
- Von Keycloak signierte Tokens aus dem richtigen Realm ohne Ziel-Audience sowie aus dem falschen Realm werden mit 401 abgewiesen; die Test-Payloads werden vor dem Request auf `iss` und `aud` geprüft
- Frische API-/Studio-Logs enthalten nach dem grünen Lauf keine ungefangenen Fehler, HTTP-500-Antworten oder fehlenden Static Assets
- Der Testhost verwendet keinen Development-API-Key und fällt nicht auf Testauth zurück
- Ein offener Blazor-Circuit überlebt den Access-Token-Ablauf; sechs parallele, antiforgery-geschützte Session-Refresh-Anfragen werden kontrolliert erneuert und der anschließende API-Aufruf bleibt funktionsfähig
- Zwei gleichzeitig aktive, vollständig getrennte Browserkontexte für unterschiedliche Tenant-Benutzer behalten vor und nach paralleler Navigation ausschließlich den jeweiligen Tenant-Kontext; es gibt keinen Cookie-, Claims- oder HttpClient-Handler-Leak
- Nach Entzug der ProcessManager-Rolle übernimmt der nächste echte Token-Refresh die reduzierten Claims; der weiterhin offene Blazor-Circuit erhält auf rollenpflichtigen API-Pfaden HTTP 403
- Nach Sperrung eines Kontos verwirft ein fehlgeschlagener Refresh den serverseitigen Tokenzustand und das Studio-Cookie; der Passwortgrant und die erneute Studio-Anmeldung bleiben gesperrt
- Lokaler Cookie-/OIDC-Logout beendet die Studio-Sitzung und führt zum realen Keycloak-Login zurück
- Ein dediziertes MFA-Konto wird bei jedem isolierten Lauf ohne vorhandenes OTP neu eingeschrieben; ohne zweiten Faktor entsteht kein Studio-Cookie und eine alternative lokale Studio-Route bleibt im Keycloak-Flow blockiert
- Falsche TOTP-Codes werden sowohl bei der Einschreibung als auch beim Folge-Login abgewiesen; erst der korrekte aktuelle Code öffnet Dashboard und API-Zugriff, und nach Logout verlangt der nächste Login erneut TOTP

## Gefundener und korrigierter Produktionsfehler

Die Tenant-API gab Nicht-Administratoren bislang die vollständige Tenant-Liste zurück und prüfte bei direktem Abruf keine Fremdtenant-Grenze. Beide Pfade sind jetzt claimgebunden.

Zusätzlich führte ein autorisierter Repository-Deploy mit leerem BPMN-XML zu einer ungefangenen `ArgumentException` und HTTP 500. Der Controller validiert nun `BpmnXml` und `Name` vor dem Serviceaufruf und antwortet mit HTTP 400.

## Noch offene K05-Nachweise

K05 ist noch nicht abgeschlossen:

- T08: Signing-Key-Rotation und JWKS-Cache
- T09: Keycloak-Unterbrechung und Wiederanlauf
- T10: Proxy-/HTTPS-/Forwarded-Header- und Callback-Manipulation
- T11: Replikat-/Neustartverhalten des Sessionstores
- T12: alternativer realer OIDC-Issuer
- Regression der übrigen Authprofile sowie SDK-/gRPC-/SignalR-Zugriffe
- finale Wiederholung aus sauberem Checkout und unabhängiges Review
