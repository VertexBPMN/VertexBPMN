# VertexBPMN local full-stack Compose (Docker / WSLC)

Selbst-gehosteter Stack: **postgres** (5 Engine-DBs) + **rabbitmq** (inkl. Management-UI) + **keycloak** (OIDC, mit eigenem Postgres) + **api** + **studio** + lokaler **agent-worker**.

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

1. `publish-build.sh` publiziert API, Studio und AgentWorker **auf dem Host** (nutzt lokales SDK 10.0.302 + NuGet-Cache, schreibt auf die Host-Disk).
2. Die drei `runtime-*.Dockerfile` (Context = `deploy/compose/`) kopieren nur die fertigen Publish-Ordner in schlanke `aspnet:10.0.12`-Runtime-Images — kein Build im Container, kleine Images. Publish ist ausdrücklich `linux-x64`, auch auf Windows, damit beispielsweise die SQLite-Bibliothek zum Linux-Container passt. Für ARM64 den Runtime-Parameter entsprechend setzen.

`publish/` ist gitignored und wird bei Bedarf neu erzeugt.

## Windows ohne Docker / Podman: WSLC

Das zusätzliche Profil verwendet das WSLC-Bridge-Netzwerk. API, Studio, PostgreSQL,
RabbitMQ und Keycloak laufen dabei tatsächlich in Containern; keine native Ersatz-API.
Das vorhandene Linux-/Docker-Profil bleibt unverändert.

Voraussetzungen: .NET-SDK aus `global.json`, Python mit PyYAML und dem
`wslc_compose`-Paket (Version 0.5 oder neuer), WSLC 2.9 und ausgefüllte `.env`.
Alternativ das `src`-Verzeichnis eines lokalen wslc-compose-Checkouts mit
`-ComposeSource` oder `WSLC_COMPOSE_SOURCE` angeben. Der Wrapper erkennt auch
einen benachbarten Checkout `../wslc-compose/src`. Falls WSLC einen erhöhten
Prozess verlangt, die folgenden Befehle in einer Administrator-PowerShell ausführen.

```powershell
cd C:\repo\VertexBPMN\deploy\compose
Copy-Item .env.example .env # nur beim ersten Mal; anschließend Secrets ersetzen
# Editor-Assets bei Änderungen zuvor über npm ci + npm run build:bpmnio erzeugen.
.\publish-build.ps1 -ContainerEngine WSLC

# Bereits publizierte Images / Container verwalten:
.\wslc.ps1 -ComposeArguments @('ps')
.\wslc.ps1 -ComposeArguments @('up', '-d', '--wait', '--wait-timeout', '600')
.\wslc.ps1 -ComposeArguments @('down') # Daten-Volumes bleiben erhalten
```

