using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Interfaces;
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

        var resolvedBpmnParser = bpmnParser ?? new BpmnParser(new ForwardingLogger<BpmnParser>(baseLogger), tracer);
        var resolvedDmnParser = dmnParser ?? new DmnParser(new ForwardingLogger<DmnParser>(baseLogger));
        var resolvedDmnEngine = dmnEngine ?? new DmnEngine(new ForwardingLogger<DmnEngine>(baseLogger));
        var resolvedCmmnParser = cmmnParser ?? new CmmnParser();
        var resolvedAiDecisionService = aiDecisionService ?? new FakeAiDecisionService();

        _messageDispatcher = messageDispatcher ?? new InMemoryMessageDispatcher(serviceTaskRegistry);
        _processInstanceStore = processInstanceStore ?? new InMemoryProcessInstanceStore();

        _distributedEngine = new DistributedProcessEngine(
            new ForwardingLogger<DistributedProcessEngine>(baseLogger),
            serviceTaskRegistry,
            _messageDispatcher,
            _processInstanceStore,
            resolvedDmnEngine,
            resolvedDmnParser,
            resolvedCmmnParser,
            resolvedBpmnParser,
            resolvedAiDecisionService,
            tracer);

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _distributedEngine.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private sealed class ForwardingLogger<T> : ILogger<T>
    {
        private readonly ILogger _inner;

        public ForwardingLogger(ILogger inner)
        {
            _inner = inner ?? NullLogger.Instance;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
