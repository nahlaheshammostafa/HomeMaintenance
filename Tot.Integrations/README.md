# Tot.Integrations

A generic .NET 9 integration engine for ASP.NET Core. Provides the runtime infrastructure
for connecting to external systems — HTTP transport, auth, resilience, command outbox,
webhook ingestion, polling, distributed tracing, and metrics — without hard-coding any
vendor-specific logic inside the package. Providers are implemented outside the package
and registered via a fluent builder.

---

## Installation

```bash
dotnet add package Tot.Integrations
```

**nuget.config** — add the TOTPLATFORM GitHub Packages feed:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="totplatform" value="https://nuget.pkg.github.com/TOTPLATFORM/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <totplatform>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_GITHUB_PAT" />
    </totplatform>
  </packageSourceCredentials>
</configuration>
```

The PAT needs `read:packages` scope.

---

## Quick start

```csharp
// Program.cs
builder.Services
    .AddIntegrations(builder.Configuration)
    .AddAcme()                              // provider extension from your project
    .UsePostgres(connectionString)
    .UseDataProtectionSecretStore();

await app.MigrateIntegrationsAsync();
app.MapIntegrationWebhooks("/integrations/webhooks");
app.MapIntegrationAdminApi("/api/admin/integrations")
   .RequireAuthorization("Integrations.Manage");
