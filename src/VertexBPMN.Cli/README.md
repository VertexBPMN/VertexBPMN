# VertexBPMN CLI

`VertexBPMN.Cli` runs the local distributed engine directly in the terminal. It can also start the API and Blazor Studio as a local dashboard stack.

Start an interactive session:

```powershell
dotnet run --project src/VertexBPMN.Cli
```

Commands:

```text
vertexbpmn> register-bpmn order examples/order.bpmn
vertexbpmn> execute examples/order.bpmn
vertexbpmn> execute-id order
vertexbpmn> register-cmmn case-1 examples/case.cmmn
vertexbpmn> register-dmn score examples/score.dmn
vertexbpmn> execute-case examples/case.cmmn
vertexbpmn> status
vertexbpmn> pending
vertexbpmn> workers
vertexbpmn> dashboard
vertexbpmn> config list
vertexbpmn> config set Dependencies__Ai__DefaultModel gpt-4o-mini
vertexbpmn> config get Dependencies__Ai__DefaultModel
vertexbpmn> config remove Dependencies__Ai__DefaultModel
```

Commands can also be executed once:

```powershell
dotnet run --project src/VertexBPMN.Cli -- execute examples/order.bpmn
dotnet run --project src/VertexBPMN.Cli -- register-dmn score examples/score.dmn
dotnet run --project src/VertexBPMN.Cli -- dashboard
```

`dashboard` (also available as `studio`) follows the local control-plane workflow:

1. Reuse the API when it is already healthy.
2. Otherwise start `VertexBPMN.Api` on `http://localhost:51870/`.
3. Wait for `/api/Health` to report readiness.
4. Start `VertexBPMN.Studio` on `http://localhost:5263/`.
5. Open the Studio URL in the default browser.

The dashboard launcher settings are configurable in `appsettings.json`:

```json
"Dashboard": {
	"ApiProject": "src/VertexBPMN.Api/VertexBPMN.Api.csproj",
	"StudioProject": "src/VertexBPMN.Studio/VertexBPMN.Studio.csproj",
	"ApiUrl": "http://localhost:51870/",
	"StudioUrl": "http://localhost:5263/",
	"AutoStartApi": true,
	"AutoStartStudio": true,
	"OpenBrowser": true,
	"WaitTimeoutSeconds": 30
}
```

The API is the shared runtime gateway for the Studio. This keeps CLI, API, and dashboard state aligned when persistent engine connection strings are configured.

The CLI uses the existing engine, application, and infrastructure registrations. Runtime dependency configuration is persisted in the SQLite database configured by `ConnectionStrings:DependencyRegistry` (by default `vertexbpmn-dependencies.db`). The database values are loaded at startup and override JSON values. `VERTEXBPMN_` environment variables have the highest priority. Help requests (`--help`, `-h`, or `help`) do not initialize the host or access persistence.

The commands accept flattened .NET configuration keys. For example:

```powershell
dotnet run --project src/VertexBPMN.Cli -- config set Dependencies__Ai__DefaultModel gpt-4o-mini
dotnet run --project src/VertexBPMN.Cli -- config set Dependencies__Mcp__Enabled true
dotnet run --project src/VertexBPMN.Cli -- config list
```

The process and runtime stores still use the existing infrastructure provider selection. Configure their connection strings separately when those records must survive across CLI processes.
