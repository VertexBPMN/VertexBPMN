using VertexBPMN.Portal.Client.Bff;

namespace VertexBPMN.Tests.Unit.Portal;

/// <summary>
/// LC06 client task status projection: raw task lifecycle statuses normalize into the portal's
/// stable vocabulary on the client, so the UI never surfaces undocumented backend strings.
/// </summary>
public sealed class TaskStatusProjectorTests
{
    [Theory]
    [InlineData("created", "pending")]
    [InlineData("pending", "pending")]
    [InlineData("ready", "pending")]
    [InlineData("active", "active")]
    [InlineData("in_progress", "active")]
    [InlineData("claimed", "active")]
    [InlineData("assigned", "active")]
    [InlineData("running", "active")]
    [InlineData("completed", "completed")]
    [InlineData("done", "completed")]
    [InlineData("finished", "completed")]
    [InlineData("cancelled", "cancelled")]
    [InlineData("canceled", "cancelled")]
    [InlineData("withdrawn", "cancelled")]
    public void Normalize_maps_raw_statuses_to_controlled_vocabulary(string raw, string expected)
        => Assert.Equal(expected, TaskStatusProjector.Normalize(raw), StringComparer.Ordinal);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("someRandomBackendStatus")]
    public void Normalize_unknown_or_blank_raw_status_is_unknown(string? raw)
        => Assert.Equal("unknown", TaskStatusProjector.Normalize(raw), StringComparer.Ordinal);

    [Fact]
    public void Normalize_is_case_and_whitespace_insensitive()
    {
        Assert.Equal("active", TaskStatusProjector.Normalize("  In_Progress "), StringComparer.Ordinal);
        Assert.Equal("completed", TaskStatusProjector.Normalize("COMPLETED"), StringComparer.Ordinal);
    }
}
