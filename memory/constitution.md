
# VertexBPMN Constitution

## Core Principles

### I. Funktionale Exzellenz & API-Parität
VertexBPMN implementiert BPMN 2.0 und DMN 1.4 vollständig, inklusive aller Events, Gateways, Tasks, Subprozesse, Multi-Instanzen und Kompensationen. Die REST-API ist OpenAPI-dokumentiert und Camunda-kompatibel. Alle Kernendpunkte (Repository, Runtime, Task, History, Decision, Identity) sind abgedeckt und werden laufend erweitert.

### II. Test-First & Conformance
Testabdeckung ist Pflicht: Unit- und Integrationstests für alle Engine-Features, API-Endpunkte und Edge-Cases. MIWG- und DMN-TCK-Konformität werden in CI geprüft. Red-Green-Refactor-Zyklus ist verbindlich.

### III. Cloud-Native Architektur
Die Engine ist stateless, container-ready und für Kubernetes optimiert. Health-, Liveness- und Readiness-Probes, Prometheus/OpenTelemetry-Metriken, Dockerfile und Kubernetes-Deployment sind Standard.

### IV. Observability & Monitoring
Health-Checks, strukturierte Logs, Metriken und Tracing sind integriert. Monitoring erfolgt via Prometheus, Grafana und OpenTelemetry. Alerts und Dashboards sind dokumentiert.

### V. Innovation & Differenzierung
VertexBPMN bietet Live-Inspector, Visual Debugger, Predictive Analytics, Process Mining Hooks, Feature Flags und eine Plug-in-Architektur. Die API ist für KI- und ML-Integrationen vorbereitet.

### VI. Security & Compliance
OAuth2/OpenID Connect, RBAC, Mandantenfähigkeit und Audit-Logs sind Pflicht für produktiven Einsatz. HTTPS, Rate Limiting und CORS sind zu erzwingen.

## Technology & Deployment Standards

- .NET 9, C# 13, EF Core 9, Dapper, OpenTelemetry, Prometheus, Docker, NuGet
- API-First: OpenAPI/Swagger, SDKs für C#, JS, Python, gRPC
- Persistenz: SQLite (Demo), PostgreSQL/SQL Server (Produktiv)
- Skalierung: Horizontale Skalierung, BackgroundServices, JobExecutor
- Backup & Recovery: Regelmäßige Backups bei Persistenz

## Development Workflow & Quality Gates

- Alle Features werden als eigenständige, testbare Module entwickelt.
- Code-Reviews prüfen Verfassungskonformität, Testabdeckung und API-Parität.
- Feature- und Release-Branches folgen klaren Spezifikations- und Task-Templates.
- Dokumentation, Benchmarks und Contract-Tests sind Teil jedes Releases.
- Innovationen werden als Plug-ins oder experimentelle Features integriert.

## Governance

- Die Constitution ist das oberste Regelwerk. Änderungen erfordern Dokumentation, Review und Migrationsplan.
- Alle PRs und Reviews müssen die Einhaltung der Verfassung bestätigen.
- Komplexität muss begründet und dokumentiert werden.
- Die Olympiasieger-Checkliste dient als Validierungs- und Zielkatalog für höchste Qualität und Innovationsgrad.

**Version**: 1.0.0 | **Ratified**: 2025-09-11 | **Last Amended**: 2025-09-11