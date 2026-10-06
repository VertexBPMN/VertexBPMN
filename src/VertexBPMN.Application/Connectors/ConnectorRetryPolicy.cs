using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Connectors;

public sealed record ConnectorRetryPolicy(int MaxAttempts = 3, TimeSpan? Timeout = null, TimeSpan? InitialDelay = null)
{
	public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(30);
	public TimeSpan EffectiveDelay => InitialDelay ?? TimeSpan.FromMilliseconds(250);
}
