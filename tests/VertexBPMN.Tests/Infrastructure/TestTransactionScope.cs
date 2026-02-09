using Microsoft.Data.Sqlite;

namespace VertexBPMN.Tests.Infrastructure;

public sealed class TestTransactionScope : IAsyncLifetime
{
    private readonly SharedSqliteDbFixture _fixture;
    private SqliteTransaction? _tx;

    public TestTransactionScope(SharedSqliteDbFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync()
    {
        _tx = _fixture.Connection.BeginTransaction();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _tx?.Rollback();
        _tx?.Dispose();
        return ValueTask.CompletedTask;
    }
}