Studio: [http://localhost:5263](http://localhost:5263), API:
[http://localhost:51870/api/ready](http://localhost:51870/api/ready), Keycloak:
[http://localhost:58080](http://localhost:58080). PostgreSQL wird ausschließlich
an Loopback-Port 55432, AMQP an 55672 gebunden, damit lokale Standardinstallationen
auf 5432/5672 weiter funktionieren. Alle aufgeführten Ports müssen frei sein;
alternative DB-/Broker-Ports sind `WSLC_POSTGRES_PORT`, `WSLC_AMQP_PORT` und
`WSLC_RABBIT_MANAGEMENT_PORT`. Browser-/OIDC-Ports bleiben passend zum Test-Realm fest.

`wslc.override.yml` wird **zusätzlich** zur Basisdatei verwendet. Keine
`--ignore-unsupported`-Option: Host-Netzwerk und nicht unterstützte automatische
Restart-Policies werden explizit entfernt. WSLC startet abgestürzte Dienste
hier nicht automatisch neu; das Profil ist eine lokale Testumgebung, keine
Produktionsbereitstellung. `wslc-compose-compat.py` übersetzt WSLC-JSON/NDJSON,
Feldnamen und Portdarstellungen, ohne installierte Werkzeuge zu verändern.
Lokale Adaptertests: `python .\test_wslc_compat.py`.

Nach einem erneuten Publish verwenden die Startskripte `--force-recreate`, weil
wslc-compose 0.5 unveränderte Konfigurationen sonst trotz neu gebautem Image
als aktuell behandelt. Das startet die ausgewählten Container einschließlich
ihrer Abhängigkeiten neu; persistente Volumes werden dabei nicht gelöscht.

Der öffentliche OIDC-Issuer bleibt `http://localhost:58080/realms/vertexbpmn`.
Nur serverseitige Discovery-/JWKS-/Token-Anfragen werden über die zusätzlich
konfigurierten `Jwt:BackchannelAuthority` und
`StudioAuthentication:BackchannelAuthority` nach `http://keycloak:8080`
geroutet. Diese Ausnahme ist auf das **OidcTest-Profil**, einen Loopback-Issuer
und genau den lokalen `vertexbpmn`-Realm beschränkt. Production, andere Hosts
und andere Realms werden beim Start abgewiesen. Signatur, Issuer, Audience,
Laufzeit, PKCE und Tenant-/Rollenprüfung bleiben aktiv.

Der optionale Agent wird erst mit `-ComposeArguments @('--profile', 'agent',
'up', '-d', '--build', '--wait-timeout', '600')` gestartet. Dazu muss
`OLLAMA_ENDPOINT` ausdrücklich aus dem Bridge-Netzwerk erreichbar sein;
`127.0.0.1` bezeichnet im Worker dessen eigenen Container. Das Standardprofil
benötigt weder Ollama noch einen Cloud-KI-Dienst.

## Healthchecks

Die .NET-aspnet-Images enthalten **kein curl/wget**. `healthcheck.sh` (`hcheck`) prüft per bash `/dev/tcp` mit rohem HTTP/1.0-GET: akzeptiert `200` (API `/api/ready`) und `302` (Studio → OIDC-Login ist ein lebendes, korrekt konfiguriertes App-Signal).

> **Pitfall:** Der `Host`-Header muss den Port enthalten (`Host: localhost:5263`). Ohne Port leitet das Studio die OIDC-`redirect_uri` als `http://localhost/signin-oidc` ab (nicht registriert) → Keycloak antwortet `invalid_request` → HTTP 500. Und: `healthcheck.sh` ist als Bind-Mount eingebunden — nach einer Änderung daran `docker compose up -d --force-recreate <service>`, sonst bleibt der alte inode aktiv.

## Architektur / wichtige Entscheidungen

- **netzwerke:** `api` und `studio` laufen auf dem **Docker-Host-Netz (`network_mode: host`)**. Grund: Der API-Security-Guard gestattet `Jwt:RequireHttpsMetadata=false` gegen HTTP-Keycloak **nur** im `OidcTest`-Profil mit **Loopback-Authority** (`http://localhost:58080`). Im Host-Netz ist `localhost` der Host — Keycloak (:58080), Postgres (:5432), RabbitMQ (:5672) und die Apps (:51870/:5263) sind alle über `localhost` erreichbar, exakt wie im getesteten OIDC-E2E.
- **Keycloak-Realm:** wird beim Start aus `../../deploy/keycloak/vertexbpmn-realm.json` importiert. Da dort weder Client-Secrets noch Nutzer stehen, setzt ein **One-Shot-`keycloak-bootstrap`** per `kcadm` (JSON-IDs via `sed`, das Image hat kein python/jq) getrennte Studio-/Worker-Secrets und legt den Testnutzer an; API, Studio und Worker starten erst nach erfolgreichem Bootstrap.
- **AgentWorker:** verwendet ausschließlich Client Credentials des Clients `vertexbpmn-contract-reviewer`. Seine Tokenclaims sind auf `tenant-a`, `agent.contract-review` und `contract-reviewer.v1` begrenzt. Ollama muss auf dem Host unter `OLLAMA_ENDPOINT` erreichbar sein; es gibt keinen Cloud-Fallback. Für jeden weiteren Tenant ist ein eigener, entsprechend begrenzter Client erforderlich.
- **Non-root:** Die Runtime-Images chownen `/app` und `/var/lib/vertexbpmn` auf `$APP_UID`, damit die App als Non-Root (`USER $APP_UID`) Plugin-/State-Verzeichnisse anlegen kann.
- **DBs:** `vertexbpmn_bpmn` entsteht via `POSTGRES_DB`; `tenants/simulation/events/decision` per `init-engine-dbs.sql` (`/docker-entrypoint-initdb.d`). Migrations laufen beim ersten API-Start (`Database__ApplyMigrationsOnStartup=true`).
- **Outbox:** API publiziert Runtime-Events über RabbitMQ (`Runtime__Outbox__Provider=RabbitMq`). Ohne Broker/Berechtigung akkumulieren `pending`-Outbox-Zeilen (siehe `docs/runbooks/production-deployment.md`).
- **Durable State:** Volumes `engine-pgdata`, `keycloak-pgdata`, `rabbitmq-data`, `vertexbpmn-state` (letzteres hält Dependency-Registry-SQLite + Data-Protection-Key-Ring der API; bei mehreren API-Replikas ist ein RWX-Volume nötig).

## Laufzeitmodus-Hinweis

Der Stack läuft bewusst im **`OidcTest`**-Profil (nicht `Production`), weil für einen HTTP-Keycloak ohne TLS nur in diesem Profil HTTPS-Metadaten abgeschaltet werden dürfen. Für eine echte Produktions-Freigabe (Zielprofil `docs/reviews/2026-09-09_Keycloak-OIDC_Self-Hosting_Umsetzungsplan.md`) sind TLS-Terminierung vor Keycloak/API sowie das `Production`-Profil mit durabler Key-Ring- und Replikat-Konfiguration umzusetzen — bewusst offen, nicht Teil dieses Compose-Stacks.

## Secrets

Alle Secrets gehören in `.env` (gitignored, siehe `.gitignore`). Studio- und Worker-Secret dürfen
nicht identisch sein. Kein Klartext-Secret committen.
