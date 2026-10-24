namespace Nudge.Api.Infrastructure;

// Bound from the "Jwt" section of configuration (appsettings or environment variables).
public class JwtOptions
{
    public string Issuer { get; set; } = "nudge-api";
    public string Audience { get; set; } = "nudge-app";

    // Secret used to sign tokens. Must be at least 32 characters.
    // Never commit a real one: set it with the Jwt__Key environment variable.
    public string Key { get; set; } = "";

    public int ExpiryHours { get; set; } = 24 * 7;
}
