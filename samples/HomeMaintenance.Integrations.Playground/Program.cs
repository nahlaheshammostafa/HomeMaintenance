using Microsoft.OpenApi;
using Tot.Integrations.DependencyInjection;
using Tot.Integrations.Endpoints;
using HomeMaintenance.Integrations.Playground.Endpoints;
using HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

var builder = WebApplication.CreateBuilder(args);

// Capability implementations receive IConnectionContext dynamically at runtime via ICapabilityResolver,
// so disable root container constructor validation on build.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = false;
    options.ValidateScopes = false;
});

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HomeMaintenance.Integrations Playground",
        Version = "v1",
        Description = "Interactive host to explore and test the 4 core pillars of Tot.Integrations (RPC Capabilities, Outbox Commands, Webhooks, Domain Events) using the HomeDispatch contractor provider."
    });
});

// Configure Integrations
var connectionString = builder.Configuration.GetConnectionString("IntegrationsDb")
    ?? throw new InvalidOperationException("Connection string 'IntegrationsDb' not found in configuration.");

builder.Services
    .AddIntegrations(builder.Configuration)
    .AddHomeDispatch()
    .UsePostgres(connectionString)
    .UseDataProtectionSecretStore();

var app = builder.Build();

// Run database migrations on startup to ensure 'integrations' schema and tables exist
Console.WriteLine("--> Applying Tot.Integrations database migrations...");
await app.Services.MigrateIntegrationsAsync();
Console.WriteLine("--> Database migrations applied successfully!");

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "HomeMaintenance Integrations v1");
        c.RoutePrefix = "swagger";
    });
}

// Redirect root to Swagger UI
app.MapGet("/", () => Results.Redirect("/swagger"));

// Map Tot.Integrations Admin and Webhook routes
app.MapIntegrationAdminApi("/api/admin/integrations");
app.MapIntegrationWebhooks("/integrations/webhooks");

// Map Interactive Playground Testing routes
app.MapPlaygroundTestApi();

Console.WriteLine("--> HomeMaintenance.Integrations.Playground ready! Listening on configured ports.");
app.Run();
