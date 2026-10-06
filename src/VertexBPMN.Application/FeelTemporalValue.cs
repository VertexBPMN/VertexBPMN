using System.Globalization;
using System.Text.Json;
using Jint;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Application;

internal sealed record FeelTemporalValue(string Kind, string Value);
