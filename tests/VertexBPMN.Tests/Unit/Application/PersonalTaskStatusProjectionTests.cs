using VertexBPMN.Application.PersonalTasks;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class PersonalTaskStatusProjectionTests
{
	[Theory]
	[InlineData(UserTaskStatus.Pending, "pending")]
	[InlineData(UserTaskStatus.Delegated, "delegated")]
	[InlineData(UserTaskStatus.Completed, "completed")]
	[InlineData(UserTaskStatus.Rejected, "rejected")]
	[InlineData(UserTaskStatus.Cancelled, "cancelled")]
	public void Of_MapsKnownStatesIntoControlledStrings(UserTaskStatus status, string expected)
	{
		Assert.Equal(expected, PersonalTaskStatus.Of(status));
		Assert.True(PersonalTaskStatus.IsControlled(expected));
	}

	[Fact]
	public void UnknownState_ProjectsToUnknownAndIsNotControlled()
	{
		var mapped = PersonalTaskStatus.Of((UserTaskStatus)999);
		Assert.Equal("unknown", mapped);
		Assert.False(PersonalTaskStatus.IsControlled(mapped));
	}

	[Fact]
	public void ControlledSet_ContainsNoProcessVariableData()
	{
		// Requirement: the status projection exposes only a controlled status surface,
		// never raw process variables or the internal enum value set.
		var controlled = PersonalTaskStatus.Statuses;
		Assert.DoesNotContain("variables", controlled, StringComparer.OrdinalIgnoreCase);
	}
}
