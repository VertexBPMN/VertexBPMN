# VertexBPMN Azure Infrastructure (P5)

Versioned Bicep IaC for the VertexBPMN Azure deployment. Declares the resources that were
previously provisioned by hand (P4) and the Container Apps workload, so every environment is
reproducible from code. **No secrets in this repo** — parameter files only carry non-secret
values; credentials are stored in Key Vault and referenced at deploy time.

## Layout

```
infra/
  main.bicep              # root orchestration (targetScope = resourceGroup)
  bicepconfig.json        # linter rules (secure-params errors, etc.)
  params/
    stage.bicepparam      # stage environment (non-secret values only)
    prod.bicepparam       # prod environment (non-secret values only)
  modules/
    network.bicep         # VNet, ACA-delegated subnet, private endpoints + private DNS
    observability.bicep   # Log Analytics workspace + App Insights
    servicebus.bicep      # namespace + vertexbpmn-runtime topic + subscriptions
    postgresql.bicep      # Flexible Server (PG16, B1ms) + 5 engine DBs + firewall
    keyvault.bicep        # Key Vault (RBAC) + dataprotection-key (RSA-3072)
    storage.bicep         # storage account + dataprotection-{api,studio} blob containers
    acr.bicep             # Azure Container Registry (Premium, private)
    aca-environment.bicep # VNet-integrated Container Apps Environment
    containerapp.bicep    # reusable app module (studio/api/agent-worker)
    migration-job.bicep   # manual Container Apps Job (--migrate-only, 1 instance)
    identity.bicep        # user-assigned MI + least-privilege role assignments
```

## Secret handling

- **PostgreSQL admin password** lives in Key Vault as `pg-admin-password` (set once at
  provision time, never in this repo). The `postgresql.bicep` module accepts `adminPassword`
  as a `@secure()` parameter so it can be injected at deploy time without being committed.
- **Engine connection strings** (`BpmnDbContext`, `TenantDbContext`, …) are wired to Container
  Apps as **Key Vault secret references** (`secretref:`), not plaintext env vars. The `api`
  container app maps `ConnectionStrings__<Name>` → `secretref:<KeyVaultSecretName>`.
- **OIDC session store** and **JWT secret** are likewise KV-referenced.
- Parameter files contain only non-secret values (location, sku-affecting switches, image tag).

## Validation

```bash
# Lint + compile
az bicep build -f main.bicep
az bicep build-params -f params/stage.bicepparam

# Dry-run against an existing resource group (creates nothing)
az deployment group what-if -g <rg> -f infra/main.bicep -p infra/params/stage.bicepparam
```

## Deployment

```bash
# Actual provisioning (cost-incurring) — requires an explicit user OK.
az deployment group create -g <rg> -f infra/main.bicep -p infra/params/stage.bicepparam \
  --parameters postgresAdminPassword='<from Key Vault>' \
               imageTag='<immutable version>'
```

## Notes / known plan decisions

- **PostgreSQL region**: this subscription has Flexible Server restricted in
  `<your-region>`/`westeurope`; deploy in `<your-region>` (EU, DSGVO-compliant). The
  `postgresLocation` parameter is separate from the general `location` for this reason.
- **MI-only access**: the user-assigned identity (`<prefix>-mi`) carries only the roles it
  needs (ACR Pull, KV Secrets/Crypto User, Service Bus Sender/Receiver, Blob Data
  Contributor). No interactive credentials in the production path.
- **Studio** has external HTTPS ingress (single revision, sticky affinity, 1 replica until P4
  session/circuit acceptance). **API** and **agent-worker** have internal (VNet-only) ingress.
- **ACR** uses Premium for Private Link; network module wires private endpoints + private
  DNS for PostgreSQL, Key Vault, Service Bus and ACR.
