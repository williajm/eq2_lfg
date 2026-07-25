namespace Eq2Lfg.Core.Models;

/// <summary>A single parsed line of the EQ2 chat log.</summary>
public sealed record ChatMessage
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Speaker { get; init; }

    /// <summary>Channel/context, e.g. "LFG", "General", "guild", "say", "tell".</summary>
    public required string Channel { get; init; }

    public required string Text { get; init; }
    public required string Raw { get; init; }
}
