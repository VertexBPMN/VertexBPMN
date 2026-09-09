# Phase 3 – Technische Restpunkte und externe Abnahme

Stand: 2026-09-09. Basis: `a9829e8`; die hier beschriebenen weiteren Änderungen sind noch nicht committet.

## Ergebnis und Grenzen

Die fünf dokumentierten technischen Arbeitspakete wurden implementiert und mit lokalen Regressionen geprüft. **Die gesamte Phase 3 ist nicht freigegeben:** Die Abnahme gegen den gewählten echten IdP und das unabhängige Sicherheitsreview fehlen weiterhin. Die Wahl des IdP, Authority/Audience, Rollen-/Tenant-Mapping sowie Testzugang und Reviewer wurden angefragt; eine Festlegung liegt diesem Lauf nicht vor.

Ein erfolgreicher lokaler Testlauf ist keine Aussage über vollständige BPMN-/DMN-Konformität oder allgemeine Produktionsreife. Die Adapterprüfung belegt die unten genannten Sicherheitsgrenzen, nicht jede beliebige Operation aller Controller.

## Erledigte technische Punkte

| Punkt | Implementierung | Ausgeführter Nachweis |
|---|---|---|
| M1/M5 Connector und Rebinding | `IConnectorNetworkTransport` isoliert DNS/Socket-IO; der produktive `SocketsHttpHandler` validiert alle aufgelösten IPs und verbindet ausschließlich zu diesen IPs. Keine automatischen Redirects, Umgebungs-Proxies oder geteilten Cookies; Connect-Timeout 10 Sekunden. Gemappte IPv4-/private IPv6-Ziele werden blockiert. | `ConnectorRedirectSsrfTests`: produktive DI-Registrierung für Connector **und** benannten OAuth2-Client; echtes HTTP über kontrolliertes lokales TCP-Routing, öffentliche DNS-Antwort erfolgreich, anschließende private Antwort ohne weiteren Socket, Redirect nicht verfolgt. Netzwerksimulation wird nicht als Internet-/DNS-Server-Abnahme ausgegeben. |
| M2 OAuth2-Transport | Beide Token-POSTs nutzen `VertexBPMN.PublicEndpoints` und damit denselben abgesicherten Handler. Token-URLs müssen HTTPS verwenden. | Gemeinsame Transporttests sowie OAuth2-Negativtests. |
| M6 OAuth2-Bindung | Authentifizierte Identität + Hash eines zufälligen Browser-Proofs werden pro State persistent gespeichert. Studio hält den Proof im `sessionStorage` des startenden Tabs. Callback ausschließlich als authentifiziertes Admin-POST; atomarer State-Verbrauch vor Token-Austausch. Abgelaufene Bindungen werden bereinigt. | `OAuth2CredentialFlowServiceTests`: fremde Identität, falscher/fehlender Proof, einmaliger Verbrauch. Zwei getrennte SQLite-Kontexte werden vor dem Verbrauch synchronisiert: nur ein Token-POST gewinnt. `OAuth2FlowApiTests` und `HttpCredentialServiceTests` prüfen den HTTP-Vertrag. Browser-Test prüft fehlenden Tab-Proof, keinen API-Aufruf, URL-Bereinigung und Cache-/Referrer-Schutz. |
| M4 Webhook-Replay | Signiertes Zeitfenster, versionierter Signaturinhalt und Delivery-ID. Persistente Reservierung in `RuntimeInbox`, Unique-Constraint auf TenantScope/Operation/IdempotencyKey; Operation enthält Trigger-ID. | `WorkflowTriggerApiTests`: korrekter HMAC und Trigger-Secret, falsche Signatur, manipulierte Delivery-ID, alter Timestamp, Wiederholung und zwei parallele Zustellungen; nur einmal HTTP 201, Duplikat HTTP 409. |
| M3 Modell-Export | `ModelExportJsonPolicy` redigiert XML-Eigenschaften erst bei API-Serialisierung. `ModelExportRedaction` erhält Credential-/Secret-Referenzen, maskiert definierte sensible Attribute/Parameter und Zugangsdaten in URLs. Originale bleiben unverändert gespeichert. Exporte mit Redaktionsmarkern sind nicht deploybar. | `ModelExportSecurityTests`: Deploy-Antwort, Repository-Liste/Detail und BPMN-Roh-XML enthalten das Original-Secret nicht; direkte Repository-Abfrage enthält weiterhin das unveränderte Original. Wieder-Deployment des redigierten BPMN wird abgewiesen. |
| Req 1/4 Adapter | SDK kann Tenant-Query nicht zur Rechteausweitung verwenden; gRPC/MCP-Mutationen benötigen ProcessManager/Admin, fehlende Tenant-Claims werden nicht mehr auf `default` gesetzt. SignalR prüft Instanz-/Tenant-/Nutzerbesitz vor Gruppenbeitritt. Workergruppen und Broadcast-Methoden sind Admin-only. Empfängerbezogene Benachrichtigungen werden nicht mehr global versandt. | `VertexBpmnClientTests`, `GrpcContractTests`, `SignalRSecurityTests`: echte SDK-/gRPC-/SignalR-Clients gegen den lokalen API-Host, positive eigene Zugriffe und negative fremde IDs/Tenants/Rollen. |

