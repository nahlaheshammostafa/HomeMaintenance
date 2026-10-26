using Microsoft.EntityFrameworkCore;
using Tot.Integrations.Persistence.Entities;

namespace Tot.Integrations.Persistence;

/// <summary>
/// EF Core DbContext for the <c>integrations</c> schema.
/// All tables live under <c>integrations.*</c> and are isolated from the host's main schema.
/// Registered by <c>IIntegrationsBuilder.UsePostgres()</c>.
/// </summary>
internal sealed class IntegrationsDbContext : DbContext
{
    /// <inheritdoc/>
    public IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options) : base(options) { }

    internal DbSet<ConnectionEntity> Connections => Set<ConnectionEntity>();
    internal DbSet<SecretEntity> Secrets => Set<SecretEntity>();
    internal DbSet<ExternalIdEntity> ExternalIds => Set<ExternalIdEntity>();
    internal DbSet<CommandEntity> Commands => Set<CommandEntity>();
    internal DbSet<InboundWebhookEntity> InboundWebhooks => Set<InboundWebhookEntity>();
    internal DbSet<PollCursorEntity> PollCursors => Set<PollCursorEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("integrations");

        ConfigureConnections(modelBuilder);
        ConfigureSecrets(modelBuilder);
        ConfigureExternalIds(modelBuilder);
        ConfigureCommands(modelBuilder);
        ConfigureInboundWebhooks(modelBuilder);
        ConfigurePollCursors(modelBuilder);
    }

    private static void ConfigureConnections(ModelBuilder b)
    {
        b.Entity<ConnectionEntity>(e =>
        {
            e.ToTable("connections");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProviderCode).HasColumnName("provider_code").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.OwnerType).HasColumnName("owner_type");
            e.Property(x => x.OwnerId).HasColumnName("owner_id");
            e.Property(x => x.Environment).HasColumnName("environment").HasDefaultValue("production").IsRequired();
            e.Property(x => x.Enabled).HasColumnName("enabled").HasDefaultValue(true);
            e.Property(x => x.Settings).HasColumnName("settings").HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();
            e.Property(x => x.SecretRef).HasColumnName("secret_ref").IsRequired();
            e.Property(x => x.WebhookKey).HasColumnName("webhook_key").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
            e.HasIndex(x => x.WebhookKey).IsUnique().HasDatabaseName("uq_connections_webhook_key");
            // xmin is a PostgreSQL system column; configure it as an optimistic concurrency token.
            e.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });
    }

    private static void ConfigureSecrets(ModelBuilder b)
    {
        b.Entity<SecretEntity>(e =>
        {
            e.ToTable("secrets");
            e.HasKey(x => x.Ref);
            e.Property(x => x.Ref).HasColumnName("ref");
            e.Property(x => x.Ciphertext).HasColumnName("ciphertext").HasColumnType("bytea").IsRequired();
            e.Property(x => x.RotatedAt).HasColumnName("rotated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        });
    }

    private static void ConfigureExternalIds(ModelBuilder b)
    {
        b.Entity<ExternalIdEntity>(e =>
        {
            e.ToTable("external_ids");
            e.HasKey(x => new { x.ConnectionId, x.InternalType, x.InternalId, x.ExternalType });
            e.Property(x => x.ConnectionId).HasColumnName("connection_id");
            e.Property(x => x.InternalType).HasColumnName("internal_type");
            e.Property(x => x.InternalId).HasColumnName("internal_id");
            e.Property(x => x.ExternalType).HasColumnName("external_type");
            e.Property(x => x.ExternalId).HasColumnName("external_id").IsRequired();
            e.Property(x => x.Meta).HasColumnName("meta").HasColumnType("jsonb");
            // Partial unique index (where provider_event_id IS NOT NULL) is created in the raw migration.
        });
    }

    private static void ConfigureCommands(ModelBuilder b)
    {
        b.Entity<CommandEntity>(e =>
        {
            e.ToTable("commands");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ConnectionId).HasColumnName("connection_id");
            e.Property(x => x.Operation).HasColumnName("operation").IsRequired();
            e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").IsRequired();
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("Pending").IsRequired();
            e.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            e.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.LastError).HasColumnName("last_error").HasColumnType("jsonb");
            e.Property(x => x.Result).HasColumnName("result").HasColumnType("jsonb");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.LockedAt).HasColumnName("locked_at").HasColumnType("timestamp with time zone");
            e.HasIndex(x => new { x.ConnectionId, x.IdempotencyKey }).IsUnique().HasDatabaseName("uq_commands_idempotency");
            e.HasIndex(x => new { x.NextAttemptAt, x.Status }).HasDatabaseName("ix_commands_pending");
        });
    }

    private static void ConfigureInboundWebhooks(ModelBuilder b)
    {
        b.Entity<InboundWebhookEntity>(e =>
        {
            e.ToTable("inbound_webhooks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ConnectionId).HasColumnName("connection_id");
            e.Property(x => x.ProviderEventId).HasColumnName("provider_event_id");
            e.Property(x => x.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            e.Property(x => x.Headers).HasColumnName("headers").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Body).HasColumnName("body").HasColumnType("bytea").IsRequired();
            e.Property(x => x.SignatureValid).HasColumnName("signature_valid");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValue("Received").IsRequired();
            e.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            e.Property(x => x.Error).HasColumnName("error");
            // Partial unique index on (connection_id, provider_event_id) WHERE provider_event_id IS NOT NULL
            // is created in the raw SQL migration — EF cannot express conditional indexes.
        });
    }

    private static void ConfigurePollCursors(ModelBuilder b)
    {
        b.Entity<PollCursorEntity>(e =>
        {
            e.ToTable("poll_cursors");
            e.HasKey(x => new { x.ConnectionId, x.Source });
            e.Property(x => x.ConnectionId).HasColumnName("connection_id");
            e.Property(x => x.Source).HasColumnName("source");
            e.Property(x => x.Cursor).HasColumnName("cursor");
            e.Property(x => x.PolledAt).HasColumnName("polled_at").HasColumnType("timestamp with time zone");
        });
    }
}
