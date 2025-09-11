using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Contracts;

namespace VertexBPMN.EngineServices.Handlers;

public class UserTaskServiceHandler : IServiceTaskHandler
{
    private readonly ILogger<UserTaskServiceHandler> _logger;

    public UserTaskServiceHandler(ILogger<UserTaskServiceHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