## Notwendige Umstellung: Webhook-Sender

Für `GET/POST/PUT/PATCH/DELETE /api/webhooks/{path}` werden künftig diese Header benötigt:

- `X-VertexBPMN-Timestamp`: UTC Unix-Sekunden als Dezimalzahl. Maximal 300 Sekunden alt, maximal 30 Sekunden in der Zukunft.
- `X-VertexBPMN-Delivery-Id`: pro fachlicher Zustellung stabil, 1–128 ASCII-Buchstaben/Ziffern/`-`/`_`. Bei Transport-Retry dieselbe ID verwenden.
- `X-VertexBPMN-Signature`: `sha256=` gefolgt von Hex-HMAC-SHA256 über folgende Bytes:

```text
UTF8("v1\n" + UPPERCASE(method) + "\n" + registeredPath + "\n" + timestamp + "\n" + deliveryId + "\n") || rawBodyBytes
```

`registeredPath` ist der registrierte normalisierte Webhook-Pfad **ohne** `/api/webhooks`, z. B. `/orders`, nicht `/api/webhooks/orders`. Body-Bytes nach dem Signieren nicht verändern. Der Schlüssel ist das referenzierte Credential-Secret (`hmac-sha256`) oder das Trigger-Secret (`trigger-secret`). Im zweiten Fall bleibt außerdem `X-VertexBPMN-Trigger-Secret` erforderlich.

Alte Body-only-Signaturen und unsignierte Trigger-Secret-Webhooks werden mit 401 abgewiesen. Sender vor dem Rollout aktualisieren. `/api/triggers/{id}/invoke` ist ein separater authentifizierter-by-secret RPC-Vertrag und wurde nicht stillschweigend auf dieses Webhook-Protokoll umgestellt.

Eine bereits reservierte Delivery-ID ergibt 409, auch wenn die API nach der Reservierung abstürzt. Dies ist bewusst fail-closed; die Reservierung allein garantiert **keine** erfolgreiche fachliche Verarbeitung. Solche Fälle über Inbox/Runtime prüfen, bevor eine neue Delivery-ID verwendet wird. Atomare Reservation-plus-Geschäftsverarbeitung/Wiederanlauf gehört zur Ausfallabnahme in Phase 4. Reservierungen werden nicht automatisch gelöscht, um Replay nach Neustarts zu verhindern; ein kontrolliertes Aufbewahrungskonzept muss mit Sender-Retryfristen abgestimmt werden.

## Notwendige Umstellung: OAuth2

1. Beim Provider die Studio-URL `<Studio-Basisadresse>/oauth2/callback` registrieren. Die Credentials-Seite zeigt diese Adresse an.
2. Der Flow navigiert im **selben Tab** zum Provider. Nicht auf einen anderen Browser/Tab wechseln; der Proof wird nicht in die Provider-URL übertragen.
3. Rückkehr erfolgt zur Studio-Seite. Diese übermittelt `state`, `code`, `browserProof` per authentifiziertem `POST /api/oauth2/callback`. Die alte anonyme API-GET-Callback-Route ist nicht mehr verfügbar.
4. Nicht-Studio-Clients müssen beim autorisierten Start einen eigenen kryptografisch zufälligen Browser-Proof (mindestens 32 Zufallsbytes, Base64-kodiert) übergeben und den Callback unter derselben Identität mit diesem Proof abschließen.
5. Bei Token-Endpunktfehler nach State-Verbrauch muss die Autorisierung neu gestartet werden. Derselbe State wird nicht erneut genutzt.

Provider-Authorization- und Token-URLs verwenden HTTPS. Private/Loopback-Token-Ziele, Redirects und Proxy-Umgehungen sind nicht freigegeben. Ein lokal betriebener IdP benötigt deshalb eine ausdrücklich überprüfte Produktions-/Testtopologie; ein SSRF-Bypass wurde dafür nicht eingeführt. Browser-Storage-Proof schützt nicht gegen JavaScript/XSS mit Rechten auf derselben Studio-Origin; CSP/XSS-Schutz bleibt erforderlich.

