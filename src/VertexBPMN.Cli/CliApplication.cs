using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.Configuration;
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
        _dashboardLauncher = services.GetRequiredService<DashboardLauncher>();
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
            return await RunInteractiveAsync(cancellationToken);
        if (args is ["--help"] or ["-h"])
        {
            PrintHelp();
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
                PrintHelp();
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
                    PrintHelp();
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

    private static Guid ParseGuid(string value)
        => Guid.TryParse(value, out var id) ? id : throw new CliUsageException($"Invalid trigger id: {value}");

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

    private void PrintHelp()
    {
        _output.WriteLine("VertexBPMN CLI");
        _output.WriteLine("  execute <bpmn-file>                         Execute a BPMN file");
        _output.WriteLine("  execute-id <process-id>                     Execute a registered BPMN process");
        _output.WriteLine("  deploy-bpmn <bpmn-file> [tenant]            Persist BPMN for later execution or triggers");
        _output.WriteLine("  register-bpmn <id> <bpmn-file>              Register BPMN");
        _output.WriteLine("  register-cmmn <id> <cmmn-file>              Register CMMN");
        _output.WriteLine("  register-dmn <id> <dmn-file>                Register DMN");
        _output.WriteLine("  execute-case <cmmn-file>                    Execute a CMMN case");
        _output.WriteLine("  status | pending | workers                  Inspect local runtime");
        _output.WriteLine("  dashboard | studio                          Start API, Studio and browser");
        _output.WriteLine("  config list                                List persisted configuration");
        _output.WriteLine("  config get <key>                           Read persisted configuration");
        _output.WriteLine("  config set <key> <value>                   Persist configuration value");
        _output.WriteLine("  config remove <key>                        Remove persisted configuration");
        _output.WriteLine("  trigger create <name> <process-key> [tenant]");
        _output.WriteLine("  trigger list [tenant]                      List registered workflow triggers");
        _output.WriteLine("  trigger invoke <id> <secret> [json] [key]  Start a workflow through a trigger");
        _output.WriteLine("  trigger enable|disable <id> [tenant]");
        _output.WriteLine("  trigger delete <id> [tenant]");
        _output.WriteLine("  clear | help | exit                         REPL commands");
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
