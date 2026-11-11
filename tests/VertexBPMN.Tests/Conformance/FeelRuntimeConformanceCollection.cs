namespace VertexBPMN.Tests.Conformance;

/// <summary>
/// Runs wall-clock-bounded FEEL conformance without CPU contention from other test collections.
/// The production execution deadline and all conformance assertions remain in force.
/// </summary>
[CollectionDefinition("FeelRuntimeConformance", DisableParallelization = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public xUnit collection contract retained for framework discovery.")]
public sealed class FeelRuntimeConformanceCollection;
