namespace VertexBPMN.Tests.Integration.Api;

// xUnit verlangt eine PUBLIC Collection-Definition (xUnit1027); die Store-Fixture selbst bleibt internal.
#pragma warning disable CA1515, RCS1251 // xUnit erfordert public; leerer Typkörper
[CollectionDefinition("Lc10PostgresReleaseAcceptance")]
public sealed class Lc10PostgresReleaseAcceptanceCollection
    : ICollectionFixture<Lc10ReleaseActivationAcceptanceTests.Lc10PostgresSharedStore>
{
}
#pragma warning restore CA1515, RCS1251
