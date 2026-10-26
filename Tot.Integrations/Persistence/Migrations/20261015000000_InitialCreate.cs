using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tot.Integrations.Persistence;

#nullable disable
#pragma warning disable CS1591 // Missing XML comment — auto-generated migration file

namespace Tot.Integrations.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(IntegrationsDbContext))]
[Migration("20261015000000_InitialCreate")]
internal partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("integrations");

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS integrations.connections (
                id              uuid        NOT NULL DEFAULT gen_random_uuid(),
                provider_code   text        NOT NULL,
                name            text        NOT NULL,
                owner_type      text,
                owner_id        text,
                environment     text        NOT NULL DEFAULT 'production',
                enabled         boolean     NOT NULL DEFAULT true,
                settings        jsonb       NOT NULL DEFAULT '{}',
                secret_ref      text        NOT NULL,
                webhook_key     text        NOT NULL,
                created_at      timestamptz NOT NULL DEFAULT now(),
                updated_at      timestamptz,
                CONSTRAINT pk_connections PRIMARY KEY (id),
                CONSTRAINT uq_connections_webhook_key UNIQUE (webhook_key)
            );
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS integrations.secrets (
                ref          text    NOT NULL,
                ciphertext   bytea   NOT NULL,
                rotated_at   timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT pk_secrets PRIMARY KEY (ref)
            );
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS integrations.external_ids (
                connection_id   uuid    NOT NULL REFERENCES integrations.connections(id),
                internal_type   text    NOT NULL,
                internal_id     text    NOT NULL,
                external_type   text    NOT NULL,
                external_id     text    NOT NULL,
                meta            jsonb,
                CONSTRAINT pk_external_ids PRIMARY KEY (connection_id, internal_type, internal_id, external_type)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS uq_external_ids_external
                ON integrations.external_ids (connection_id, external_type, external_id);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS integrations.commands (
                id               uuid        NOT NULL DEFAULT gen_random_uuid(),
                connection_id    uuid        NOT NULL,
                operation        text        NOT NULL,
                idempotency_key  text        NOT NULL,
                payload          jsonb       NOT NULL,
                status           text        NOT NULL DEFAULT 'Pending',
                attempts         integer     NOT NULL DEFAULT 0,
                next_attempt_at  timestamptz,
                last_error       jsonb,
                result           jsonb,
                correlation_id   text,
                created_at       timestamptz NOT NULL DEFAULT now(),
                completed_at     timestamptz,
                CONSTRAINT pk_commands PRIMARY KEY (id),
                CONSTRAINT uq_commands_idempotency UNIQUE (connection_id, idempotency_key)
            );

            CREATE INDEX IF NOT EXISTS ix_commands_pending
                ON integrations.commands (next_attempt_at, status)
                WHERE status IN ('Pending', 'Running');
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS integrations.inbound_webhooks (
                id                bigint      GENERATED ALWAYS AS IDENTITY,
                connection_id     uuid        NOT NULL,
                provider_event_id text,
                received_at       timestamptz NOT NULL DEFAULT now(),
                headers           jsonb       NOT NULL DEFAULT '{}',
                body              bytea       NOT NULL,
                signature_valid   boolean     NOT NULL DEFAULT false,
                status            text        NOT NULL DEFAULT 'Received',
                attempts          integer     NOT NULL DEFAULT 0,
                error             text,
                CONSTRAINT pk_inbound_webhooks PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS uq_inbound_webhooks_event
                ON integrations.inbound_webhooks (connection_id, provider_event_id)
                WHERE provider_event_id IS NOT NULL;
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS integrations.poll_cursors (
                connection_id   uuid    NOT NULL,
                source          text    NOT NULL,
                cursor          text,
                polled_at       timestamptz,
                CONSTRAINT pk_poll_cursors PRIMARY KEY (connection_id, source)
            );
            """);

        // Partitioned journal table — rows inserted via Npgsql binary COPY (INT-18).
        // Partitions are created here for today and the next 7 days; the JournalWriter
        // background service creates further daily partitions in advance.
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS integrations.calls (
                ts              timestamptz NOT NULL,
                connection_id   uuid        NOT NULL,
                provider_code   text        NOT NULL,
                operation       text,
                method          text,
                url_path        text,
                status_code     integer,
                duration_ms     integer,
                outcome         text,
                error_kind      text,
                request_body    jsonb,
                response_body   jsonb,
                trace_id        text,
                command_id      uuid
            ) PARTITION BY RANGE (ts);

            DO $$
            DECLARE
                d         date;
                part_name text;
                start_ts  timestamptz;
                end_ts    timestamptz;
            BEGIN
                FOR d IN SELECT generate_series(CURRENT_DATE, CURRENT_DATE + 7, '1 day'::interval)::date LOOP
                    part_name := 'calls_' || to_char(d, 'YYYY_MM_DD');
                    start_ts  := d::timestamptz;
                    end_ts    := (d + 1)::timestamptz;
                    EXECUTE format(
                        'CREATE TABLE IF NOT EXISTS integrations.%I '
                        'PARTITION OF integrations.calls '
                        'FOR VALUES FROM (%L) TO (%L)',
                        part_name, start_ts, end_ts
                    );
                END LOOP;
            END $$;

            CREATE INDEX IF NOT EXISTS ix_calls_connection_ts
                ON integrations.calls (connection_id, ts DESC);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS integrations CASCADE;");
    }
}
