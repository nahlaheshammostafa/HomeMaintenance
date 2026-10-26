namespace Tot.Integrations;

/// <summary>
/// Dispatches pending commands from the outbox.
/// The default implementation is a <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> that polls the DB.
/// Replace by registering a custom implementation via <c>IIntegrationsBuilder.UseDispatcher&lt;T&gt;()</c>
/// (e.g. a MassTransit-based consumer).
/// </summary>
public interface IIntegrationDispatcher
{
    /// <summary>
    /// Forces an immediate dispatch cycle, processing any due commands.
    /// Useful in tests and after enqueuing urgent commands.
    /// </summary>
    Task DispatchNowAsync(CancellationToken ct);
}
