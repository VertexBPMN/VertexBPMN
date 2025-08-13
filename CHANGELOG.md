# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased]
## [1.0.0] - 2025-08-13

### Added
- Persistente Process Mining Event-Analytics mit PostgreSQL und EF Core
- REST-API für Event-Export, Statistiken, Zeitreihen, Mandantenfilter und Prozessmetriken
- Datenbank-Indexe für EventType, ProcessInstanceId, TenantId, Timestamp
- Rollenbasierte Authentifizierung für alle Analytics- und Reporting-Endpunkte
- OpenAPI/Swagger-Dokumentation für alle neuen Endpunkte
- Integrationstests für Analytics- und Reporting-APIs
- Erweiterte README mit Security- und Analytics-Beispielen

### Changed
- Analytics-Endpunkte nutzen jetzt persistente Datenbank statt In-Memory-Events

### Security
- Zugriffsschutz für alle Analytics- und Reporting-Endpunkte

### Added
- Erweiterte Testabdeckung für BPMN- und DMN-Edge-Cases (verschachtelte Subprozesse, Boundary Events, komplexe DMN-Inputs)
- Zusätzliche Unit-Tests für Fehlerfälle und Modellvalidierung
- Parser-Fix für Event-Subprozesse (korrekte IsEventSubprocess-Erkennung)
- Dokumentation und Teststrategie aktualisiert
- Vollständige OpenAPI/Swagger-Spezifikation (`openapi.json`) für alle REST-Endpunkte generiert
- Nahtlose Integration mit bpmn-js, dmn-js und form-js (bpmn.io)
- Neue Endpunkte: GET/PUT /camunda/process-definition/{id}/xml, /camunda/decision-definition/{key}/xml, /camunda/task/{id}/form-schema
- Automatische OpenAPI-Doku und neue Entwicklerdokumentation (`docs/openapi.md`)
- Hinweise zur API-Dokumentation und Contract-Tests in README und CONTRIBUTING.md ergänzt
- Cloud-Native-Readiness: Health-/Liveness-/Readiness-Probes, Docker/Kubernetes-Deployment
- Prometheus/OpenTelemetry-Metriken via /api/metrics und /api/metrics/prometheus
- Live-Inspector-API für laufende Prozessinstanzen (/api/inspector/process-instance/{id}/state)
- Neue Entwicklerdoku: docs/cloud-native.md
- Innovationen: Live-Inspector-API, Feature-Flag-Architektur, API-Hooks für Analytics/Process Mining
- Neue Entwicklerdoku: docs/features-innovation.md
- Process Mining & Analytics Hooks: Event-Log-/Token-Log-API-Design, Predictive Analytics Flag
- Neue Entwicklerdoku: docs/process-mining-hooks.md

### Changed

### Deprecated

### Removed

### Fixed

- Fehler bei Event-Subprozess-Erkennung im Parser behoben
- Teststruktur und -erkennung für neue Szenarien verbessert

### Security