```

---

## `AddIntegrations` options

`services.AddIntegrations(configuration, options => { ... })` accepts an
`IntegrationsOptions` delegate. The most common options:

| Option | Default | Description |
|---|---|---|
| `RunBackgroundWorkers` | `true` | Set to `false` on hosts that only enqueue commands (no dispatch). |
| `WebhookProcessorBatchSize` | 50 | Rows claimed per WebhookProcessor cycle. |
| `WebhookProcessorInterval` | 5 s | Delay between WebhookProcessor sweeps. |
| `CommandDispatcherInterval` | 3 s | Delay between CommandDispatcher sweeps. |
| `PollingInterval` | 60 s | Default polling interval (overridable per source). |

Chain fluent calls on the returned `IIntegrationsBuilder`:

| Method | Purpose |
|---|---|
| `.AddProvider<T>()` | Register a provider (`IIntegrationProvider`). |
| `.AddCapability<TInterface, TImpl>()` | Map a capability interface to an implementation. |
| `.AddTypedClient<TInterface, TImpl>()` | Register a typed HTTP client for the provider. |
| `.AddCommandHandler<T>()` | Register an `ICommandHandler` for an operation name. |
| `.AddWebhookHandler<T>()` | Register an `IWebhookHandler` for a provider. |
| `.AddPollingSource<T>()` | Register an `IPollingSource` for a provider. |
| `.AddRedaction(patterns)` | Add JSON path / header name patterns to redact in the journal. |
| `.UseSecretStore<T>()` | Replace the default secret store with a custom `ISecretStore`. |
| `.UseDispatcher<T>()` | Replace the background dispatcher with a custom `IIntegrationDispatcher`. |
| `.UsePostgres(cs, schema)` | Set the PostgreSQL connection string and schema (default: `"integrations"`). |
| `.UseDataProtectionSecretStore()` | Use ASP.NET Core Data Protection to encrypt secrets at rest. |

---

## Configuration keys

All keys live under `Integrations:` in your `appsettings.json`:

```json
{
  "Integrations": {
    "RunBackgroundWorkers": true,
    "CommandDispatcherIntervalSeconds": 3,
    "WebhookProcessorIntervalSeconds": 5,
    "PollingIntervalSeconds": 60,
    "WebhookProcessorBatchSize": 50,
    "Providers": {
      "acme": {
        "BaseUrl": "https://api.acme.example.com",
        "TimeoutSeconds": 30
      }
    }
  }
}
```

Provider-specific defaults in `Integrations:Providers:{providerCode}` are merged with
per-connection settings stored in the database.

---

## Per-connection runtime settings

The following keys in a connection's `Settings` JSON object control the built-in
runtime behaviour for that connection. All are optional and fall back to the defaults
listed below.

| Key | Type | Default | Description |
|---|---|---|---|
| `rateLimitPerSecond` | `int` | `10` | Token-bucket refill rate (requests per second). The burst capacity is capped at `max(2×rate, 20)`. |
| `breakDurationSeconds` | `int` | `30` | Circuit-breaker open duration in seconds after the failure threshold is reached. Minimum `1`. |
| `timeoutSeconds` | `int` | `30` | Per-request timeout in seconds. Set to `0` to disable the timeout. |

Example — throttle a slow provider to 2 rps and open the circuit for 60 s:

```json
{
  "rateLimitPerSecond": 2,
  "breakDurationSeconds": 60,
  "timeoutSeconds": 15
}
```

---

## Secret store

Each connection has a `SecretRef` that points to an encrypted blob (API key, OAuth2
credentials, etc.). Two implementations ship:

| Store | Registration | Use when |
|---|---|---|
| Data Protection (default) | `.UseDataProtectionSecretStore()` | Single-host or shared-key Data Protection ring. |
| In-memory (tests only) | `.UseSecretStore<MemorySecretStore>()` | Unit / integration tests. |
| Custom | `.UseSecretStore<MyVaultStore>()` | Azure Key Vault, AWS Secrets Manager, etc. |

Implement `ISecretStore`:

```csharp
public interface ISecretStore
{
    Task SetAsync(string secretRef, string value, CancellationToken ct);
    Task<string?> GetAsync(string secretRef, CancellationToken ct);
    Task RotateAsync(string secretRef, CancellationToken ct);
    Task DeleteAsync(string secretRef, CancellationToken ct);
}
```

---

## Database migrations

The package ships EF Core migrations targeting PostgreSQL. Apply them on startup:

```csharp
await app.MigrateIntegrationsAsync();
```

Or add them to your migrator host:

```csharp
await using var scope = host.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
await db.Database.MigrateAsync();
```

The default schema is `integrations`. Change it with `.UsePostgres(cs, schema: "my_schema")`.

**Tables created:** `connections`, `secrets`, `commands`, `inbound_webhooks`,
`poll_cursors`, `id_maps`, `journal`.

---

## Webhook URL format

```
POST {prefix}/{providerCode}/{webhookKey}
```

Default prefix: `/integrations/webhooks`. Example:

```
POST https://your-host.example/integrations/webhooks/acme/wk_abc123xyz
```

`webhookKey` is returned when you create a connection via the admin API. Register it with
the upstream provider as the target URL for their outbound webhook.

---

## Admin API

| Method | Path | Purpose |
|---|---|---|
| `POST` | `{prefix}/connections` | Create a connection (stores secret, returns `webhookKey`). |
| `GET` | `{prefix}/connections` | List all connections (optionally filter by `providerCode`, `ownerType`, `ownerId`). |
| `GET` | `{prefix}/connections/{id}` | Get one connection. |
| `PATCH` | `{prefix}/connections/{id}` | Update a connection's settings or name. |
| `DELETE` | `{prefix}/connections/{id}` | Delete a connection and its secret. |
| `POST` | `{prefix}/connections/{id}/test` | Test a connection (calls `PingAsync` on the provider). |

Map with optional authorization:

```csharp
app.MapIntegrationAdminApi("/api/admin/integrations")
   .RequireAuthorization("Integrations.Manage");
```

---

## Health checks and metrics

### Health check

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<IntegrationsHealthCheck>("integrations");
```

Reports `Healthy` when the DB is reachable and the background workers are running.

### OpenTelemetry metrics

Instrument name prefix: `integration_`

