# Azure Release Workflow (P7) — Setup & Operations Runbook

Gültig für `.github/workflows/azure-release.yml`. Überführt den manuellen
Stage-Betrieb (Container-Apps per `az`/`az acr login` deployt) in einen
reproduzierbaren, OIDC-gesicherten CI/CD-Release mit Commit-SHA-Image-Tags.
Dieses Runbook ist der **Set-up-Leitfaden und die Betriebsanleitung** — es
dokumentiert, woher jede Variable/Secret kommt und wie der VNet-Smoke-Test des
Workflows tatsächlich ausgeführt wird (der Workflow-Stub druckt nur den Hinweis).

Security-Prinzipien (Plan P7 / AGENTS.md):
- **Keine langlebigen `AZURE_CLIENT_SECRET`** in GitHub Secrets — nur Federated
  Credentials (OIDC). `id-token: write` wird je Job deklariert.
- **Immutable Image-Tags:** API/Studio/AgentWorker werden mit dem Commit-SHA
  getaggt und `<imageTag>`-Parameter des Bicep-Deployments identisch gesetzt.
  `:latest` ist untersagt.
- Kein automatisches Schema-Downgrade; Migrations-Job läuft vor API-/Studio-Traffic.
- Production-Deployment erfordert ein GitHub-Environment mit manueller Freigabe.

## 1. OIDC Federation einmalig einrichten

Je Umgebung (Stage, Prod) wird ein eigenes GitHub Environment und eine eigene
Federated Credential-Identität empfohlen (Least Privilege, getrennte Audit-Trails).

1. GitHub-Umgebung anlegen: **Settings → Environments → New environment**
   (`stage`, `prod`). Für `prod`: *Required reviewers* aktivieren (manuelle
   Freigabe) und `WAIT_TIMEOUT` nicht zu knapp setzen.
2. In Entra ID ein App-Registration (oder benutzte Managed Identity-Identität)
   mit Federated Credential:
   - Issuer: `https://token.actions.githubusercontent.com`
   - Subject: `repo:<owner>/<repo>:environment:stage` (bzw. `:prod`)
   - Audience: `api://AzureADTokenExchange`
3. Die Identität muss RBAC erhalten:
   - `AcrPush` auf der Ziel-ACR (für den `build-and-push`-Job).
   - `Contributor` (oder maßgeschneiderte Least-Privilege-Rolle, die
     `Microsoft.App/containerApps/*`, `.../jobs/*` und
     `Microsoft.Resources/deployments/*` abdeckt) auf der Ziel-Ressourcengruppe.
4. GitHub Secrets setzen (Repository-Level, nicht Environment-gebunden):
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

> Hinweis: Alternativ kann ein einzelnes Federated-Credential die `:stage`- und
> `:prod`-Umgebung unterscheiden (zwei Federated Credentials auf derselben App),
> wenn RBAC ohnehin über die Ressourcengruppen trennt. Für saubere Trennung der
> Freigabe-Gates bleiben getrennte Environments der Standardweg.

## 2. Repository-/Environment-Variablen

Nicht-sekrete Variablen (Repository-Level für gemeinsame Werte, Environment-Level
für umgebungsspezifische):

| Variable | Beispiel | Bedeutung |
| --- | --- | --- |
| `ACR_NAME` | `vertexbpmnstageacr` | Registrierungsname ohne `.azurecr.io` |
| `RESOURCE_GROUP` | `rg-vertexbpmn-stage` | Ziel-RG (entspricht Deployment-Gruppe) |
| `NAME_PREFIX` | `vertexbpmn` | Präfix aus `main.bicep` (steuert App-Namen) |
| `LOCATION` | `westeurope` | Zielregion (Doku; wenn gewünscht in Parametern) |
| `PARAMS_FILE` | `params/stage.local.bicepparam` | Bicep-Param-Datei relativ zu `infra/` |
| `STUDIO_API_BASE_URL` | `https://studio.stage.yourdomain` | optional |
| `OIDC_AUTHORITY` | … | optional, erst nach P6-OIDC |
| `OIDC_CLIENT_ID` | … | optional |
| `OIDC_API_SCOPE` | … | optional |

**`PARAMS_FILE` ist zentral:** Der Workflow ruft
`-f infra/main.bicep -p infra/<PARAMS_FILE>`. Die Committed-Templates
`params/stage.bicepparam`/`prod.bicepparam` sind **generische Platzhalter**
(`<your-region>`, `<your-name-prefix>`). Für reale Deployments eine lokale Kopie
anlegen (z. B. `params/stage.local.bicepparam`), die echten Wert für
`namePrefix`, `location`, `postgresAdminUsername` und ein von der CI
verwaltetes `imageTag`-Default enthalten. Die Datei darf **keine Secrets**
enthalten (nur nicht-sekret Werte): Connection-Strings/Passwörter kommen aus
Key Vault per `secretref:` (siehe `infra/README.md`).