## Export-Policy und Grenzen

Maskierte strukturierte Namen: `password`, `passwd`, `secret`, `client_secret`, `api_key`, `access_token`, `refresh_token`, `authorization`, `connectionString` (Groß-/Kleinschreibung sowie `_`/`-` normalisiert). `credentialRef` und `secretRef` bleiben erhalten. XML-Felder `bpmnXml`, `bpmn20Xml`, `dmnXml`, `cmmnXml` werden an der MVC-JSON-Ausgabe behandelt, einschließlich anonymer DTOs für Inspector/Debugger. Der vorhandene API-Auditlogger zeichnet Methode/Pfad statt Request-Bodies auf.

Geänderte Exporte tragen `[VERTEX-REDACTED]` und `vertex:redacted=true`; BPMN-Prozesse werden `isExecutable=false`. Vor Deployment Platzhalter durch gültige Credential-Referenzen ersetzen. Modelle ohne strukturierte Secrets bleiben byte-identisch. Die Policy ist **kein universeller Secret-Scanner**: opaque Skripte, freie Texte und beliebig verschachtelte JSON-Strings können nicht zuverlässig automatisch als Geheimnisse erkannt werden. Keine Secrets in diese Modellteile einbetten; den Credential-Store verwenden. Das gespeicherte Original ist weiter nur über berechtigte interne Repository-/Runtime-Zugriffe verfügbar.

## Testergebnisse

- Zwischenstand Core mit WSLC: **924/924 bestanden, 0 Fehler, 0 Skips**, 159,552 s (`TestResults/phase3-wslc-full.xml`). Enthält reale PostgreSQL-Migrationen und RabbitMQ-Prüfungen, aber liegt vor der letzten SignalR-/SDK-Erweiterung.
- Adapter-Nachprüfung: **7/7 bestanden**, keine Skips (`TestResults/phase3-adapters.xml`).
- Browser-Nachprüfung: **9/9 bestanden**, keine Skips (`TestResults/phase3-browser-final.xml`); ein erster fehlerhafter Header-Stringvergleich wurde auf die tatsächliche `no-store`-Direktive korrigiert. Keine Sicherheitsprüfung entfernt.
- Finaler Core-Lauf mit realem PostgreSQL und RabbitMQ über WSLC: **927/927 bestanden, 0 Fehler, 0 Skips**, 166,232 s (`TestResults/phase3-wslc-final.xml`). Enthält die letzten SDK-/SignalR-Erweiterungen.
- Finale lokale Browser-Nachprüfung nach Studio-Logging-Härtung: **9/9 bestanden, 0 Fehler, 0 Skips**, 27,791 s (`TestResults/phase3-browser-qualified.xml`), einschließlich fehlendem Browser-Proof und Abwesenheit des Callback-Codes im Studio-Log.
- Release-Builds erfolgreich: Core 22 Warnungen, UI-Testprojekt 2 Warnungen; keine Warnungsfreiheit behauptet. `git diff --check` ohne Fehler. Geprüft wurde der uncommittete Arbeitsstand auf Basis `a9829e8`, kein sauberer Release-Checkout. TestReports sind lokale Artefakte, keine eingecheckte Zertifizierung. Keine CI-Workflows geändert.

## Noch erforderlich für den formalen Phasenabschluss

- [ ] Echter IdP: Anbieter, erreichbare Testumgebung, Authority/Audience, Rollen-/Tenant-Claims und Callback-Registrierung festlegen; Login/Logout, Token-Ablauf und Rollenentzug mit realen Konten prüfen. Keine Passwörter/Tokens in Issues oder diesen Bericht schreiben.
- [ ] Unabhängiger Security-Reviewer: finalen Commit, reproduzierbare Testberichte und Findings übergeben; Review und Freigabe dokumentieren. Diese Eigenprüfung ersetzt ihn nicht.

Bis diese beiden externen Punkte und die im Hauptplan weiterhin offenen vollständigen Matrix-/Zielumgebungsabnahmen erledigt sind, bleibt Phase 3 insgesamt offen. Die gezielten Adaptertests prüfen konkrete positive und negative Pfade, nicht jede Operation jeder öffentlichen Schnittstelle. Ebenso ist die vollständige Produktions-Telemetrie nicht allein durch den Studio-Logtest qualifiziert.
