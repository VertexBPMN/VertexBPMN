# ADR-0001 — Azure-Produktionshosting für VertexBPMN

- **Status:** Angenommen (vorbehaltlich Budget-/SKU-Freigabe; keine kostenpflichtigen Ressourcen bis Freigabe)
- **Datum:** 2026-09-14
- **Entscheider:** Yovanny Rodríguez (Yova) / Product & Operations
- **Kontext:** [Umsetzungsplan Azure-Deployment + Service Bus](2026-09-17_Azure-Deployment-und-Service-Bus_Umsetzungsplan.md), P0. Alle unten stehenden Parameter sind verbindliche, nicht-geheime Konfigurationsentscheidungen.

## Zielarchitektur

| Bereich | Entscheidung | Begründung |
|---|---|---|
| Region | **`<your-deployment-region>`** | Datenresidenz DE/niedrige Latenz nach Wahl (siehe Kostenschätzung) |
| Resource Group | **`<your-rg>`** (eine prod-Umgebung) | Alles wird darunter angelegt; Namen mit Suffixen `-api`/`-studio`/`-worker` |
| Container-Images | **Azure Container Registry (Basic)** | Versionierte Images per Digest; keine Builds im Zielsystem |
| Laufzeit | **Azure Container Apps (ACA)** | Getrennte Revisionen, HTTPS, KEDA, Managed Identities, CA-Jobs |
| Persistenz | **Azure Database for PostgreSQL Flexible Server** | Eine Instanz, **5 Engine-DBs** (bpmn, tenants, simulation, events, decision) + Dependency-Registry |
| Messaging | **Azure Service Bus — Standard** | Nur Runtime-Outbox/Inbox; **keine** Remote-Service-Tasks (HTTP-External-Tasks bleiben) |
| Secrets | **Azure Key Vault (Standard)** | Keine Secrets in Git/IaC-Parametern/Logs |
| Schlüsselring | **Azure Blob Storage (LRS, Hot)** | Gemeinsamer Data-Protection-Key-Ring; Wrap in Key Vault |
| Telemetrie | **Azure Monitor / Log Analytics / App Insights** | Logs, Traces, Metriken, Alarme |
| Identity | **User-assigned Managed Identities** | Minimalberechtigte Rollen (ACR, Key Vault, Service Bus, DB) |
| OIDC | **Microsoft Entra ID** | Kein Self-Hosting; TLS/PKCE/Code-Flow/Rollen- und Tenant-Claims |

## Öffentliche Erreichbarkeit

- **Einstieg ausschließlich über Azure Front Door (Standard) + WAF**.
- Öffentlich: **Studio** (`studio.<your-domain>`) **und API** (CLI/SDK/Webhooks) — jeweils mit eigenen Rate-Limits und WAF.
- Kein direkter öffentlicher Ingress auf ACA für API/Worker; nur Hinter Front Door.
- **Custom-Domain:** `studio.<your-domain>`.
- **DNS bleibt beim bestehenden Registrar** in `<your-domain>`. **Keine Azure-DNS-Zone, kein Domain-Umzug.** Bestehende Website `<your-domain>` und E-Mail-Konfiguration bleiben unverändert.
- Beim Deployment werden die konkreten **DNS-Einträge zum Eintragen bei IONOS** geliefert (siehe unten), z. B. CNAME auf den Front-Door-Endpunkt.

## Betriebsziele (SLOs)

- **RPO ≤ 15 min**, **RTO ≤ 4 h** (bestätigte Phase-0-Entscheidung, beizubehalten).
- Latenz-/Backlog-Ziele: werden in P8 als Messwerte gegen das Zielprofil belegt, nicht im Vorhinein zugesichert.

## Service-Bus-Semantik

- Topic `vertexbpmn-runtime` für Fan-out; pro fachlichem Konsumenten eine Subscription.
- Stabile `MessageId` = Runtime-Outbox-ID; `CorrelationId`/`tenantId`/`eventType`/`processInstanceId` als Application Properties.
- **At-least-once**; Duplikate zulässig; Inbox garantiert idempotente Fachwirkung. Keine Exactly-once-Zusage.
- **Keine** Remote-Service-Tasks über Service Bus; VertexBPMN.AgentWorker (HTTP) bleibt unverändert.

## Budget / Kostenschätzung

- Noch **keine feste Obergrenze**.
- Zuerst: **monatliche Kostenschätzung** für Stage und Production, aufgeschlüsselt nach Dienst/SKU (in Arbeit, siehe `docs/reviews/2026-09-14_Azure-Kostenschaetzung.md`).
- Es werden **zwei Varianten** gezeigt: (A) Stage / kostengünstiger Einstieg, (B) Production.
- **Keine kostenpflichtigen Azure-Ressourcen** bis zur Budget-/SKU-Freigabe.

## Konsequenzen / offene Punkte

- Service-Bus **Standard** bietet **keine Private Endpoints/VNet-Isolation** → alle Dienste mit Netzwerk-Regeln/Public-Policy absichern; ACR-Rollen und WAF entsprechend. Dies wird in P5 als Design-Implikation dokumentiert.
- Provider für Registry, Schlüsselring und Messaging bleiben unabhängig konfigurierbar; lokale Profile (Aspire/Compose/WSLC/SQLite) bleiben erhalten.
- Umgesetzt wird erst in den Phasen P1–P8; Abnahme gemäß Definition of Done des Umsetzungsplans.

## DNS-Einträge (für IONOS, wird beim Deployment konkretisiert)

| Typ | Name | Ziel | Zweck |
|---|---|---|---|
| CNAME | `studio` | `<front-door-endpoint>.azurefd.net` | Studio-Frontend |
| CNAME | `api` | `<front-door-endpoint>.azurefd.net` | API (CLI/SDK/Webhooks) |
| TXT (optional) | — | Domain-Verifikation (z. B. Entra/Custom-Domain) | OIDC/Domain-Validierung |

> Konkrete Endpunkt-Hostnamen liefert das Deployment (nach Provisionierung), nicht die Planung.