## 3. Release ausführen

Workflow per **Actions → Azure Release (P7) → Run workflow** starten:

- `environment`: `stage` (auto-Deploy nach Bicep) oder `prod` (manuelle Freigabe).
- `deploy_only`: true, wenn nur die Infrastruktur neu deployed werden soll und
  die Images für den SHA bereits im ACR liegen (spart Build-Zeit).
- `skip_migrations`: true, wenn das Schema unverändert ist.

Pipeline-Reihenfolge im Workflow:
1. `build-and-push` — Checkout, OIDC-Login, ACR-Token-Login (kein Admin),
   3 Images (`vertexbpmn-api/-studio/-agent-worker`) mit `:<SHA>` bauen + pushen,
   Digests erfassen.
2. `deploy` (needs build-and-push) — Parameter bauen (imageTag=SHA + optionale
   Overrides), **Bicep what-if** (folgenlos), **Apply** (`--no-wait`), dann
   Poll auf aktive API-Revision mit `trafficWeight>0` und `*SHA*`-Image.
3. **Migration-Job** starten und auf `Succeeded` warten (Schema passend zum
   deployed Digest; Reihenfolge vor Traffic-Schaft).
4. Readiness/Smoke — Stub im Workflow; siehe Abschnitt 4.

## 4. VNet-Smoke-Test (Readiness/Studio/Service Bus)

Die API ist `internal` (nur aus dem VNet erreichbar). Der Freigabe-Nachweis aus
Plan P7 Schritt 6 (`/api/health/live`, `/api/ready`, Studio-Login,
Service-Bus-Smoke) **muss aus dem VNet laufen** und ist daher ein separater,
manuell gesteuerter Schritt — im Workflow als dokumentierter Stub angelegt.
Optionen:

- **Option A (empfohlen): kurzlebiger Smoke-Job** als zweites
  `runtime-api`-Image im VNet (Container Apps Job) starten, der die HTTP-Probes
  gegen `<prefix>-api` aufruft und Logs in Log Analytics schreibt.
- **Option B:** temporärer Bastion-/Jumpbox-VM-Tunnel in das VNet, von dort
  `curl https://<api-fqdn>/api/ready`.
- **Option C:** `aja smoke`-Tool auf dem Runner mit VNet-Peering (nur wenn der
  Runner bereits VNet-zugewiesen ist).

Nachweis im Release-Kommentar/Environment festhalten: `live`+`ready`=200,
Studio-Login ok, ASB-Smoke (Senden/Empfangen) ok.

## 5. Rollback (kein automatisches Schema-Downgrade)

Bei fehlgeschlagenem Release nach Digests zurückrollen (nicht auf `latest`):

1. **Schema:** Kein Auto-Downgrade. Nur zurückspringen, wenn die alte
   App-Version schema-kompatibel ist; andernfalls den getesteten Backup-Restore
   aus `docs/runbooks/production-deployment.md` verwenden.
2. **API:** vorherige kompatible Revision aktivieren.
   `az containerapp revision list` → alte Revision (altes `*SHA*`), dann
   `az containerapp revision activate -g "$RG" -n "<prefix>-api" --revision <alt>`.
   Bei Multi-Revision-Modus Traffic-Gewicht auf die alte Revision setzen.
3. **Studio:** Single-Revision-Rollback auf vorherigen Digest mit angekündigtem
   Reconnect (Blazor-Circuit-Neustart). Kein ungeprüfter Multi-Revision-Split.
4. **Broker/Outbox:** Vor Rückwechsel DB-Outbox (Failed/Leases), alte Queues,
   Inbox-Claims inventarisieren/abgleichen. Leerer `outbox_pending`-Zähler allein
   genügt nicht (Plan P7 Punkt 5). Nach erstem ASB-Versand erfordert Rückwechsel
   denselben Abgleich.
5. **Audit:** Digest, Infrastruktur-Änderung, Migrationsergebnis und Rollback-Plan
   im GitHub-Release/Environment protokollieren.

## 6. CI-Safe-Suite bleibt getrennt

Der schnelle PR-Workflow (`ci.yml`) führt keine Azure-Deployments aus. Diese
Release-Workflow-Familie ist explizit manuell (`workflow_dispatch`) und über
GitHub Environments gated. Azure-Integrationstests (P8.1) folgen dem separaten
Opt-in-Playbook `docs/runbooks/azure-service-bus-integration-tests.md`.

## Abnahmekriterium (Plan P7)

„Ein Stage-Release und ein kontrollierter Rollback sind ohne Portal-Klicks, mit
nachvollziehbaren Digests und auditierbaren Logs reproduzierbar.“ — erfüllt,
sobald der Workflow gegen eine reale Stage-Umgebung mit OIDC (Abschnitt 1–2)
einmal grün durchgelaufen ist und ein Rollback laut Abschnitt 5 ausgeführt wurde.
