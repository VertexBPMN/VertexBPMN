using Microsoft.Data.Sqlite;

namespace VertexBPMN.Tests.Infrastructure;

internal sealed class TestTransactionScope(SharedSqliteDbFixture fixture) : IAsyncLifetime
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213", Justification = "Borrowed xUnit collection fixture; xUnit owns and disposes it after all collection tests, not this transaction scope.")]
	private readonly SharedSqliteDbFixture _fixture = fixture;
	private SqliteTransaction? _tx;

	public async ValueTask InitializeAsync()
	{
		_tx = (SqliteTransaction)await _fixture.Connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if (_tx is null)
		{
			return;
		}

		try
		{
			// Cleanup must also complete after the test's cancellation token is cancelled.
			await _tx.RollbackAsync(CancellationToken.None);
		}
		finally
		{
			await _tx.DisposeAsync();
			_tx = null;
		}
	}
}
