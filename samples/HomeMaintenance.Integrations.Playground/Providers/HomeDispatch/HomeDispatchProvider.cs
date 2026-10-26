using System.Diagnostics;
using Tot.Integrations;

namespace HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

/// <summary>
/// Provider declaration for the Home Maintenance contractor dispatch network.
/// </summary>
public sealed class HomeDispatchProvider : IIntegrationProvider
{
    public ProviderDescriptor Descriptor { get; } = new(
        Code: "homedispatch",
        DisplayName: "Home Maintenance Dispatch Network",
        Category: "contractor-dispatch",
        SettingsType: typeof(HomeDispatchSettings),
        Auth: AuthScheme.ApiKey,
        Capabilities: [typeof(IDispatchCapability)],
        SupportsWebhooks: true,
        SupportsPolling: false);

    public async Task<IntegrationResult<HealthInfo>> PingAsync(
        IConnectionContext ctx, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await ctx.Http.GetAsync("/status/200", ct);
            sw.Stop();

            return response.IsSuccessStatusCode
                ? IntegrationResult<HealthInfo>.Ok(new HealthInfo(sw.Elapsed))
                : IntegrationResult<HealthInfo>.Fail(
                    new IntegrationError(
                        Kind: ErrorKind.Transient,
                        Code: "PING_FAILED",
                        Message: $"Ping returned HTTP {(int)response.StatusCode}",
                        Retryable: true,
                        HttpStatus: (int)response.StatusCode));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return IntegrationResult<HealthInfo>.Fail(
                new IntegrationError(
                    Kind: ErrorKind.Transient,
                    Code: "PING_UNREACHABLE",
                    Message: ex.Message,
                    Retryable: true));
        }
    }
}
