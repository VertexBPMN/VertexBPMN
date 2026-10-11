using System.Reflection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Engine;

public sealed class TrackedExecutionTokenMergeTests
{
	[Fact]
	public void MergePreservesPersistedTokensAndAppendsEachNewIdentityOnce()
	{
		var persisted = new ExecutionToken { Id = Guid.NewGuid() };
		var localCopy = new ExecutionToken { Id = persisted.Id };
		var firstLocal = new ExecutionToken { Id = Guid.NewGuid() };
		var duplicateLocal = new ExecutionToken { Id = firstLocal.Id };
		var secondLocal = new ExecutionToken { Id = Guid.NewGuid() };
		var destination = new List<ExecutionToken> { persisted };

		Merge(destination, [localCopy, firstLocal, duplicateLocal, secondLocal]);

		Assert.Equal(3, destination.Count);
		Assert.Same(persisted, destination[0]);
		Assert.Same(firstLocal, destination[1]);
		Assert.Same(secondLocal, destination[2]);
	}

	[Fact]
	public void MergeHandlesEmptyCollectionsAndRepeatedCallsWithoutDuplicatingTokens()
	{
		var destination = new List<ExecutionToken>();
		Merge(destination, []);
		Assert.Empty(destination);
		var local = new ExecutionToken { Id = Guid.NewGuid() };
		Merge(destination, [local]);
		Merge(destination, [local]);
		Assert.Same(local, Assert.Single(destination));
	}

	private static void Merge(List<ExecutionToken> destination, IEnumerable<ExecutionToken> tracked)
	{
		var method = typeof(PersistentProcessExecutionRuntime).GetMethod("MergeTrackedTokens",
			BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);
		method.Invoke(null, [destination, tracked]);
	}
}
