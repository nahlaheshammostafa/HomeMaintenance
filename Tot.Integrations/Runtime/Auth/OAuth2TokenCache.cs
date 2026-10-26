using System.Collections.Concurrent;

namespace Tot.Integrations.Runtime.Auth;

/// <summary>
/// Singleton cache for OAuth2 access tokens, keyed by connection id.
/// Handles single-flight refresh: only one concurrent fetch per connection
/// even when many requests race simultaneously.
/// </summary>
internal sealed class OAuth2TokenCache
{
    private sealed class Entry
    {
        public string? Token;
        public DateTimeOffset ExpiresAt;
        public readonly SemaphoreSlim Lock = new(1, 1);
    }

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    /// <summary>
    /// Returns a valid token for <paramref name="connectionId"/>, calling <paramref name="fetchAsync"/>
    /// when none is cached or fewer than 60 s remain before expiry.
    /// Only one caller fetches at a time; other callers wait on the semaphore.
    /// </summary>
    internal async Task<string> GetOrRefreshAsync(
        Guid connectionId,
        Func<CancellationToken, Task<(string Token, DateTimeOffset ExpiresAt)>> fetchAsync,
        CancellationToken ct)
    {
        var entry = _entries.GetOrAdd(connectionId, _ => new Entry());

        // Fast path: valid token available
        if (entry.Token is not null && entry.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60))
            return entry.Token;

        // Slow path: need to refresh; take the per-connection lock
        await entry.Lock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (entry.Token is not null && entry.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60))
                return entry.Token;

            var (token, expiresAt) = await fetchAsync(ct);
            entry.Token = token;
            entry.ExpiresAt = expiresAt;
            return token;
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    /// <summary>Forces the next call to re-fetch the token for this connection.</summary>
    internal void Invalidate(Guid connectionId)
    {
        if (_entries.TryGetValue(connectionId, out var entry))
            entry.Token = null;
    }
}
