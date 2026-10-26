using System.Net.Http.Json;
using System.Text.Json;
using Tot.Integrations;

namespace HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

/// <summary>
/// Handles asynchronous dispatch of bookings to external technicians via transactional outbox.
/// Operation: "booking.dispatch_technician"
/// </summary>
public sealed class DispatchTechnicianCommandHandler : ICommandHandler
{
    public string Operation => "booking.dispatch_technician";

    public async Task<IntegrationResult<JsonElement>> ExecuteAsync(
        JsonElement payload, IConnectionContext ctx, CancellationToken ct)
    {
        try
        {
            var response = await ctx.Http.PostAsJsonAsync("/post", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                return IntegrationResult<JsonElement>.Fail(
                    new IntegrationError(
                        Kind: ErrorKind.Transient,
                        Code: "DISPATCH_FAILED",
                        Message: $"Vendor dispatch endpoint returned HTTP {(int)response.StatusCode}",
                        Retryable: true,
                        HttpStatus: (int)response.StatusCode));
            }

            var vendorBookingId = "VND-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var result = JsonSerializer.SerializeToElement(new
            {
                dispatchStatus = "ACCEPTED",
                vendorBookingId = vendorBookingId,
                estimatedArrivalMinutes = 45,
                assignedAt = DateTimeOffset.UtcNow
            });

            return IntegrationResult<JsonElement>.Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return IntegrationResult<JsonElement>.Fail(
                new IntegrationError(
                    Kind: ErrorKind.Transient,
                    Code: "DISPATCH_NETWORK_ERROR",
                    Message: ex.Message,
                    Retryable: true));
        }
    }
}
