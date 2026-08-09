# VertexBPMN: Hinweise für produktiven Betrieb

## Authentifizierung & Sicherheit
- Außerhalb des Testbetriebs erzwingt die API eine konfigurierte JWT- oder API-Key-Authentifizierung.
- HTTPS-Redirect, CORS und globales Rate Limiting sind für Production und Stage aktiviert.
- Produktive JWT-Audience sowie Authority oder Secret Key müssen über Konfiguration beziehungsweise Secret Management gesetzt werden.
- Plugin-Management und MCP/gRPC-Endpunkte sind authentifizierungspflichtig; mutierende Plugin-Operationen erfordern die Rolle `Admin`.

## Deployment
- Empfohlen: Containerisierung (Dockerfile bereitstellen)
- Health-Check-Endpoint (`/api/health`) für Load-Balancer und Monitoring nutzen
- Logging-Ausgabe an zentrale Systeme (z.B. ELK, Azure Monitor, CloudWatch)

## Skalierung
- Die API unterstützt EF-Core-Persistenz für SQLite, PostgreSQL und SQL Server sowie InMemory für Tests.
- Relationale Datenbanken werden beim Start über versionierte EF-Migrationen aktualisiert.
- Für horizontale Skalierung müssen alle Instanzen denselben relationalen Speicher und eine geeignete externe Zustellung für Live-Ereignisse verwenden.

## Monitoring & Observability
- Health-Check, strukturierte Logs und Metriken sind integriert
- Erweiterbar mit OpenTelemetry, Prometheus, Application Insights

## Backup & Recovery
- Regelmäßige Backups der produktiven Datenbanken einrichten und Wiederherstellungen testen.
- Migrationen vor dem Rollout prüfen und Snapshots beziehungsweise Rollback-Verfahren für die jeweilige Betriebsumgebung dokumentieren.
- Webhook-Ziele müssen HTTPS verwenden; Zustellungen sind HMAC-SHA256-signiert und nach Ereignistyp filterbar.

---
*Letztes Update: 2026-03*
