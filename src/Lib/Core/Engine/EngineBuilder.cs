#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using System.Xml.Linq;
using VertexBPMN.Domain;
using VertexBPMN.Domain.Contracts;
using VertexBPMN.EngineServices;
using VertexBPMN.EngineServices;

namespace VertexBPMN.Core.Engine;

public sealed class EngineBuilder
{
    private bool _useInMemory = false;
    private bool _useDistributed = false;
    private readonly IServiceCollection _services = new ServiceCollection();

    public EngineBuilder()
    {
        _services.AddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);
        _services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        _services.AddSingleton(TracerProvider.Default);
    }

    public EngineBuilder UseInMemoryStorage()
    {
        _useInMemory = true;
        return this;
    }

    public EngineBuilder UseDistributedExecution()
    {
        _useDistributed = true;
        _useInMemory = true; // still need basic in‑memory repo/runtime for definition + instance metadata
        return this;
    }

    public EngineBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
       
        configure?.Invoke(_services);
        return this;
    }

    public Task<IBpmnEngine> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (_useInMemory)
            RegisterInMemoryCore();

        // Always supply (dummy) BPMN parser; if distributed we replace with real
        if (!_useDistributed)
            _services.AddSingleton<IBpmnParser, BpmnParser>();

        if (_useDistributed)
            RegisterDistributedLayer();

        var provider = _services.BuildServiceProvider(validateScopes: false);

        var engine = _useDistributed
                ? provider.GetRequiredService<IBpmnEngine>()
                : null;

        return Task.FromResult(engine);
    }

    private void RegisterInMemoryCore()
    {
        _services.AddVertexEngineServices(_useInMemory);
    }

    private void RegisterDistributedLayer()
    {
        _services.AddLogging();
        _services.AddSingleton<IBpmnParser, BpmnParser>();
        _services.AddSingleton<IDmnParser, DmnParser>();
        _services.AddSingleton<ICmmnParser, CmmnParser>();
        _services.AddSingleton<IDmnEngine, DmnEngine>();
        _services.AddSingleton<IDistributedTokenEngine, DistributedTokenEngine>();
        _services.AddSingleton<IBpmnEngine, BpmnEngine>();
 
    }

}
