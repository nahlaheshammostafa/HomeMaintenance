using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using System.Data.Common;
using Tot.Integrations.DependencyInjection;
using Tot.Integrations.Persistence.Entities;

namespace Tot.Integrations.Persistence;

internal sealed class EfCommandQueue : IIntegrationCommandQueue
{
    private readonly IntegrationsDbContext _db;
    private readonly string _schema;

    public EfCommandQueue(IntegrationsDbContext db, IntegrationsOptions options)
    {
        _db = db;
        _schema = options.Schema;
    }

    /// <inheritdoc/>
    public async Task<Guid> EnqueueAsync(
        IntegrationCommand command,
        DbTransaction? dbTransaction = null,
        CancellationToken ct = default)
    {
        if (dbTransaction is not null)
            return await EnqueueWithTransactionAsync(command, dbTransaction, ct);

        return await EnqueueWithEfAsync(command, ct);
    }

    private async Task<Guid> EnqueueWithEfAsync(IntegrationCommand command, CancellationToken ct)
    {
        var entity = new CommandEntity
        {
            Id = Guid.NewGuid(),
            ConnectionId = command.ConnectionId,
            Operation = command.Operation,
            IdempotencyKey = command.IdempotencyKey,
            Payload = command.Payload.GetRawText(),
            Status = "Pending",
            NextAttemptAt = command.NotBefore ?? DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Commands.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
            return entity.Id;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _db.ChangeTracker.Clear();
            return await _db.Commands
                .AsNoTracking()
                .Where(c => c.ConnectionId == command.ConnectionId
                         && c.IdempotencyKey == command.IdempotencyKey)
                .Select(c => c.Id)
                .FirstAsync(ct);
        }
    }

    private async Task<Guid> EnqueueWithTransactionAsync(
        IntegrationCommand command, DbTransaction dbTransaction, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)dbTransaction.Connection!;
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = (NpgsqlTransaction)dbTransaction;
        cmd.CommandText = $"""
            INSERT INTO {_schema}.commands
                (id, connection_id, operation, idempotency_key, payload, status, next_attempt_at, created_at)
            VALUES
                (@id, @conn, @op, @key, @payload::jsonb, 'Pending', @next_at, now())
            ON CONFLICT (connection_id, idempotency_key) DO UPDATE
                SET status = {_schema}.commands.status
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, Guid.NewGuid());
        cmd.Parameters.AddWithValue("conn", NpgsqlDbType.Uuid, command.ConnectionId);
        cmd.Parameters.AddWithValue("op", NpgsqlDbType.Text, command.Operation);
        cmd.Parameters.AddWithValue("key", NpgsqlDbType.Text, command.IdempotencyKey);
        cmd.Parameters.AddWithValue("payload", NpgsqlDbType.Text, command.Payload.GetRawText());
        cmd.Parameters.AddWithValue("next_at", NpgsqlDbType.TimestampTz,
            command.NotBefore ?? DateTimeOffset.UtcNow);

        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";
}
