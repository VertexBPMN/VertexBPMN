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
dotnet build VertexBPMN.sln --configuration Release --no-restore -m:1
dotnet test VertexBPMN.sln --configuration Release --no-build --no-restore --filter-not-trait "Category=Phase1Acceptance" --max-parallel-test-modules 1
```

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

Migration und Modell müssen außerdem synchron sein:

```text
dotnet ef migrations has-pending-model-changes --project src/VertexBPMN.Infrastructure --startup-project src/VertexBPMN.Api --context BpmnDbContext --no-build
```
