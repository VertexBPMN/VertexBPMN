using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Security;

namespace VertexBPMN.Tests.Parsing.Hardening;

// Absolute heap measurements must not overlap other collections. This changes
// scheduling, not the measured scope, assertions, or CI inclusion of the tests.
[CollectionDefinition("ProcessMemoryMeasurements", DisableParallelization = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public xUnit collection or fixture contract retained for framework activation and injection into public test constructors.")]
public sealed class ProcessMemoryMeasurementsCollection;
