# Build-, Security- und Release-Prüfungen

## Schneller GitHub-Workflow

Der Workflow `.github/workflows/ci.yml` besitzt bewusst nur zwei Jobs:

1. `Build and test` restauriert die Solution, baut sie einmal im Release-Modus unter Linux und führt `tests/VertexBPMN.Tests` aus. Die vier Tests mit `Category=Phase3ExternalAcceptance` benötigen echte RabbitMQ-/PostgreSQL-Dienste und sind vom schnellen Standardlauf ausgenommen. Interaktive Performance-Runner, Benchmarks und die separate Browser-Suite werden nicht über den Solution-weiten `dotnet test`-Aufruf gestartet.
2. `Publish SDK and CLI to NuGet` läuft ausschließlich für `v*`-Tags, übernimmt die im Build erzeugten Pakete und veröffentlicht sie mit NuGet Trusted Publishing.

Neue Läufe für denselben Branch brechen ältere laufende Builds ab. Pull Requests und `master` müssen in den Repository Rules nur den Check `Build and test` verlangen.

## Erweiterte Prüfungen bei Bedarf

Die aufwendigeren Prüfungen bleiben als lokale beziehungsweise manuell ausführbare Werkzeuge erhalten, blockieren aber nicht mehr jeden Pull Request oder Release:

```text
bash scripts/verify-dependency-audit.sh
bash scripts/verify-coverage.sh
bash scripts/verify-openapi-snapshot.sh
bash scripts/verify-phase1-acceptance-baseline.sh
bash scripts/verify-phase4-acceptance.sh
bash scripts/verify-reproducible-packages.sh 1.0.0-local.1
```

Die externen Verträge benötigen explizite Verbindungsdaten:

```text
VERTEXBPMN_TEST_RABBITMQ=amqp://...
VERTEXBPMN_TEST_POSTGRES_ADMIN=Host=...;Database=postgres;Username=...;Password=...
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --filter-trait "Category=Phase3ExternalAcceptance" --max-parallel-test-modules 1
```

Auf dem lokalen Windows-/WSL-Rechner können API- und Studio-Container mit WSLC gebaut und geprüft werden:

```text
wslc.exe build --tag vertexbpmn:release-check .
wslc.exe build --file src/VertexBPMN.Studio/Dockerfile --tag vertexbpmn-studio:release-check .
wslc.exe inspect --type image vertexbpmn:release-check
wslc.exe inspect --type image vertexbpmn-studio:release-check
```

## Tagged Releases

Ein Tag wie `v1.0.1` durchläuft denselben Build und dasselbe zentrale Testprojekt wie `master`. Anschließend werden `VertexBPMN.Sdk` und `VertexBPMN.Cli` einmal mit der Version aus dem Tag paketiert, als Workflow-Artefakt hochgeladen und vom Publish-Job übernommen.

NuGet Trusted Publishing muss Repository `VertexBPMN/VertexBPMN`, Workflow-Datei `ci.yml` und den tatsächlichen NuGet-Profilersteller aus dem GitHub-Secret `NUGET_USER` verwenden. `NuGet/login` erzeugt über OIDC einen kurzlebigen Schlüssel; ein dauerhaftes `NUGET_API_KEY`-Secret ist nicht erforderlich.