| Instrument | Type | Tags |
|---|---|---|
| `integration_requests_total` | Counter | `provider`, `method`, `outcome` |
| `integration_request_duration_ms` | Histogram | `provider`, `method`, `outcome` |
| `integration_commands_succeeded_total` | Counter | — |
| `integration_commands_deadlettered_total` | Counter | — |
| `integration_webhooks_total` | Counter | `provider`, `outcome` |
| `integration_poll_cycles_total` | Counter | `provider`, `outcome` |
| `integration_journal_dropped_total` | Counter | — |

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("Tot.Integrations"))
    .WithTracing(t => t.AddSource("Tot.Integrations"));
```

---

## Writing an integration — step-by-step

Use the Acme template below as a starting point. The provider project lives **outside
this package** (e.g. `src/Integrations/YourProduct.Integrations.Acme/`).

### 7-step checklist

1. **Define settings** — a plain record or class with the fields the operator fills in
   (base URL, region, etc.). This is the `SettingsType` in `ProviderDescriptor`.

2. **Define a typed client** (optional but recommended) — an interface + implementation
   that wraps the raw HTTP calls. The engine injects `IConnectionContext` for auth tokens,
   base URL, and resilience policy.

3. **Declare the provider** — implement `IIntegrationProvider` with a `ProviderDescriptor`
   listing the auth scheme, capabilities, webhook and polling support.

4. **Implement capabilities** — one class per `IYourCapability` interface. Each method
   calls the typed client through the provided `IConnectionContext`.

5. **Implement command handlers** (if the provider supports async operations) — one
   `ICommandHandler` per operation. The engine retries and dead-letters automatically.

6. **Implement a webhook handler** (if the provider sends webhooks) — parse raw bytes into
   `IntegrationEvent` records; the engine dispatches them to your `IIntegrationEventHandler`.

7. **Implement a polling source** (if the provider has no webhooks) — return a cursor-based
   list of events; the engine persists the cursor between runs.

### Acme provider template

```csharp
// ── 1. Settings ────────────────────────────────────────────────────────────
public sealed record AcmeSettings(string BaseUrl, string Region = "eu");

// ── 2. Typed client ────────────────────────────────────────────────────────
public interface IAcmeClient
{
    Task<AcmePingResponse> PingAsync(CancellationToken ct);
    Task<AcmeEchoResponse> EchoAsync(string message, CancellationToken ct);
}

public sealed class AcmeClient(IConnectionContext ctx) : IAcmeClient
{
    public Task<AcmePingResponse> PingAsync(CancellationToken ct)
        => ctx.Http.GetAsync<AcmePingResponse>("/v1/ping", ct);

    public Task<AcmeEchoResponse> EchoAsync(string message, CancellationToken ct)
        => ctx.Http.PostAsync<AcmeEchoResponse>("/v1/echo",
               new { message }, ct);
}

// ── 3. Provider declaration ────────────────────────────────────────────────
public sealed class AcmeProvider : IIntegrationProvider
{
    public ProviderDescriptor Descriptor { get; } = new(
        Code: "acme",
        DisplayName: "Acme",
        Category: "sample",
        SettingsType: typeof(AcmeSettings),
        Auth: AuthScheme.ApiKey,
        Capabilities: [typeof(IAcmePing), typeof(IAcmeEcho)],
        SupportsWebhooks: true,
        SupportsPolling: false);

    public async Task<IntegrationResult<HealthInfo>> PingAsync(
        IConnectionContext ctx, CancellationToken ct)
    {
        var client = ctx.GetService<IAcmeClient>();
        var resp = await client.PingAsync(ct);
        return resp.Ok
            ? IntegrationResult<HealthInfo>.Ok(new HealthInfo(resp.Latency))
            : IntegrationResult<HealthInfo>.Fail(
                new IntegrationError("Acme ping failed", ErrorKind.Transient, Retryable: true));
    }
}

// ── 4. Capabilities ────────────────────────────────────────────────────────
public interface IAcmePing  { Task<IntegrationResult<HealthInfo>> PingAsync(CancellationToken ct); }
public interface IAcmeEcho  { Task<IntegrationResult<string>> EchoAsync(string msg, CancellationToken ct); }

