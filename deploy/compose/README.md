# VertexBPMN full-stack docker compose

Selbst-gehosteter Stack: **postgres** (5 Engine-DBs) + **rabbitmq** (inkl. Management-UI) + **keycloak** (OIDC, mit eigenem Postgres) + **api** + **studio**.

## Schnellstart

```bash
cd deploy/compose
cp .env.example .env          # Secrets ausfüllen!
./publish-build.sh            # Host-Publish + docker compose up -d --build
```

Startet alles als Daemon. Danach:
- Studio / Browser-Login: **http://localhost:5263** (Login über Keycloak auf **http://localhost:58080**)
- API-Readiness: `curl -s http://localhost:51870/api/ready` → `Healthy`
- RabbitMQ-Management: **http://localhost:15672**
- Demo-Nutzer (vom Bootstrap angelegt): siehe `.env` (`KEYCLOAK_TEST_USER` / `..._PASSWORD`, Rolle `ProcessManager`, Tenant `tenant-a`)

Stoppen: `docker compose down` — mit Volumes: `docker compose down -v`

### Warum Host-Publish statt `docker build` aus den Repo-Dockerfiles?

Die Original-Dockerfiles machen einen **Solution-weiten Build im Container** (`dotnet restore VertexBPMN.sln` + publish ALLER Projekte) und brauchen ~5–6 GB freien Speicher im Container-Layer. Auf der 29-GB-Referenzpartition (Host-SDK ~6 GB + Repo + Basis-Images) schlägt das wiederholt mit `No space left on device` fehl. Daher:

1. `publish-build.sh` puplisht API + Studio **auf dem Host** (nutzt lokales SDK 10.0.302 + NuGet-Cache, schreibt auf die Host-Disk).
2. `runtime-api.Dockerfile` / `runtime-studio.Dockerfile` (Context = `deploy/compose/`) kopieren nur den fertigen Publish-Ordner in ein schlankes `aspnet:10.0.11`-Runtime-Image — kein Build im Container, kleine Images.

`publish/` ist gitignored und wird bei Bedarf neu erzeugt.

## Healthchecks

Die .NET-aspnet-Images enthalten **kein curl/wget**. `healthcheck.sh` (`hcheck`) prüft per bash `/dev/tcp` mit rohem HTTP/1.0-GET: akzeptiert `200` (API `/api/ready`) und `302` (Studio → OIDC-Login ist ein lebendes, korrekt konfiguriertes App-Signal).

> **Pitfall:** Der `Host`-Header muss den Port enthalten (`Host: localhost:5263`). Ohne Port leitet das Studio die OIDC-`redirect_uri` als `http://localhost/signin-oidc` ab (nicht registriert) → Keycloak antwortet `invalid_request` → HTTP 500. Und: `healthcheck.sh` ist als Bind-Mount eingebunden — nach einer Änderung daran `docker compose up -d --force-recreate <service>`, sonst bleibt der alte inode aktiv.

## Architektur / wichtige Entscheidungen

- **netzwerke:** `api` und `studio` laufen auf dem **Docker-Host-Netz (`network_mode: host`)**. Grund: Der API-Security-Guard gestattet `Jwt:RequireHttpsMetadata=false` gegen HTTP-Keycloak **nur** im `OidcTest`-Profil mit **Loopback-Authority** (`http://localhost:58080`). Im Host-Netz ist `localhost` der Host — Keycloak (:58080), Postgres (:5432), RabbitMQ (:5672) und die Apps (:51870/:5263) sind alle über `localhost` erreichbar, exakt wie im getesteten OIDC-E2E.
- **Keycloak-Realm:** wird beim Start aus `../../deploy/keycloak/vertexbpmn-realm.json` importiert. Da dort weder Client-Secrets noch Nutzer stehen, setzt ein **One-Shot-`keycloak-bootstrap`** per `kcadm` (JSON-IDs via `sed`, das Image hat kein python/jq) das Studio-Client-Secret und legt den Testnutzer an; `api`/`studio` starten erst, wenn dieser erfolgreich beendet ist (`depends_on: service_completed_successfully`).
- **Non-root:** Die Runtime-Images chownen `/app` und `/var/lib/vertexbpmn` auf `$APP_UID`, damit die App als Non-Root (`USER $APP_UID`) Plugin-/State-Verzeichnisse anlegen kann.
- **DBs:** `vertexbpmn_bpmn` entsteht via `POSTGRES_DB`; `tenants/simulation/events/decision` per `init-engine-dbs.sql` (`/docker-entrypoint-initdb.d`). Migrations laufen beim ersten API-Start (`Database__ApplyMigrationsOnStartup=true`).
- **Outbox:** API publiziert Runtime-Events über RabbitMQ (`Runtime__Outbox__Provider=RabbitMq`). Ohne Broker/Berechtigung akkumulieren `pending`-Outbox-Zeilen (siehe `docs/runbooks/production-deployment.md`).
- **Durable State:** Volumes `engine-pgdata`, `keycloak-pgdata`, `rabbitmq-data`, `vertexbpmn-state` (letzteres hält Dependency-Registry-SQLite + Data-Protection-Key-Ring der API; bei mehreren API-Replikas ist ein RWX-Volume nötig).

## Laufzeitmodus-Hinweis

Der Stack läuft bewusst im **`OidcTest`**-Profil (nicht `Production`), weil für einen HTTP-Keycloak ohne TLS nur in diesem Profil HTTPS-Metadaten abgeschaltet werden dürfen. Für eine echte Produktions-Freigabe (Zielprofil `docs/reviews/2026-09-09_Keycloak-OIDC_Self-Hosting_Umsetzungsplan.md`) sind TLS-Terminierung vor Keycloak/API sowie das `Production`-Profil mit durabler Key-Ring- und Replikat-Konfiguration umzusetzen — bewusst offen, nicht Teil dieses Compose-Stacks.

## Secrets

Alle Secrets gehören in `.env` (gitignored, siehe `.gitignore`). Kein Klartext-Secret committen.
