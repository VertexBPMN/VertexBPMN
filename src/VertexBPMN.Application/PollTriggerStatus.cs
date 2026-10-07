using System.Text.Json;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

public enum PollTriggerStatus { Idle, Started, Failed }
