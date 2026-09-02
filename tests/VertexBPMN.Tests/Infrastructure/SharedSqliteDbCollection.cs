namespace VertexBPMN.Tests.Infrastructure;

[CollectionDefinition("SharedSqliteDb")]
public sealed class SharedSqliteDbCollection : ICollectionFixture<SharedSqliteDbFixture>
{
    // Intentionally empty. xUnit uses this for fixture scoping.
}

[CollectionDefinition("ApiTestCollection")]
public class ApiTestCollection : ICollectionFixture<CustomWebApplicationFactory> { }

[Collection("IntegratedApi")]
public class ProcessTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProcessTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }
}

[CollectionDefinition("IntegratedApi")]
public class IntegratedApiCollection :
    ICollectionFixture<SharedSqliteDbFixture>,
    ICollectionFixture<CustomWebApplicationFactory>
{ }