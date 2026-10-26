namespace HomeMaintenance.Application.Authentication.Settings;

/// <summary>
/// Configuration settings for JWT token generation and validation.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryInMinutes { get; set; } = 120;
}
