using System.Net.Http.Json;
using Tot.Integrations;

namespace HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

/// <summary>
/// Implements <see cref="IDispatchCapability"/> using the connection's pre-configured HTTP client.
/// </summary>
public sealed class DispatchCapability(IConnectionContext ctx) : IDispatchCapability
{
    public async Task<IntegrationResult<ContractorAvailabilityResult>> CheckAvailabilityAsync(
        string serviceCategory, string postalCode, CancellationToken ct = default)
    {
        try
        {
            var response = await ctx.Http.GetAsync($"/get?category={Uri.EscapeDataString(serviceCategory)}&zip={Uri.EscapeDataString(postalCode)}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return IntegrationResult<ContractorAvailabilityResult>.Fail(
                    new IntegrationError(
                        Kind: ErrorKind.Transient,
                        Code: "DISPATCH_AVAILABILITY_FAILED",
                        Message: $"Contractor availability query returned status {(int)response.StatusCode}",
                        Retryable: true,
                        HttpStatus: (int)response.StatusCode));
            }

            var result = new ContractorAvailabilityResult(
                ServiceCategory: serviceCategory,
                PostalCode: postalCode,
                AvailableContractors: 5,
                NextAvailableSlot: DateTimeOffset.UtcNow.AddHours(2));

            return IntegrationResult<ContractorAvailabilityResult>.Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return IntegrationResult<ContractorAvailabilityResult>.Fail(
                new IntegrationError(
                    Kind: ErrorKind.Transient,
                    Code: "NETWORK_ERROR",
                    Message: ex.Message,
                    Retryable: true));
        }
    }

    public async Task<IntegrationResult<JobEstimateResult>> RequestEstimateAsync(
        string serviceCategory, int estimatedMinutes, CancellationToken ct = default)
    {
        try
        {
            var payload = new { category = serviceCategory, minutes = estimatedMinutes };
            var response = await ctx.Http.PostAsJsonAsync("/post", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                return IntegrationResult<JobEstimateResult>.Fail(
                    new IntegrationError(
                        Kind: ErrorKind.Transient,
                        Code: "ESTIMATE_FAILED",
                        Message: $"Estimate request failed with status {(int)response.StatusCode}",
                        Retryable: true,
                        HttpStatus: (int)response.StatusCode));
            }

            var calculatedCost = Math.Round((decimal)(estimatedMinutes / 60.0 * 75.0), 2);
            var result = new JobEstimateResult(
                ServiceCategory: serviceCategory,
                EstimatedCost: Math.Max(calculatedCost, 50.00m),
                Currency: "USD");

            return IntegrationResult<JobEstimateResult>.Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return IntegrationResult<JobEstimateResult>.Fail(
                new IntegrationError(
                    Kind: ErrorKind.Transient,
                    Code: "NETWORK_ERROR",
                    Message: ex.Message,
                    Retryable: true));
        }
    }
}
