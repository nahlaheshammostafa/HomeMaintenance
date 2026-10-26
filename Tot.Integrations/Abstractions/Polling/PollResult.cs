namespace Tot.Integrations;

/// <summary>The result of one poll cycle from a <see cref="IPollingSource"/>.</summary>
/// <param name="Events">Integration events produced during this poll.</param>
/// <param name="NextCursor">
/// Opaque cursor to pass to the next poll call.
/// <see langword="null"/> means the cursor did not advance (no new data).
/// </param>
public sealed record PollResult(
    IReadOnlyList<IntegrationEvent> Events,
    string? NextCursor = null);