public sealed class AcmePingCapability(IConnectionContext ctx) : IAcmePing
{
    private readonly IAcmeClient _client = ctx.GetService<IAcmeClient>();
    public async Task<IntegrationResult<HealthInfo>> PingAsync(CancellationToken ct)
    {
        var r = await _client.PingAsync(ct);
        return r.Ok
            ? IntegrationResult<HealthInfo>.Ok(new HealthInfo(r.Latency))
            : IntegrationResult<HealthInfo>.Fail(
                new IntegrationError(r.Error ?? "ping failed", ErrorKind.Transient, true));
    }
}

public sealed class AcmeEchoCapability(IConnectionContext ctx) : IAcmeEcho
{
    private readonly IAcmeClient _client = ctx.GetService<IAcmeClient>();
    public async Task<IntegrationResult<string>> EchoAsync(string msg, CancellationToken ct)
    {
        var r = await _client.EchoAsync(msg, ct);
        return IntegrationResult<string>.Ok(r.Message);
    }
}

// ── 5. Command handler ─────────────────────────────────────────────────────
public sealed class AcmeEchoCommandHandler : ICommandHandler
{
    public string Operation => "acme.echo";
    public async Task<IntegrationResult<JsonElement>> ExecuteAsync(
        JsonElement payload, IConnectionContext ctx, CancellationToken ct)
    {
        var msg = payload.GetProperty("message").GetString() ?? string.Empty;
        var cap = ctx.GetService<IAcmeEcho>();
        var result = await cap.EchoAsync(msg, ct);
        if (!result.IsSuccess)
            return IntegrationResult<JsonElement>.Fail(result.Error!);
        return IntegrationResult<JsonElement>.Ok(
            JsonSerializer.SerializeToElement(new { echoed = result.Value }));
    }
}

// ── 6. Webhook handler ─────────────────────────────────────────────────────
public sealed class AcmeWebhookHandler : IWebhookHandler
{
    public string ProviderCode => "acme";

    public Task<IReadOnlyList<IntegrationEvent>> ParseAsync(
        InboundWebhook webhook, IConnectionContext ctx, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(webhook.Body);
        var type = doc.RootElement.GetProperty("type").GetString();
        if (type is null)
            return Task.FromResult<IReadOnlyList<IntegrationEvent>>([]);

        var ev = new IntegrationEvent(
            Type: $"acme.{type}",
            ConnectionId: webhook.ConnectionId,
            Payload: doc.RootElement.Clone());

        return Task.FromResult<IReadOnlyList<IntegrationEvent>>([ev]);
    }
}

// ── 7. Polling source ──────────────────────────────────────────────────────
// (not needed for Acme; shown for completeness)
public sealed class AcmePoller : IPollingSource
{
    public string ProviderCode => "acme";

    public async Task<PollingResult> PollAsync(
        IConnectionContext ctx, string? cursor, CancellationToken ct)
    {
        // fetch events since cursor; return new cursor + events
        var client = ctx.GetService<IAcmeClient>();
        // ...
        return new PollingResult(NewCursor: "2026-10-18T00:00:00Z", Events: []);
    }
}

// ── Registration ───────────────────────────────────────────────────────────
public static class AcmeRegistration
{
    public static IIntegrationsBuilder AddAcme(this IIntegrationsBuilder b) => b
        .AddProvider<AcmeProvider>()
        .AddTypedClient<IAcmeClient, AcmeClient>()
        .AddCapability<IAcmePing, AcmePingCapability>()
        .AddCapability<IAcmeEcho, AcmeEchoCapability>()
        .AddCommandHandler<AcmeEchoCommandHandler>()
        .AddWebhookHandler<AcmeWebhookHandler>();
}
```

---

## License

Internal package — TOTPLATFORM only.
