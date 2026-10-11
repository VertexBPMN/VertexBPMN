using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.Operational;

internal sealed record DatabaseSchemaStatus(string Name, bool CanConnect, IReadOnlyList<string> PendingMigrations, string? Error = null);
