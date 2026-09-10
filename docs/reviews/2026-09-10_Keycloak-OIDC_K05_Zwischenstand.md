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
- Reale Keycloak-/Browser-Suite: 2/2 Tests bestanden
- Frische API-/Studio-Logs enthalten nach dem grünen Lauf keine ungefangenen Fehler, HTTP-500-Antworten oder fehlenden Static Assets
- Der Testhost verwendet keinen Development-API-Key und fällt nicht auf Testauth zurück

## Gefundener und korrigierter Produktionsfehler

Die Tenant-API gab Nicht-Administratoren bislang die vollständige Tenant-Liste zurück und prüfte bei direktem Abruf keine Fremdtenant-Grenze. Beide Pfade sind jetzt claimgebunden.

Zusätzlich führte ein autorisierter Repository-Deploy mit leerem BPMN-XML zu einer ungefangenen `ArgumentException` und HTTP 500. Der Controller validiert nun `BpmnXml` und `Name` vor dem Serviceaufruf und antwortet mit HTTP 400.

## Noch offene K05-Nachweise

K05 ist noch nicht abgeschlossen:

- T02: rollenabhängige UI-Sichtbarkeit und Bedienbarkeit zusätzlich zur bestandenen API-Matrix
- T03: falscher Issuer/Audience sowie manipulierte, unsignierte und abgelaufene Tokens gegen den real gestarteten Host
- T04: parallele API-Aufrufe während des Refreshs
- T05: zwei reale parallele Browsersitzungen mit verschiedenen Tenants
- T06: Rollenentzug und Kontosperre bei offener Sitzung
- T07: vollständiger MFA-Ablauf einschließlich negativer zweiter Faktoren
- T08: Signing-Key-Rotation und JWKS-Cache
- T09: Keycloak-Unterbrechung und Wiederanlauf
- T10: Proxy-/HTTPS-/Forwarded-Header- und Callback-Manipulation
- T11: Replikat-/Neustartverhalten des Sessionstores
- T12: alternativer realer OIDC-Issuer
- Regression der übrigen Authprofile sowie SDK-/gRPC-/SignalR-Zugriffe
- finale Wiederholung aus sauberem Checkout und unabhängiges Review

