using System.Security.Cryptography;
using System.Text;

namespace Tot.Integrations.Runtime.Auth;

/// <summary>
/// Signs each outbound request using an HMAC signature (SHA-256 or SHA-512).
/// Canonical string: <c>METHOD\nPATH\nTIMESTAMP\nBODY_HASH</c>.
/// The timestamp and signature are added as request headers.
/// </summary>
internal sealed class HmacSigningProvider : IAuthenticationProvider
{
    private readonly byte[] _keyBytes;
    private readonly HmacAlgorithm _algorithm;
    private readonly string _timestampHeader;
    private readonly string _signatureHeader;

    internal enum HmacAlgorithm { Sha256, Sha512 }

    internal HmacSigningProvider(
        string signingKey,
        HmacAlgorithm algorithm = HmacAlgorithm.Sha256,
        string timestampHeader = "X-Timestamp",
        string signatureHeader = "X-Signature")
    {
        _keyBytes = Encoding.UTF8.GetBytes(signingKey);
        _algorithm = algorithm;
        _timestampHeader = timestampHeader;
        _signatureHeader = signatureHeader;
    }

    public async Task AuthenticateAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var method = request.Method.Method.ToUpperInvariant();
        var path = request.RequestUri?.AbsolutePath ?? "/";

        byte[] bodyBytes = [];
        if (request.Content is not null)
        {
            bodyBytes = await request.Content.ReadAsByteArrayAsync(ct);
            // Restore content: ReadAsByteArrayAsync exhausts the stream; next handler needs it.
            var restored = new ByteArrayContent(bodyBytes);
            foreach (var header in request.Content.Headers)
                restored.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Content = restored;
        }

        var bodyHash = ComputeBodyHash(bodyBytes);
        var canonical = $"{method}\n{path}\n{timestamp}\n{bodyHash}";

        var sig = Sign(Encoding.UTF8.GetBytes(canonical));

        request.Headers.TryAddWithoutValidation(_timestampHeader, timestamp);
        request.Headers.TryAddWithoutValidation(_signatureHeader, Convert.ToHexString(sig).ToLowerInvariant());
    }

    private string ComputeBodyHash(byte[] body)
    {
        byte[] hash = _algorithm == HmacAlgorithm.Sha256
            ? SHA256.HashData(body)
            : SHA512.HashData(body);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private byte[] Sign(byte[] data)
    {
        using var hmac = _algorithm == HmacAlgorithm.Sha256
            ? (HMAC)new HMACSHA256(_keyBytes)
            : new HMACSHA512(_keyBytes);
        return hmac.ComputeHash(data);
    }
}
