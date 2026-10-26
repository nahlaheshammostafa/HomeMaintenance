using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Tot.Integrations.Runtime.Connections;

internal sealed class ConnectionContext : IConnectionContext
{
    private static readonly JsonSerializerOptions _webOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger _logger;

    public IntegrationConnection Connection { get; }
    public HttpClient Http { get; }
    public IExternalIdMap Ids { get; }
    ILogger IConnectionContext.Logger => _logger;

    internal ConnectionContext(
        IntegrationConnection connection,
        HttpClient http,
        IExternalIdMap ids,
        ILogger logger)
    {
        Connection = connection;
        Http = http;
        Ids = ids;
        _logger = logger;
    }

    public TSettings Settings<TSettings>() where TSettings : class
    {
        TSettings? settings;
        try
        {
            settings = Connection.Settings.Deserialize<TSettings>(_webOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Could not deserialize connection '{Connection.Id}' settings to {typeof(TSettings).Name}: {ex.Message}", ex);
        }

        if (settings is null)
            throw new InvalidOperationException(
                $"Settings for connection '{Connection.Id}' deserialized to null for type {typeof(TSettings).Name}.");

        // Validate DataAnnotations on the settings object
        var ctx = new ValidationContext(settings);
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(settings, ctx, results, validateAllProperties: true))
        {
            var errors = string.Join("; ", results.Select(r => r.ErrorMessage));
            throw new InvalidOperationException(
                $"Settings for connection '{Connection.Id}' failed validation: {errors}");
        }

        return settings;
    }
}
