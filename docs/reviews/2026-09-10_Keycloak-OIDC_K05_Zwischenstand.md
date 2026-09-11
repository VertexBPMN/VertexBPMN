# K05 Sicherheitsabnahme – Zwischenstand 5

Stand: 2026-09-11
Basis-Commit: `8d82fdb`
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
- Relevante Auth-/Tenant-/SDK-/gRPC-/SignalR-Regression: 84/84 Tests bestanden, 0 Fehler und 0 übersprungen; erneuter Release-Build mit 0 Warnungen und 0 Fehlern
- Vollständige serielle Haupttestsuite: 988 Tests, 0 Fehler, 25 erwartungsgemäß übersprungene externe Infrastrukturtests, Laufzeit 147,475 Sekunden
- Reale Keycloak-/Browser-Suite nach dem achten K05-Paket: 12/12 Tests bestanden
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
- Ein temporärer echter RSA-Signing-Key-Provider wird über die Keycloak-Admin-API erzeugt; Keycloak stellt anschließend ein Access Token mit einem neuen `kid` aus und veröffentlicht alten und neuen Schlüssel gleichzeitig im JWKS.
- Die API akzeptiert bereits den ersten Request mit dem neuen gültigen `kid` durch einen kontrollierten, rate-limitierten und blockierenden Metadatenrefresh; das alte Übergangstoken bleibt im Überlappungsfenster gültig, während eine manipulierte Signatur weiterhin 401 erhält.
- Nach Entfernen des temporären Providers verschwindet dessen `kid` aus dem JWKS, Keycloak verwendet wieder den ursprünglichen Schlüssel und die API akzeptiert das neue Rollback-Token. Der Test hinterlässt keinen Schlüsselprovider.
- Bei einem kontrollierten Stopp des echten Keycloak-Containers bleibt ein bereits validiertes, noch gültiges Access Token aufgrund der gecachten Signaturschlüssel nutzbar; anonyme API-Aufrufe bleiben mit 401 geschlossen.
- Während des IdP-Ausfalls schlagen neue Tokenausstellung und Browseranmeldung begrenzt fehl. Das Studio zeigt seine anonyme Fehlerseite, statt den geschützten Error-Handler erneut zum IdP umzuleiten. Es entsteht weder ein Studio-Cookie noch Dashboard-Zugriff; es gibt keinen API-Key-/Testauth-Fallback und keine endlose Redirectschleife.
- Nach dem echten Keycloak-Wiederanlauf funktionieren Discovery, neue Tokenausstellung, API-Zugriff und Browseranmeldung erneut. Der vollständige Lauf bestand 12/12 Tests in 370,973 Sekunden; beide dedizierten WSLC-Container wurden anschließend entfernt.
- Nach der Error-Handler-Korrektur bestand T09 fokussiert erneut 1/1 Tests in 167,294 Sekunden. Die bestehende mobile Seiten-/Error-Page-Vertragsprüfung bestand zusätzlich 20/20 Fälle; die frischen Studio-Logs enthalten weder einen rekursiv fehlgeschlagenen Error-Handler noch einen Kestrel-Verbindungsabbruch.
- T10 bestand fokussiert 1/1 gegen den echten Keycloak-PAR-Endpunkt: Nur ein explizit bekannter Proxy darf `X-Forwarded-For`/`X-Forwarded-Proto` liefern; `X-Forwarded-Host` wird nicht verarbeitet. Keycloak akzeptiert die registrierte lokale HTTPS-Callback-Origin, ein fremder Host wird mit 400 abgewiesen und eine fremde Callback-URI wird nicht umgeleitet.
- Eine schemerelative Login-Return-URL wie `//attacker.example/` wird auf `/` normalisiert. Der reale Login endet im VertexBPMN-Dashboard derselben Studio-Origin. Die Keycloak-Template-Verträge bestanden nach Ergänzung der exakten lokalen HTTPS-URI 4/4.
- T11 bestand fokussiert 1/1: Zwei reale Studio-Prozesse verwenden denselben expliziten Data-Protection-Keyring. Das bestehende Authentifizierungsticket funktioniert auf der zweiten Replik und nach deren Prozessneustart, ohne einen verdeckten Browser-Redirect zum IdP. Production und Stage starten künftig ohne konfigurierten dauerhaften `DataProtection:KeyRingPath` nicht mehr.
- T12 bestand fokussiert 1/1: Ein unabhängiger lokaler Standard-OIDC-Issuer veröffentlicht eigene Discovery-/JWKS-Dokumente und signiert den providerneutralen Claim-Vertrag. Eine zweite echte API akzeptiert sein Token nach Konfigurationswechsel; die weiterhin auf Keycloak konfigurierte Haupt-API weist dasselbe Token mit 401 ab.
- Der echte Studio-Start bestätigt die neuen Konfigurationsgrenzen zusätzlich ohne Browser: Production ohne `DataProtection:KeyRingPath` und aktivierter Reverse-Proxy-Betrieb ohne mindestens eine explizite `ReverseProxy:KnownProxies`-Adresse brechen jeweils vor dem Start mit einer eindeutigen `InvalidOperationException` ab.
- Ein erneuter kombinierter T01–T12-Lauf am 2026-09-11 ist nicht als Produktergebnis wertbar: Die Codex-Sandbox verweigerte bereits beim Start des mitgelieferten Playwright-Chromiums die Prozesserzeugung mit `spawn EPERM`. Der Versuch wurde beendet und die beiden dedizierten WSLC-Container wurden entfernt. Die zuvor bestandenen realen Einzel- und Teilsuiten bleiben davon unberührt; der kombinierte Lauf muss mit lokaler Browserprozess-Berechtigung wiederholt werden.

