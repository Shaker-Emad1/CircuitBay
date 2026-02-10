namespace CircuitBay.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "CircuitBay";
    public string Audience { get; set; } = "CircuitBay.Api";
    public string SigningKey { get; set; } = "replace-this-with-env-secret-of-at-least-32-characters";
    public int Minutes { get; set; } = 90;
}
