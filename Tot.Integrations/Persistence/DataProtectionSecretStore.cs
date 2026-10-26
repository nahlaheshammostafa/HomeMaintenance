using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Tot.Integrations.Persistence.Entities;

namespace Tot.Integrations.Persistence;

internal sealed class DataProtectionSecretStore : ISecretStore
{
    private readonly IntegrationsDbContext _db;
    private readonly IDataProtectionProvider _dataProtection;

    public DataProtectionSecretStore(IntegrationsDbContext db, IDataProtectionProvider dataProtection)
    {
        _db = db;
        _dataProtection = dataProtection;
    }

    /// <inheritdoc/>
    public async Task SetAsync(string secretRef, string value, CancellationToken ct)
    {
        var ciphertext = Protect(secretRef, value);

        var existing = await _db.Secrets
            .FirstOrDefaultAsync(s => s.Ref == secretRef, ct);

        if (existing is null)
        {
            _db.Secrets.Add(new SecretEntity
            {
                Ref = secretRef,
                Ciphertext = ciphertext,
                RotatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Ciphertext = ciphertext;
            existing.RotatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<string?> GetAsync(string secretRef, CancellationToken ct)
    {
        var entity = await _db.Secrets
            .FirstOrDefaultAsync(s => s.Ref == secretRef, ct);

        if (entity is null || entity.Ciphertext.Length == 0)
            return null;

        return Unprotect(secretRef, entity.Ciphertext);
    }

    /// <inheritdoc/>
    public async Task RotateAsync(string secretRef, CancellationToken ct)
    {
        var plaintext = await GetAsync(secretRef, ct);
        if (plaintext is not null)
            await SetAsync(secretRef, plaintext, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string secretRef, CancellationToken ct)
    {
        var entity = await _db.Secrets
            .FirstOrDefaultAsync(s => s.Ref == secretRef, ct);

        if (entity is not null)
        {
            _db.Secrets.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    private byte[] Protect(string secretRef, string value)
    {
        var protector = _dataProtection.CreateProtector(Purpose(secretRef));
        return protector.Protect(Encoding.UTF8.GetBytes(value));
    }

    private string? Unprotect(string secretRef, byte[] ciphertext)
    {
        try
        {
            var protector = _dataProtection.CreateProtector(Purpose(secretRef));
            return Encoding.UTF8.GetString(protector.Unprotect(ciphertext));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string Purpose(string secretRef) => $"Tot.Integrations.Secret:{secretRef}";
}
