using Tot.Integrations;

namespace HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

/// <summary>
/// Domain model representing contractor availability in an area.
/// </summary>
public sealed record ContractorAvailabilityResult(
    string ServiceCategory,
    string PostalCode,
    int AvailableContractors,
    DateTimeOffset NextAvailableSlot);

/// <summary>
/// Domain model representing a vendor job cost estimate.
/// </summary>
public sealed record JobEstimateResult(
    string ServiceCategory,
    decimal EstimatedCost,
    string Currency);

/// <summary>
/// Direct RPC capability for interacting with external contractor dispatch networks.
/// </summary>
public interface IDispatchCapability
{
    Task<IntegrationResult<ContractorAvailabilityResult>> CheckAvailabilityAsync(
        string serviceCategory, string postalCode, CancellationToken ct = default);

    Task<IntegrationResult<JobEstimateResult>> RequestEstimateAsync(
        string serviceCategory, int estimatedMinutes, CancellationToken ct = default);
}
