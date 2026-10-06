using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskRecoveryResult(int Examined, int Transitioned, int Conflicts);
