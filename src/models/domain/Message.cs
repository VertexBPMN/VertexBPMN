using System.Collections.Generic;

namespace VertexBPMN.Domain;

public record Message(string Name, string Payload,  Dictionary<string, object> Variables);