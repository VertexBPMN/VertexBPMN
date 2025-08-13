# VertexBPMN: Hinweise für produktiven Betrieb

## Authentifizierung & Sicherheit
- Aktuell ist die API offen. Für produktiven Einsatz empfiehlt sich:
  - Authentifizierung (z.B. JWT, OAuth2, API-Key)
  - HTTPS erzwingen
  - Rate Limiting und CORS-Konfiguration

## Deployment
- Empfohlen: Containerisierung (Dockerfile bereitstellen)
- Health-Check-Endpoint (`/api/health`) für Load-Balancer und Monitoring nutzen
- Logging-Ausgabe an zentrale Systeme (z.B. ELK, Azure Monitor, CloudWatch)

## Skalierung
- Die Engine ist zustandslos (in-memory) und kann horizontal skaliert werden, sobald Persistenz verfügbar ist
- Für produktive Workflows: Persistenz-Provider (z.B. EF Core/PostgreSQL) aktivieren, sobald .NET 9/EF Core kompatibel

## Monitoring & Observability
- Health-Check, strukturierte Logs und Metriken sind integriert
- Erweiterbar mit OpenTelemetry, Prometheus, Application Insights

## Backup & Recovery
- Bei Nutzung von Persistenz: Regelmäßige Backups der Datenbank

---
*Letztes Update: August 2025*
