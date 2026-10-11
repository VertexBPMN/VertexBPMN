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

internal sealed record BenchmarkThresholds(double ValidResultRate, double ExpectedCategoryRecall,
	double CategoryPrecision, double CriticalFindingRecall, double GroundedQuoteRate,
	double HumanReviewRate, int HumanReviewBypassViolations, long P95RuntimeMilliseconds);
