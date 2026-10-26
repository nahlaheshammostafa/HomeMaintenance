namespace Tot.Integrations;

/// <summary>
/// Handles an <see cref="IntegrationEvent"/> of a specific type.
/// Register implementations via <c>services.AddScoped&lt;IIntegrationEventHandler&lt;IntegrationEvent&gt;, MyHandler&gt;()</c>
/// or through a provider's <c>AddXyz()</c> extension.
/// </summary>
public interface IIntegrationEventHandler
{
    /// <summary>
    /// Returns <see langword="true"/> when this handler can process the given <paramref name="event"/>.
    /// The runtime calls <see cref="HandleAsync"/> only when this returns <see langword="true"/>.
    /// </summary>
    bool CanHandle(IntegrationEvent @event);

    /// <summary>Processes the event. Must be idempotent.</summary>
    Task HandleAsync(IntegrationEvent @event, CancellationToken ct);
}
