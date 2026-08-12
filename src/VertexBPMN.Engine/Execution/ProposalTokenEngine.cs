using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Application.Messaging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Infrastructure.Persistence.InMemory;

namespace VertexBPMN.Engine.Execution;


/// <summary>
/// ProposalTokenEngine
/// Facade that configures a <see cref="DistributedProcessEngine"/> with in-memory defaults
/// to provide a lightweight, offline-friendly process engine without duplicating execution logic.
/// </summary>
public sealed class ProposalTokenEngine : IProcessEngine, IDisposable
{
    private readonly ILogger<ProposalTokenEngine> _logger;
    private readonly DistributedProcessEngine _distributedEngine;
    private readonly IMessageDispatcher _messageDispatcher;
    private readonly IProcessInstanceStore _processInstanceStore;
    private bool _disposed;

    public ProposalTokenEngine(
        ILogger<ProposalTokenEngine> logger,
        IServiceTaskRegistry serviceTaskRegistry,
        IBpmnParser? bpmnParser = null,
        IDmnParser? dmnParser = null,
        IDmnEngine? dmnEngine = null,
        ICmmnParser? cmmnParser = null,
        IAiDecisionService? aiDecisionService = null,
        IMessageDispatcher? messageDispatcher = null,
        IProcessInstanceStore? processInstanceStore = null,
        TracerProvider? tracerProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(serviceTaskRegistry);

        var tracer = tracerProvider ?? TracerProvider.Default;
        var baseLogger = (ILogger)logger;

        var resolvedBpmnParser = bpmnParser ?? new BpmnParser( new BpmnParserOptions(), new ForwardingLogger<BpmnParser>(baseLogger), tracer);
        var resolvedDmnParser = dmnParser ?? new DmnParser(new ForwardingLogger<DmnParser>(baseLogger));
        var resolvedDmnEngine = dmnEngine ?? new DmnEngine(new ForwardingLogger<DmnEngine>(baseLogger));
        var resolvedCmmnParser = cmmnParser ?? new CmmnParser();
        var resolvedAiDecisionService = aiDecisionService ?? new FakeAiDecisionService();

        _messageDispatcher = messageDispatcher ?? new InMemoryMessageDispatcher(serviceTaskRegistry);
        _processInstanceStore = processInstanceStore ?? new InMemoryProcessInstanceStore();

        // Removed 'static' so the lambda can capture required variables (fixes CS8820/CS8821).
        _distributedEngine = EngineCache.GetOrCreate(
            serviceTaskRegistry,
            resolvedDmnEngine,
            resolvedDmnParser,
            resolvedCmmnParser,
            resolvedBpmnParser,
            resolvedAiDecisionService,
            tracer,
            () => new DistributedProcessEngine(
                new ForwardingLogger<DistributedProcessEngine>(baseLogger),
                serviceTaskRegistry,
                _messageDispatcher,
                _processInstanceStore,
                resolvedDmnEngine,
                resolvedDmnParser,
                resolvedCmmnParser,
                resolvedBpmnParser,
                resolvedAiDecisionService,
                tracer));

        _logger.LogInformation("ProposalTokenEngine initialized using DistributedProcessEngine facade");
    }

    public Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
        => _distributedEngine.ExecuteAsync(model, cancellationToken);

    public List<string> Execute(BpmnModel model)
        => _distributedEngine.Execute(model);

    public Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default)
        => _distributedEngine.ExecuteCaseAsync(model, cancellationToken);

    public Task<List<string>> ExecuteProcessAsync(string processId, CancellationToken cancellationToken = default)
        => _distributedEngine.ExecuteProcessAsync(processId, cancellationToken);

    public Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default)
        => _distributedEngine.CanExecuteAsync(nodeId, cancellationToken);

    public Task RegisterBpmnModelAsync(string processId, string bpmnXml, CancellationToken cancellationToken = default)
        => _distributedEngine.RegisterBpmnModelAsync(processId, bpmnXml, cancellationToken);

    public Task RegisterCmmnModelAsync(string caseId, string cmmnXml)
        => _distributedEngine.RegisterCmmnModelAsync(caseId, cmmnXml);

    public Task RegisterDmnModelAsync(string decisionId, string dmnXml)
        => _distributedEngine.RegisterDmnModelAsync(decisionId, dmnXml);

    public Task<CaseModel> GetCmmnModelAsync(string caseId)
        => _distributedEngine.GetCmmnModelAsync(caseId);

    public Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId)
        => _distributedEngine.GetHistoricalCaseDataAsync(caseId);

    public Task AddDiscretionaryItemAsync(string caseId, PlanItem planItem, CancellationToken cancellationToken = default)
        => _distributedEngine.AddDiscretionaryItemAsync(caseId, planItem, cancellationToken);

    public Task UpdateCaseFileItemAsync(string caseId, string caseFileItemId, object newValue, CancellationToken cancellationToken = default)
        => _distributedEngine.UpdateCaseFileItemAsync(caseId, caseFileItemId, newValue, cancellationToken);

    public Task TriggerUserEventAsync(string caseId, string eventId, Dictionary<string, object> eventData, CancellationToken cancellationToken = default)
        => _distributedEngine.TriggerUserEventAsync(caseId, eventId, eventData, cancellationToken);

    public Task GenerateAdHocSubprocessAsync(string caseId, CancellationToken cancellationToken = default)
        => _distributedEngine.GenerateAdHocSubprocessAsync(caseId, cancellationToken);

    public void Dispose()
    {
        if (_disposed) return;
        _distributedEngine.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private sealed class ForwardingLogger<T> : ILogger<T>
    {
        private readonly ILogger _inner;
        public ForwardingLogger(ILogger inner) => _inner = inner ?? NullLogger.Instance;
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => _inner.Log(logLevel, eventId, state, exception, formatter);
    }

    private static class EngineCache
    {
        private static readonly object _lock = new();
        private static DistributedProcessEngine? _singleton;
        private static IServiceTaskRegistry? _registry;
        private static IDmnEngine? _dmnEngine;
        private static IDmnParser? _dmnParser;
        private static ICmmnParser? _cmmnParser;
        private static IBpmnParser? _bpmnParser;

        public static DistributedProcessEngine GetOrCreate(
            IServiceTaskRegistry registry,
            IDmnEngine dmnEngine,
            IDmnParser dmnParser,
            ICmmnParser cmmnParser,
            IBpmnParser bpmnParser,
            IAiDecisionService aiDecisionService,
            TracerProvider tracer,
            Func<DistributedProcessEngine> factory)
        {
            if (_singleton != null &&
                ReferenceEquals(registry, _registry) &&
                ReferenceEquals(dmnEngine, _dmnEngine) &&
                ReferenceEquals(dmnParser, _dmnParser) &&
                ReferenceEquals(cmmnParser, _cmmnParser) &&
                ReferenceEquals(bpmnParser, _bpmnParser))
            {
                return _singleton;
            }

            lock (_lock)
            {
                if (_singleton != null &&
                    ReferenceEquals(registry, _registry) &&
                    ReferenceEquals(dmnEngine, _dmnEngine) &&
                    ReferenceEquals(dmnParser, _dmnParser) &&
                    ReferenceEquals(cmmnParser, _cmmnParser) &&
                    ReferenceEquals(bpmnParser, _bpmnParser))
                {
                    return _singleton;
                }

                _singleton = factory();
                _registry = registry;
                _dmnEngine = dmnEngine;
                _dmnParser = dmnParser;
                _cmmnParser = cmmnParser;
                _bpmnParser = bpmnParser;
                return _singleton;
            }
        }
    }
}