## Gefundener und korrigierter Produktionsfehler

Die Tenant-API gab Nicht-Administratoren bislang die vollständige Tenant-Liste zurück und prüfte bei direktem Abruf keine Fremdtenant-Grenze. Beide Pfade sind jetzt claimgebunden.

Zusätzlich führte ein autorisierter Repository-Deploy mit leerem BPMN-XML zu einer ungefangenen `ArgumentException` und HTTP 500. Der Controller validiert nun `BpmnXml` und `Name` vor dem Serviceaufruf und antwortet mit HTTP 400.

IdentityModel 8 aktualisierte bei einem unbekannten Signing-Key standardmäßig nur im Hintergrund. Dadurch wurde der auslösende erste Request nach einer ordnungsgemäßen Keycloak-Rotation mit 401 abgewiesen, obwohl der neue Schlüssel bereits im JWKS veröffentlicht war. Die API aktiviert nun standardmäßig den von IdentityModel vorgesehenen blockierenden Refreshpfad; `Jwt:MetadataRefreshIntervalSeconds` begrenzt dessen Frequenz, und `Jwt:BlockOnMetadataRefresh` erlaubt Betreibern ein bewusstes Opt-out. Das lokale OIDC-Profil verwendet ein einsekündiges Intervall für den deterministischen Rotationstest.

WSLC verlor während wiederholter Abnahmeläufe vereinzelt die veröffentlichte Host-Port-Bindung, obwohl der Container intern weiterlief. Der lokale Testhost erkennt diesen Zustand mit begrenzten Rebind-Versuchen und prüft Discovery nochmals vor Studio- und Teststart. Diese Härtung gilt nur für den lokalen WSLC-Abnahmepfad.

Nach einem echten Keycloak-Neustart konnten kurzlebige Master-Realm-Admin-Tokens aus einer vorherigen Verbindung noch transportbedingt fehlschlagen. Der Testhost beschafft und validiert deshalb für jede zeitlich getrennte administrative Mutation einen frischen Admin-Client und wiederholt ausschließlich idempotente Readiness-/GET-Operationen bei transienten Transportfehlern. Fachliche Mutationen und ihre Erwartungen werden nicht wiederholt oder abgeschwächt.

Der produktive Exception-Handler verwies auf die global geschützte Route `/Error`. Bei nicht erreichbarem IdP löste diese Route selbst erneut einen OIDC-Challenge aus, sodass auch die Fehlerbehandlung mit einer ungefangenen Verbindungsexception endete. Nur die statische Fehlerseite ist nun explizit anonym; T09 verlangt deren sichtbare Ausgabe, während alle fachlichen Studio-Routen weiterhin autorisiert bleiben.

Das Studio hatte keinen expliziten Reverse-Proxy-Vertrag und akzeptierte bei der Login-Return-URL jeden mit `/` beginnenden Wert, einschließlich schemerelativer externer Ziele. Reverse-Proxy-Verarbeitung ist nun opt-in, verlangt mindestens eine syntaktisch gültige konkrete Proxy-IP, verarbeitet höchstens einen symmetrischen `X-Forwarded-For`-/`X-Forwarded-Proto`-Satz und übernimmt niemals `X-Forwarded-Host`. Hostfilterung und exakte Keycloak-Redirect-URIs bilden zusätzliche Grenzen. Die Konfiguration zum Vertrauen beliebiger Proxies wird nicht angeboten.

Das Studio verließ sich zuvor auf den impliziten Data-Protection-Keyring des Hostprozesses. Für Production und Stage ist jetzt ein expliziter dauerhafter `DataProtection:KeyRingPath` Pflicht; alle Replikate verwenden den gemeinsamen Application-Discriminator `VertexBPMN.Studio`. Der lokale T11-Lauf verwendet einen isolierten Keyring im Ergebnisverzeichnis und entfernt seine Prozesse kontrolliert.

## Noch offene K05-Nachweise

K05 ist noch nicht abgeschlossen:

- kombinierter lokaler T01–T12-Lauf mit 15/15 bestandenen Pflichtfällen und echter Browserprozess-Berechtigung

Die finale Wiederholung aus sauberem Checkout und das unabhängige Review sind anschließend Bestandteil von K06 beziehungsweise der Releaseabnahme, nicht Ersatz für den noch offenen kombinierten K05-Lauf.
