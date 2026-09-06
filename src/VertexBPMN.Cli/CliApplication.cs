using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.Configuration;
using VertexBPMN.Application.Import;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Configuration;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Cli;

internal sealed class CliApplication
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly IProcessEngine _engine;
    private readonly IBpmnParser _bpmnParser;
    private readonly ICmmnParser _cmmnParser;
    private readonly IWorkerNodeManager _workerManager;
    private readonly IDependencyRegistry _dependencyRegistry;
    private readonly IWorkflowTriggerService _triggerService;
    private readonly IRepositoryService _repositoryService;
    private readonly IRuntimeService _runtimeService;
    private readonly ICredentialService _credentialService;
    private readonly IConnectorService _connectorService;
    private readonly IConnectorTemplateService _connectorTemplateService;
    private readonly IFormDefinitionService _formDefinitionService;
    private readonly IDecisionService _decisionService;
    private readonly ISemanticValidationService _validationService;
    private readonly IN8nWorkflowImporter _n8nImporter;
    private readonly IOpenApiConnectorTemplateImporter _openApiImporter;
    private readonly DashboardLauncher _dashboardLauncher;

    public CliApplication(IServiceProvider services, TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
        _engine = services.GetRequiredService<IProcessEngine>();
        _bpmnParser = services.GetRequiredService<IBpmnParser>();
        _cmmnParser = services.GetRequiredService<ICmmnParser>();
        _workerManager = services.GetRequiredService<IWorkerNodeManager>();
        _dependencyRegistry = services.GetRequiredService<IDependencyRegistry>();
        _triggerService = services.GetRequiredService<IWorkflowTriggerService>();
        _repositoryService = services.GetRequiredService<IRepositoryService>();
        _runtimeService = services.GetRequiredService<IRuntimeService>();
        _credentialService = services.GetRequiredService<ICredentialService>();
        _connectorService = services.GetRequiredService<IConnectorService>();
        _connectorTemplateService = services.GetRequiredService<IConnectorTemplateService>();
        _formDefinitionService = services.GetRequiredService<IFormDefinitionService>();
        _decisionService = services.GetRequiredService<IDecisionService>();
        _validationService = services.GetRequiredService<ISemanticValidationService>();
        _n8nImporter = services.GetRequiredService<IN8nWorkflowImporter>();
        _openApiImporter = services.GetRequiredService<IOpenApiConnectorTemplateImporter>();
        _dashboardLauncher = services.GetRequiredService<DashboardLauncher>();
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
            return await RunInteractiveAsync(cancellationToken);
        if (args is ["--help"] or ["-h"])
        {
            WriteHelp(_output);
            return 0;
        }
        return await ExecuteCommandAsync(args, cancellationToken);
    }

    private async Task<int> RunInteractiveAsync(CancellationToken cancellationToken)
    {
        _output.WriteLine($"VertexBPMN CLI - {GetEngineType()} engine");
        while (!cancellationToken.IsCancellationRequested)
        {
            await _output.WriteAsync("vertexbpmn> ");
            var line = await Console.In.ReadLineAsync(cancellationToken);
            if (line is null)
                break;
            var command = Tokenize(line);
            if (command.Count == 0)
                continue;
            if (command[0] is "exit" or "quit")
                break;
            if (command[0] is "help" or "--help")
            {
                WriteHelp(_output);
                continue;
            }
            if (command[0] == "clear")
            {
                Console.Clear();
                continue;
            }
            await ExecuteCommandAsync(command.ToArray(), cancellationToken);
        }
        return 0;
    }

    private async Task<int> ExecuteCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "status":
                    await PrintStatusAsync(cancellationToken);
                    return 0;
                case "pending":
                    await PrintPendingAsync(cancellationToken);
                    return 0;
                case "workers":
                    RequireDistributedEngine();
                    await PrintWorkersAsync();
                    return 0;
                case "dashboard":
                case "studio":
                    await _dashboardLauncher.OpenAsync(cancellationToken);
                    return 0;
                case "config":
                    await ExecuteConfigCommandAsync(args, cancellationToken);
                    return 0;
                case "trigger":
                    await ExecuteTriggerCommandAsync(args, cancellationToken);
                    return 0;
                case "credential":
                    await ExecuteCredentialCommandAsync(args, cancellationToken);
                    return 0;
                case "connector":
                    await ExecuteConnectorCommandAsync(args, cancellationToken);
                    return 0;
                case "template":
                    await ExecuteTemplateCommandAsync(args, cancellationToken);
                    return 0;
                case "validate":
                    RequireArguments(args, 2);
                    await PrintValidationAsync(await ReadFileAsync(args[1]));
                    return 0;
                case "import-n8n":
                    RequireArguments(args, 2);
                    var importTenant = args.Length > 3 ? args[3] : "default";
                    var imported = _n8nImporter.Import(
                        await ReadFileAsync(args[1]),
                        await _credentialService.ListAsync(importTenant, cancellationToken));
                    var outputPath = args.Length > 2 ? args[2] : Path.ChangeExtension(args[1], ".bpmn");
                    await File.WriteAllTextAsync(outputPath, imported.BpmnXml, cancellationToken);
                    await _output.WriteLineAsync($"n8n workflow imported: {outputPath}");
                    foreach (var item in imported.Report) await _output.WriteLineAsync($"{item.Disposition}: {item.NodeName} - {item.Message}");
                    return 0;
                case "execute":
                    RequireArguments(args, 2);
                    var model = await _bpmnParser.ParseAsync(await ReadFileAsync(args[1]), cancellationToken);
                    await PrintTraceAsync(_engine.ExecuteAsync(model, cancellationToken));
                    return 0;
                case "execute-id":
                    RequireArguments(args, 2);
                    await PrintTraceAsync(_engine.ExecuteProcessAsync(args[1], cancellationToken));
                    return 0;
                case "deploy-bpmn":
                    RequireArguments(args, 2);
                    var deployed = await _repositoryService.DeployAsync(
                        await ReadFileAsync(args[1]),
                        Path.GetFileName(args[1]),
                        args.Length > 2 ? args[2] : null,
                        cancellationToken);
                    await _output.WriteLineAsync($"BPMN deployed: {deployed.Key} ({deployed.Id})");
                    return 0;
                case "deploy-dmn":
                    RequireArguments(args, 2);
                    var dmnPath = args[1];
                    var decisionKey = Path.GetFileNameWithoutExtension(dmnPath);
                    await _decisionService.DeployAsync(decisionKey, decisionKey, await ReadFileAsync(dmnPath), args.Length > 2 ? args[2] : null);
                    await _output.WriteLineAsync($"DMN deployed: {decisionKey}");
                    return 0;
                case "deploy-form":
                    RequireArguments(args, 2);
                    var formPath = args[1];
                    var formKey = Path.GetFileNameWithoutExtension(formPath);
                    var form = await _formDefinitionService.CreateAsync(TenantAt(args, 2), new(formKey, formKey, await ReadFileAsync(formPath)), cancellationToken);
                    await _output.WriteLineAsync($"Form deployed: {form.Key} ({form.Id})");
                    return 0;
                case "test-run":
                    RequireArguments(args, 3);
                    var testPath = args[1];
                    var testDefinition = await _repositoryService.DeployAsync(await ReadFileAsync(testPath), Path.GetFileName(testPath), args.Length > 3 ? args[3] : null, cancellationToken);
                    var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(await ReadFileAsync(args[2])) ?? throw new CliUsageException("Variables must be a JSON object.");
                    var instance = await _runtimeService.StartProcessByKeyAsync(testDefinition.Key, variables, $"cli-test-{Guid.NewGuid():N}", args.Length > 3 ? args[3] : null, cancellationToken);
                    await _output.WriteLineAsync($"Test run started: {instance.Id} ({testDefinition.Key})");
                    return 0;
                case "register-bpmn":
                    RequireArguments(args, 3);
                    await _engine.RegisterBpmnModelAsync(args[1], await ReadFileAsync(args[2]), cancellationToken);
                    await _output.WriteLineAsync($"BPMN registered: {args[1]}");
                    return 0;
                case "register-cmmn":
                    RequireArguments(args, 3);
                    await _engine.RegisterCmmnModelAsync(args[1], await ReadFileAsync(args[2]));
                    await _output.WriteLineAsync($"CMMN registered: {args[1]}");
                    return 0;
                case "register-dmn":
                    RequireArguments(args, 3);
                    await _engine.RegisterDmnModelAsync(args[1], await ReadFileAsync(args[2]));
                    await _output.WriteLineAsync($"DMN registered: {args[1]}");
                    return 0;
                case "execute-case":
                    RequireArguments(args, 2);
                    var caseModel = await _cmmnParser.ParseAsync(await ReadFileAsync(args[1]), cancellationToken);
                    await PrintTraceAsync(_engine.ExecuteCaseAsync(caseModel, cancellationToken));
                    return 0;
                case "help":
                case "--help":
                    WriteHelp(_output);
                    return 0;
                default:
                    throw new CliUsageException($"Unknown command '{args[0]}'. Use 'help' for available commands.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _error.WriteLineAsync("Operation cancelled.");
            return 130;
        }
        catch (CliUsageException exception)
        {
            await _error.WriteLineAsync($"Usage error: {exception.Message}");
            return 2;
        }
        catch (Exception exception)
        {
            await _error.WriteLineAsync($"Error: {exception.Message}");
            return 1;
        }
    }

    private async Task PrintStatusAsync(CancellationToken cancellationToken)
    {
        if (_engine is not IDistributedProcessEngine distributedEngine)
        {
            await _output.WriteLineAsync($"Engine: {GetEngineType()}");
            await _output.WriteLineAsync("Workers: unavailable, pending tokens: unavailable, pending case tokens: unavailable");
            return;
        }

        var workers = await _workerManager.GetActiveWorkersAsync();
        var pending = await distributedEngine.GetPendingTokensAsync(cancellationToken);
        var cases = await distributedEngine.GetPendingCaseTokensAsync(cancellationToken);
        await _output.WriteLineAsync($"Engine: {GetEngineType()}");
        await _output.WriteLineAsync($"Workers: {workers.Count}, pending tokens: {pending.Count}, pending case tokens: {cases.Count}");
    }

    private async Task PrintPendingAsync(CancellationToken cancellationToken)
    {
        RequireDistributedEngine();
        var distributedEngine = (IDistributedProcessEngine)_engine;
        var tokens = await distributedEngine.GetPendingTokensAsync(cancellationToken);
        var caseTokens = await distributedEngine.GetPendingCaseTokensAsync(cancellationToken);
        await _output.WriteLineAsync($"Execution tokens: {tokens.Count}");
        foreach (var token in tokens)
            await _output.WriteLineAsync($"  {token.Id} -> {token.CurrentNodeId} ({token.State})");
        await _output.WriteLineAsync($"Case tokens: {caseTokens.Count}");
        foreach (var token in caseTokens)
            await _output.WriteLineAsync($"  {token.Id} -> {token.CurrentPlanItemId}");
    }

    private async Task PrintWorkersAsync()
    {
        var workers = await _workerManager.GetActiveWorkersAsync();
        if (workers.Count == 0)
        {
            await _output.WriteLineAsync("No active workers.");
            return;
        }
        foreach (var worker in workers)
            await _output.WriteLineAsync($"{worker.Id}: {worker.CurrentLoad}/{worker.MaxCapacity} on {worker.HostName}");
    }

    private string GetEngineType()
        => _engine is IDistributedProcessEngine
            ? ProcessEngineType.Distributed.ToString()
            : ProcessEngineType.Simple.ToString();

    private void RequireDistributedEngine()
    {
        if (_engine is not IDistributedProcessEngine)
            throw new CliUsageException("This command requires ProcessEngine:Type=Distributed.");
    }

    private async Task ExecuteTriggerCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        RequireArguments(args, 2);
        switch (args[1].ToLowerInvariant())
        {
            case "create":
                RequireArguments(args, 4);
                var created = await _triggerService.CreateAsync(args[2], args[3], args.Length > 4 ? args[4] : null, cancellationToken);
                await _output.WriteLineAsync($"Trigger registered: {created.Trigger.Id}");
                await _output.WriteLineAsync($"Invoke path: {created.InvokePath}");
                await _output.WriteLineAsync($"Secret (store securely; shown once): {created.Secret}");
                break;
            case "list":
                foreach (var trigger in await _triggerService.ListAsync(args.Length > 2 ? args[2] : null, cancellationToken))
                    await _output.WriteLineAsync($"{trigger.Id}  {trigger.Name}  {trigger.ProcessDefinitionKey}  {(trigger.Enabled ? "enabled" : "disabled")}  invocations={trigger.InvocationCount}");
                break;
            case "invoke":
                RequireArguments(args, 4);
                var triggerId = ParseGuid(args[2]);
                Dictionary<string, object?>? variables = null;
                if (args.Length > 4)
                    variables = JsonSerializer.Deserialize<Dictionary<string, object?>>(args[4])
                        ?? throw new CliUsageException("Variables must be a JSON object.");
                var result = await _triggerService.InvokeAsync(triggerId, args[3], variables, args.Length > 5 ? args[5] : null, cancellationToken);
                if (result.Status != WorkflowTriggerInvocationStatus.Started)
                    throw new CliUsageException($"Trigger invocation failed: {result.Status}.");
                await _output.WriteLineAsync($"Process started: {result.ProcessInstance!.Id}");
                break;
            case "enable":
            case "disable":
                RequireArguments(args, 3);
                if (!await _triggerService.UpdateAsync(ParseGuid(args[2]), null, args[1].Equals("enable", StringComparison.OrdinalIgnoreCase), args.Length > 3 ? args[3] : null, cancellationToken))
                    throw new CliUsageException("Trigger not found.");
                await _output.WriteLineAsync($"Trigger {args[1].ToLowerInvariant()}d: {args[2]}");
                break;
            case "delete":
                RequireArguments(args, 3);
                if (!await _triggerService.DeleteAsync(ParseGuid(args[2]), args.Length > 3 ? args[3] : null, cancellationToken))
                    throw new CliUsageException("Trigger not found.");
                await _output.WriteLineAsync($"Trigger deleted: {args[2]}");
                break;
            default:
                throw new CliUsageException("Usage: trigger create <name> <process-key> [tenant] | list [tenant] | invoke <id> <secret> [variables-json] [business-key] | enable|disable <id> [tenant] | delete <id> [tenant]");
        }
    }

    private async Task ExecuteCredentialCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        RequireArguments(args, 2);
        switch (args[1].ToLowerInvariant())
        {
            case "create":
                RequireArguments(args, 5);
                var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(args[4]) ?? throw new CliUsageException("Secrets must be a JSON object.");
                var credential = await _credentialService.CreateAsync(TenantAt(args, 6), new(args[2], args[3], args.Length > 5 ? args[5] : null, secrets), cancellationToken);
                await _output.WriteLineAsync($"Credential created: {credential.Id}");
                break;
            case "list":
                foreach (var item in await _credentialService.ListAsync(TenantAt(args, 2), cancellationToken))
                    await _output.WriteLineAsync($"{item.Id}  {item.Name}  {item.Type}");
                break;
            case "rotate":
                RequireArguments(args, 5);
                if (!await _credentialService.RotateSecretAsync(TenantAt(args, 5), args[2], new(args[3], args[4]), cancellationToken))
                    throw new CliUsageException("Credential not found.");
                await _output.WriteLineAsync($"Credential secret rotated: {args[2]}");
                break;
            default: throw new CliUsageException("Usage: credential create <name> <type> <secrets-json> [description] [tenant] | list [tenant] | rotate <id> <key> <value> [tenant]");
        }
    }

    private async Task ExecuteConnectorCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        RequireArguments(args, 2);
        switch (args[1].ToLowerInvariant())
        {
            case "list":
                foreach (var item in await _connectorService.ListAsync(TenantAt(args, 2), cancellationToken))
                    await _output.WriteLineAsync($"{item.Id}  {item.Name}  {item.Type}  {(item.Enabled ? "enabled" : "disabled")}");
                break;
            case "create":
                RequireArguments(args, 4);
                var connector = await _connectorService.CreateAsync(TenantAt(args, 7), new(args[2], args[3], null, args.Length > 4 ? args[4] : null, args.Length > 5 ? args[5] : null, args.Length > 6 ? args[6] : null), cancellationToken);
                await _output.WriteLineAsync($"Connector created: {connector.Id}");
                break;
            case "test":
                RequireArguments(args, 3);
                var result = await _connectorService.TestAsync(TenantAt(args, 3), args[2], cancellationToken) ?? throw new CliUsageException("Connector not found.");
                await _output.WriteLineAsync($"Connector test: {(result.Success ? "success" : "failed")} - {result.Message}");
                break;
            case "import-openapi":
                RequireArguments(args, 3);
                var importTenant = TenantAt(args, 3);
                var openApiResult = _openApiImporter.Import(await ReadFileAsync(args[2]), importTenant);
                foreach (var template in openApiResult.Templates)
                    await _connectorTemplateService.CreateAsync(importTenant, template, cancellationToken);
                foreach (var item in openApiResult.Report)
                    await _output.WriteLineAsync($"{item.Disposition}: {item.OperationId} - {item.Message}");
                break;
            default: throw new CliUsageException("Usage: connector list [tenant] | create <name> <type> [endpoint] [credential-id] [template-id] [tenant] | test <id> [tenant] | import-openapi <spec.json> [tenant]");
        }
    }

    private async Task ExecuteTemplateCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length is > 1 && !args[1].Equals("list", StringComparison.OrdinalIgnoreCase))
            throw new CliUsageException("Usage: template list [tenant]");
        foreach (var template in await _connectorTemplateService.ListAsync(TenantAt(args, 2), cancellationToken))
            await _output.WriteLineAsync($"{template.Id}  {template.Name}  {template.Category}");
    }

    private async Task PrintValidationAsync(string bpmnXml)
    {
        var validation = _validationService.ValidateBpmn(bpmnXml);
        await _output.WriteLineAsync(validation.IsValid ? "BPMN is valid." : "BPMN is invalid.");
        foreach (var error in validation.Errors ?? []) await _output.WriteLineAsync($"Error: {error}");
        foreach (var warning in validation.Warnings ?? []) await _output.WriteLineAsync($"Warning: {warning}");
    }

    private static Guid ParseGuid(string value)
        => Guid.TryParse(value, out var id) ? id : throw new CliUsageException($"Invalid trigger id: {value}");

    private static string TenantAt(string[] args, int index) => args.Length > index && !string.IsNullOrWhiteSpace(args[index]) ? args[index] : "default";

    private async Task ExecuteConfigCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 1 || args[1].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var entry in await _dependencyRegistry.ListAsync(cancellationToken))
                await _output.WriteLineAsync($"{entry.Key}={entry.Value}");
            return;
        }

        switch (args[1].ToLowerInvariant())
        {
            case "get":
                RequireArguments(args, 3);
                var entry = await _dependencyRegistry.GetAsync(args[2], cancellationToken);
                if (entry is null)
                    throw new CliUsageException($"Configuration key not found: {args[2]}");
                await _output.WriteLineAsync(entry.Value);
                break;
            case "set":
                RequireArguments(args, 4);
                await _dependencyRegistry.SetAsync(args[2], args[3], cancellationToken);
                await _output.WriteLineAsync($"Configuration saved: {args[2]}");
                break;
            case "remove":
            case "delete":
                RequireArguments(args, 3);
                if (!await _dependencyRegistry.RemoveAsync(args[2], cancellationToken))
                    throw new CliUsageException($"Configuration key not found: {args[2]}");
                await _output.WriteLineAsync($"Configuration removed: {args[2]}");
                break;
            default:
                throw new CliUsageException("Usage: config list | get <key> | set <key> <value> | remove <key>");
        }
    }

    private async Task PrintTraceAsync(Task<List<string>> traceTask)
    {
        foreach (var line in await traceTask)
            await _output.WriteLineAsync(line);
    }

    private static async Task<string> ReadFileAsync(string path)
    {
        if (!File.Exists(path))
            throw new CliUsageException($"File not found: {path}");
        return await File.ReadAllTextAsync(path);
    }

    private static void RequireArguments(string[] args, int count)
    {
        if (args.Length < count)
            throw new CliUsageException("Missing arguments. Use 'help' for command syntax.");
    }

    internal static bool IsHelpRequest(string[] args)
        => args is ["--help"] or ["-h"] or ["help"];

    internal static void WriteHelp(TextWriter output)
    {
        output.WriteLine("VertexBPMN CLI");
        output.WriteLine("  execute <bpmn-file>                         Execute a BPMN file");
        output.WriteLine("  execute-id <process-id>                     Execute a registered BPMN process");
        output.WriteLine("  deploy-bpmn <bpmn-file> [tenant]            Persist BPMN for later execution or triggers");
        output.WriteLine("  deploy-dmn <dmn-file> [tenant]              Deploy a DMN decision table");
        output.WriteLine("  deploy-form <form-json> [tenant]            Deploy a form schema");
        output.WriteLine("  test-run <bpmn-file> <variables-json> [tenant] Deploy and start a test process");
        output.WriteLine("  validate <bpmn-file>                         Validate BPMN semantics");
        output.WriteLine("  import-n8n <workflow-json> [output-bpmn] [tenant] Convert an n8n workflow and print its import report");
        output.WriteLine("  register-bpmn <id> <bpmn-file>              Register BPMN");
        output.WriteLine("  register-cmmn <id> <cmmn-file>              Register CMMN");
        output.WriteLine("  register-dmn <id> <dmn-file>                Register DMN");
        output.WriteLine("  execute-case <cmmn-file>                    Execute a CMMN case");
        output.WriteLine("  status | pending | workers                  Inspect local runtime");
        output.WriteLine("  dashboard | studio                          Start API, Studio and browser");
        output.WriteLine("  config list                                List persisted configuration");
        output.WriteLine("  config get <key>                           Read persisted configuration");
        output.WriteLine("  config set <key> <value>                   Persist configuration value");
        output.WriteLine("  config remove <key>                        Remove persisted configuration");
        output.WriteLine("  trigger create <name> <process-key> [tenant]");
        output.WriteLine("  trigger list [tenant]                      List registered workflow triggers");
        output.WriteLine("  trigger invoke <id> <secret> [json] [key]  Start a workflow through a trigger");
        output.WriteLine("  trigger enable|disable <id> [tenant]");
        output.WriteLine("  trigger delete <id> [tenant]");
        output.WriteLine("  credential create|list|rotate ...           Manage credential metadata and secrets");
        output.WriteLine("  connector create|list|test ...              Manage and test connectors");
        output.WriteLine("  template list [tenant]                      List connector templates");
        output.WriteLine("  clear | help | exit                         REPL commands");
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        foreach (var character in input.Trim())
        {
            if (quote != '\0')
            {
                if (character == quote)
                    quote = '\0';
                else
                    current.Append(character);
            }
            else if (character is '\'' or '"')
                quote = character;
            else if (char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
                current.Append(character);
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }

    private sealed class CliUsageException(string message) : Exception(message);
}
