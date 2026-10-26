using Microsoft.Extensions.Logging;
using Tot.Integrations;

namespace HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

/// <summary>
/// Domain event handler reacting to verified inbound webhook events from HomeDispatch.
/// </summary>
public sealed class HomeDispatchEventHandler(ILogger<HomeDispatchEventHandler> logger) : IIntegrationEventHandler
{
    public bool CanHandle(IntegrationEvent @event) =>
        @event.ProviderCode == "homedispatch";

    public Task HandleAsync(IntegrationEvent @event, CancellationToken ct)
    {
        logger.LogInformation(
            "====> [INTEGRATION DOMAIN EVENT HANDLED] Type: {Type}, EventId: {EventId}, Connection: {ConnectionId}\nPayload: {Payload}",
            @event.Type,
            @event.ProviderEventId,
            @event.ConnectionId,
            @event.Data.GetRawText());

        // In production integration with HomeMaintenance:
        // switch (@event.Type)
        // {
        //     case "technician.en_route": update booking status to EnRoute; break;
        //     case "technician.arrived": update booking status to InProgress; break;
        //     case "job.completed": update booking status to Completed; break;
        // }

        return Task.CompletedTask;
    }
}
