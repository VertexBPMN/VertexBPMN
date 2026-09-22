# Isolierte Azure Service Bus Integrationstest-Suite (P8.1)

Dieses Runbook beschreibt, wie die P8.1-Integrationstests für die persistierte
Runtime-Outbox/Inbox gegen einen **echten, isolierten Azure Service Bus
Namespace** ausgeführt werden — bewusst getrennt vom schnellen GitHub-PR-Workflow.

## Warum isoliert und getrennt

- Die Unit-/Wire-Format-Tests (`AzureServiceBusOutboxWireFormatTests`,
  `InboxEnvelopeParseTests`, `AzureServiceBusRuntimeOutboxTests`,
  `AzureServiceBusInboxConsumerValidationTests`) laufen **ohne Broker** und
  gehören in den CI-Safe-Pfad.
- Echte Brokertests verlangen **kurzlebige Test-Credentials oder eine isolierte
  Test-Managed-Identity** und dürfen **nicht** in den schnellen Workflow. Sie
  werden manuell/Stage-gesteuert ausgeführt und per Umgebungsvariable opt-in
  aktiviert (Skip ohne Konfiguration), analog `AzureDataProtectionKeyRingAcceptanceTests`.

## Voraussetzungen / Opt-in

Die Suite skippt ihre Tests, bis alle Pflicht-Variablen gesetzt sind
(`Assert.SkipUnless`):

```text
VERTEXBPMN_TEST_ASB_FQNS=your-test-namespace.servicebus.windows.net
VERTEXBPMN_TEST_ASB_ENTITY=vertexbpmn-runtime-tests        # Topic (Test)
VERTEXBPMN_TEST_ASB_SUBSCRIPTION=all                        # Inbox-Subscription
VERTEXBPMN_TEST_ASB_AUTH=ManagedIdentity|ConnectionString
# bei managed identity, optional:
VERTEXBPMN_TEST_ASB_MI_CLIENT_ID=<user-assigned-mi-client-id>
# bei ConnectionString (nur lokale Ad-hoc-Tests):
VERTEXBPMN_TEST_ASB_CONNECTION_STRING=<connection string>
```

Kein Test darf Azure-Credentials aus der Standard-Umgebung anfordern, die im
schnellen PR-Workflow nicht vorhanden sind. Fehlt eine Variable → Skip (nicht
Fehlschlag).

## Angelegte Test-Ressourcen (empfohlen, per IaC)

- Namespace im **Standard**-Tier (Duplikaterkennung), in der Test-/Stage-RG,
  VNet-privat oder mit IP-Firewall auf die Testausführungsadresse.
- Ein Topic `vertexbpmn-runtime-tests` mit Subscription `all`.
- Beim Nano-Granular: ein Queue `vertexbpmn-runtime-tests-q`.
- Eine isolierte **Test-Managed-Identity** mit `Azure Service Bus Data Sender`
  und `Azure Service Bus Data Receiver` auf dem Namespace — **nicht** die
  Produktions-App-MI (kurzlebig, nach der Übung entfern- oder rotierbar).
- Optional: kurzlebiger Connection-String nur für lokale Ad-hoc-Läufe; nie
  committen, nie im PR-Workflow.

Die Entities werden durch nachfolgende IaC-Erweiterung provisioniert; bis dahin
gelten die Voraussetzungen in `infra/params/stage.local.bicepparam`/privater
Schicht.

## Geplante Szenarien (Klasse wird mit der autorisierten Einführung angelegt)

| Szenario | Beweisziel |
|---|---|
| Senden → Empfang | Outbox-Message wird am Topic/Queue zugestellt und vom Inbox-Processor verarbeitet |
| Duplicate Detection | identische `MessageId` wird nicht doppelt fachlich verarbeitet (stable MessageId greift) |
| Restart / redelivery | nicht-completete Message wird nach Abandon erneut zugestellt |
| Retry / transient | RetryableFailure → Abandon → Redelivery |
| DLQ / Rejected | unparseable/permanent-Reject → Dead-Letter mit strukturiertem Grund |
| Replay / Idempotenz | Replay derselben Message ohne doppelte Fachwirkung |
| Lock-Loss | abgelaufener Claim wird reklamiert und verarbeitet |

Jede Testklasse trägt `[Trait("Category", "AzureServiceBusIntegration")]` und
`Assert.SkipUnless(...)`, sodass die CI-Safe-Filterliste um
`--filter-not-trait "Category=AzureServiceBusIntegration"` ergänzt wird, sobald
sie existiert.

## Ausführung (manuell/Stage-gesteuert)

Lokale Host-Authentifizierung gegen den Namespace über die Test-MI:

```bash
export VERTEXBPMN_TEST_ASB_FQNS="<namespace>.servicebus.windows.net"
export VERTEXBPMN_TEST_ASB_ENTITY="vertexbpmn-runtime-tests"
export VERTEXBPMN_TEST_ASB_SUBSCRIPTION="all"
export VERTEXBPMN_TEST_ASB_AUTH="ManagedIdentity"
export VERTEXBPMN_TEST_ASB_MI_CLIENT_ID="<test-mi-client-id>"
# Zurücksetzen / Host-Authz:
#  az login && az account set --subscription <sub>
#  az identity allow-... oder VM-Assignierung der Test-MI / env AZURE_CLIENT_ID=<test-mi>
```

Build und gezielte Ausführung:

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
cd ~/repo/VertexBPMN
dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj -c Release --nologo -v q
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll \
  -class "VertexBPMN.Tests.Unit.Infrastructure.AzureServiceBusIntegrationTests"
```

Ohne die Variablen skipen alle Tests („nicht ausgeführt"), der Lauf ist grün.

## CI-Safe-Abgrenzung (Pflicht)

- Der schnelle PR-Workflow (`.github/workflows/ci.yml`, Schritt „Test (CI-safe suite)")
  darf diese Klasse **nicht** ausführen: zusätzlicher Filter
  `--filter-not-trait "Category=AzureServiceBusIntegration"`.
- Azure-Ressourcen (Namespace, MI) gehören in die **private** Deployment-Schicht
  bzw. ein explizit benanntes Test-Ressourcen-Set; keine Tracked-Identifier im
  öffentlichen Repo.
- Nach der Übung Aufräumen dokumentieren: Topic/Subscription löschen,
  kurzlebige MI rotieren/entfernen, Connection-String widerrufen.

## Abnahmekriterium (dieser Suite)

Mindestens **Senden→Empfang**, **Duplicate Detection**, **Retry/Redelivery**,
**DLQ/Rejected** und **Replay-Idempotenz** sind gegen den isolierten Namespace
nachgewiesen und die Ergebnisse (Testname, Exit-Code, Ausführungszeit, Namespace)
sind im Plan-Doc P8 vermerkt. Die Suite läuft nicht im PR-Workflow.
