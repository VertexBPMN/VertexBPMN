namespace VertexBPMN.Tests.Infrastructure;

[CollectionDefinition("SharedSqliteDb")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public xUnit collection or fixture contract retained for framework activation and injection into public test constructors.")]
public sealed class SharedSqliteDbCollection : ICollectionFixture<SharedSqliteDbFixture>
{
	// Intentionally empty. xUnit uses this for fixture scoping.
}
