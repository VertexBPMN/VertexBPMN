using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PostgresTestDatabaseCommandsTests
{
	[Theory]
	[InlineData("CREATE DATABASE \"p4_test_0123\"")]
	[InlineData("DROP DATABASE \"p4_test_0123\" WITH (FORCE)")]
	[InlineData("DROP DATABASE IF EXISTS \"p4_test_0123\" WITH (FORCE)")]
	public void ValidatedCommandsAllowIsolatedDatabaseOperations(string commandText) =>
		PostgresTestDatabaseCommands.Validate(commandText);

	[Theory]
	[InlineData("CREATE DATABASE \"test\"; DROP DATABASE postgres")]
	[InlineData("DROP DATABASE \"test\" WITH (FORCE)\n")]
	[InlineData("CREATE DATABASE \"test-name\"")]
	[InlineData("SELECT 1")]
	[InlineData("CREATE DATABASE \"test\" WITH (FORCE)")]
	public void ArbitrarySqlAndUnsafeIdentifiersAreRejected(string commandText) =>
		Assert.Throws<ArgumentException>(() => PostgresTestDatabaseCommands.Validate(commandText));
}
