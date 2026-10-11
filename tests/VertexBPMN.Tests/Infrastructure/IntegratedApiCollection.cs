namespace VertexBPMN.Tests.Infrastructure;

[CollectionDefinition("IntegratedApi")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public xUnit collection or fixture contract retained for framework activation and injection into public test constructors.")]
public class IntegratedApiCollection :
	ICollectionFixture<SharedSqliteDbFixture>,
	ICollectionFixture<CustomWebApplicationFactory>;
