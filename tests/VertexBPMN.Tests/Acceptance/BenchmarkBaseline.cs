using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using VertexBPMN.AgentWorker;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Acceptance;

internal sealed record BenchmarkBaseline(string Runtime, string Model, string ModelDigest,
	string PromptVersion, string ResultSchemaVersion, double Temperature, bool Thinking,
	int MaximumRuntimeSecondsPerDocument);
