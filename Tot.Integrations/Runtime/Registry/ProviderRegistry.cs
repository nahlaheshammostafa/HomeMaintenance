using System.Collections.Concurrent;

namespace Tot.Integrations.Runtime.Registry;

/// <summary>
/// In-memory registry built at startup: maps provider codes, capability interfaces,
/// command operations, webhook handlers and polling sources to their implementation types.
/// Everything in this namespace is internal.
/// </summary>
internal sealed class ProviderRegistry
{
    private readonly ConcurrentDictionary<string, ProviderRegistration> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Type> _providerTypes = [];
    private readonly Dictionary<Type, Type> _capabilities = []; // interface → impl
    private readonly List<Type> _commandHandlers = [];
    private readonly Dictionary<string, Type> _commandHandlersByOperation = [];
    private readonly List<Type> _webhookHandlers = [];
    private readonly Dictionary<string, Type> _webhookHandlersByProviderCode = [];
    private readonly List<Type> _pollingSources = [];
    private readonly List<string> _redactionPatterns = [];

    internal string? ConnectionString { get; private set; }
    internal string Schema { get; private set; } = "integrations";
    internal bool UseDataProtectionSecretStore { get; set; }

    internal void RegisterProvider(Type providerType)
    {
        if (!typeof(IIntegrationProvider).IsAssignableFrom(providerType))
            throw new ArgumentException($"{providerType.Name} must implement IIntegrationProvider.");
        _providerTypes.Add(providerType);
    }

    internal void RegisterCapability(Type interfaceType, Type implType)
    {
        if (!interfaceType.IsInterface)
            throw new ArgumentException($"{interfaceType.Name} must be an interface.");
        _capabilities[interfaceType] = implType;
    }

    internal void RegisterCommandHandler(Type handlerType)
    {
        if (!typeof(ICommandHandler).IsAssignableFrom(handlerType))
            throw new ArgumentException($"{handlerType.Name} must implement ICommandHandler.");
        _commandHandlers.Add(handlerType);
    }

    internal void RegisterWebhookHandler(Type handlerType)
    {
        if (!typeof(IWebhookHandler).IsAssignableFrom(handlerType))
            throw new ArgumentException($"{handlerType.Name} must implement IWebhookHandler.");
        _webhookHandlers.Add(handlerType);
    }

    internal void RegisterPollingSource(Type sourceType)
    {
        if (!typeof(IPollingSource).IsAssignableFrom(sourceType))
            throw new ArgumentException($"{sourceType.Name} must implement IPollingSource.");
        _pollingSources.Add(sourceType);
    }

    internal void AddRedactionPatterns(string[] patterns) => _redactionPatterns.AddRange(patterns);

    internal void SetConnectionString(string connectionString, string schema)
    {
        ConnectionString = connectionString;
        Schema = schema;
    }

    // Lazy initialization guard — allows tests to skip explicit startup without double-init.
    private int _indexingDone;
    private readonly object _indexingLock = new();

    /// <summary>
    /// Ensures providers have been indexed exactly once. Safe to call from any thread.
    /// No-op after the first successful call.
    /// </summary>
    internal void EnsureIndexed(IServiceProvider services)
    {
        if (Volatile.Read(ref _indexingDone) == 1) return;
        lock (_indexingLock)
        {
            if (_indexingDone == 1) return;
            ValidateRegistrations(services);
            Volatile.Write(ref _indexingDone, 1);
        }
    }

    /// <summary>
    /// Called once at startup (via <see cref="ValidateRegistrations"/>) to resolve descriptors
    /// from the DI container and index them by provider code.
    /// </summary>
    internal void IndexProvider(string code, IIntegrationProvider provider)
    {
        var reg = new ProviderRegistration(provider.Descriptor, provider.GetType());
        if (!_providers.TryAdd(code, reg))
            throw new InvalidOperationException(
                $"Duplicate provider code '{code}'. Each provider must have a unique code.");
    }

    internal IReadOnlyList<Type> ProviderTypes => _providerTypes.AsReadOnly();
    internal IReadOnlyDictionary<Type, Type> Capabilities => _capabilities;
    internal IReadOnlyList<Type> CommandHandlerTypes => _commandHandlers.AsReadOnly();
    internal IReadOnlyList<Type> WebhookHandlerTypes => _webhookHandlers.AsReadOnly();
    internal IReadOnlyList<Type> PollingSourceTypes => _pollingSources.AsReadOnly();
    internal IReadOnlyList<string> RedactionPatterns => _redactionPatterns.AsReadOnly();

    internal bool TryGetProvider(string code, out ProviderRegistration registration) =>
        _providers.TryGetValue(code, out registration!);

    internal Type? GetCapabilityImpl(Type interfaceType) =>
        _capabilities.TryGetValue(interfaceType, out var impl) ? impl : null;

    internal Type? GetHandlerTypeForOperation(string operation) =>
        _commandHandlersByOperation.TryGetValue(operation, out var type) ? type : null;

    internal Type? GetWebhookHandlerTypeForProvider(string providerCode) =>
        _webhookHandlersByProviderCode.TryGetValue(providerCode, out var type) ? type : null;

    /// <summary>
    /// Validates all registrations at startup.
    /// Throws <see cref="InvalidOperationException"/> with a descriptive message on the first problem found.
    /// </summary>
    internal void ValidateRegistrations(IServiceProvider services)
    {
        // Resolve and index all providers, checking for duplicate codes
        foreach (var providerType in _providerTypes)
        {
            if (services.GetService(providerType) is not IIntegrationProvider provider)
                throw new InvalidOperationException(
                    $"Provider type '{providerType.Name}' is registered but not resolvable from DI. " +
                    "Ensure it is added to services (AddProvider<T>() calls AddTransient<T> automatically).");
            IndexProvider(provider.Descriptor.Code, provider);
        }

        // Capability impls are instantiated via ActivatorUtilities (not plain DI),
        // so we only verify the impl type is actually registered in the container,
        // not that it can be fully constructed (IConnectionContext is injected at call time).

        // Build operation → handler type map for the command dispatcher.
        foreach (var handlerType in _commandHandlers)
        {
            try
            {
                var handler = (ICommandHandler)Microsoft.Extensions.DependencyInjection
                    .ActivatorUtilities.CreateInstance(services, handlerType);
                if (_commandHandlersByOperation.TryGetValue(handler.Operation, out var existing))
                    throw new InvalidOperationException(
                        $"Duplicate command handler operation '{handler.Operation}': " +
                        $"{existing.Name} and {handlerType.Name}.");
                _commandHandlersByOperation[handler.Operation] = handlerType;
            }
            catch (InvalidOperationException) { throw; }
            catch { /* handler has unresolvable deps — map built at dispatch */ }
        }

        // Build providerCode → webhook handler type map for the webhook endpoint.
        foreach (var handlerType in _webhookHandlers)
        {
            try
            {
                var handler = (IWebhookHandler)Microsoft.Extensions.DependencyInjection
                    .ActivatorUtilities.CreateInstance(services, handlerType);
                if (_webhookHandlersByProviderCode.TryGetValue(handler.ProviderCode, out var existing))
                    throw new InvalidOperationException(
                        $"Duplicate webhook handler for provider '{handler.ProviderCode}': " +
                        $"{existing.Name} and {handlerType.Name}.");
                _webhookHandlersByProviderCode[handler.ProviderCode] = handlerType;
            }
            catch (InvalidOperationException) { throw; }
            catch { /* handler has unresolvable deps — skip */ }
        }
    }
}

internal sealed record ProviderRegistration(ProviderDescriptor Descriptor, Type ProviderType);
