# Reproduzierbarer Build und Test

## Voraussetzungen

- Git-Checkout ohne versionierte `bin`-/`obj`-Artefakte
- .NET SDK `10.0.302` gemäß `global.json`
- Node.js 22 für die Studio-Web-Assets

`dotnet --version` muss im Repository `10.0.302` ausgeben. `rollForward` ist deaktiviert, damit lokale Builds und CI denselben SDK-Feature-Band verwenden.

## Sauberer Lauf unter Linux/WSL und Windows

Die Befehle sind in Bash und PowerShell identisch:

```text
dotnet restore VertexBPMN.sln --force --no-http-cache --disable-parallel
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-not-trait "Category=Phase3ExternalAcceptance" --max-parallel-test-modules 1
```

Der Testaufruf zielt absichtlich auf das echte zentrale Testprojekt. Ein Solution-weiter `dotnet test` würde auch den interaktiven `PerformanceRunner` starten, der in einer nicht interaktiven CI-Session auf `Console.ReadKey()` scheitert.

Die Studio-Assets werden separat reproduziert:

```text
cd src/VertexBPMN.Studio
npm ci
npm run build:bpmnio
npm run test:vertex-moddle
```

## Wechsel zwischen Windows und WSL/Linux

`obj/project.assets.json` ist plattformspezifisch. Ein Windows-Restore kann unter WSL beispielsweise einen Visual-Studio-Fallbackpfad wie `C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages` enthalten. In diesem Fall keinen `--no-restore`-Build starten, sondern zuerst den erzwungenen Restore aus dem vorherigen Abschnitt ausführen. Dadurch werden die Assets für das aktive Betriebssystem neu erzeugt.

Build-Ausgaben dürfen nie committed werden. Das Repository-Gate kann lokal so geprüft werden:

```text
bash scripts/verify-no-tracked-build-artifacts.sh
```

## Persistente BPMN-Acceptance-Verträge

Die Phase-1-Kernverträge werden als separates, grünes Blocker-Gate ausgeführt (der historische Skriptname bleibt aus Kompatibilitätsgründen bestehen):

```text
bash scripts/verify-phase1-acceptance-baseline.sh
```

Das Gate führt `P1-AC-01` bis `P1-AC-05` mit sieben konkreten Testmethoden aus und akzeptiert ausschließlich einen erfolgreichen Lauf. Die zusätzlichen Phase-2-Verträge sind Bestandteil der regulären grünen Suite und können gezielt ausgeführt werden:

```text
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-trait "Category=Phase2Acceptance" --max-parallel-test-modules 1
```

Das Phase-4-Gate qualifiziert den öffentlich freigegebenen DMN-Subset und erzwingt mit neun konkreten Testfällen die fail-closed Verträge für CMMN-Lifecycle, Simulation und Prozessmigration:

```text
bash scripts/verify-phase4-acceptance.sh
```

Die Phase-3-Akzeptanzverträge für Outbox-Leasing, Retry, Readiness, persistente Metriken und Deployment-Härtung laufen in der regulären Suite. Vier zusätzliche externe Verträge prüfen bei Bedarf echte RabbitMQ- und PostgreSQL-Dienste; der schnelle GitHub-Workflow startet diese Infrastruktur nicht. Der lokale Lauf benötigt:

```text
VERTEXBPMN_TEST_RABBITMQ=amqp://...
VERTEXBPMN_TEST_POSTGRES_ADMIN=Host=...;Database=postgres;Username=...;Password=...
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-trait "Category=Phase3ExternalAcceptance" --max-parallel-test-modules 1
```

Migration und Modell müssen außerdem synchron sein:

```text
dotnet ef migrations has-pending-model-changes --project src/VertexBPMN.Infrastructure --startup-project src/VertexBPMN.Api --context BpmnDbContext --no-build
```

## Lokale Studio-GUI-End-to-End-Tests

Die Real-E2E-Suite startet die echte API, das echte Studio und einen echten Chromium-Browser. Sie verwendet keine Stub-API und läuft absichtlich nicht in GitHub Actions. PostgreSQL und RabbitMQ werden lokal über WSLC oder über bereits installierte Dienste bereitgestellt.

Automatische Auswahl: Wenn `wslc.exe` vorhanden ist, wird WSLC verwendet; andernfalls werden bestehende lokale Dienste erwartet:

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Auto
```

WSLC beziehungsweise vorhandene Dienste können explizit ausgewählt werden:

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Wslc
./scripts/test-studio-e2e.ps1 -Infrastructure Existing -PostgresPort 5432 -RabbitMqPort 5672
```

Der Runner baut API, Studio und Browser-Testprojekt vor dem Infrastrukturzugriff, prüft beide TCP-Endpunkte und führt ausschließlich Tests mit `Category=LocalStudioE2E` aus. API und Studio laufen auf freien Ports und werden nach dem Test vollständig beendet. Die WSLC-Datencontainer bleiben bestehen, damit lokale Folgeläufe ihre Infrastruktur wiederverwenden können.

Ein direkter Lauf der separaten UI-Suite startet die Real-E2E-Tests nicht: Sie sind zusätzlich durch `VERTEXBPMN_STUDIO_E2E_ENABLED=true` geschützt. Diese Variable sowie die benötigten Verbindungswerte setzt der Runner nur für die Dauer des lokalen Testprozesses.

## Optionale erweiterte Qualitätsprüfungen

Nach Restore, Release-Build und `npm ci` prüfen die folgenden Befehle die aufgelösten NuGet-/npm-Abhängigkeiten, mindestens 60% Zeilen- und 45% Branch-Coverage sowie zwei byteidentische SDK-/CLI-Paketläufe:

```text
bash scripts/verify-dependency-audit.sh
bash scripts/verify-coverage.sh
bash scripts/verify-reproducible-packages.sh 1.0.0-local.1
```

Diese Prüfungen sind für gezielte Qualifikationsläufe verfügbar, blockieren aber nicht den schnellen Standardworkflow. Der aktuelle Workflow und weitere optionale Checks sind in [Build-, Security- und Release-Prüfungen](security-and-release-gates.md) beschrieben.
