namespace WebAPI.OpenFinance.Auth
{
    // Bound from the "Jwt" configuration section. The signing Key is a secret and must come from
    // user-secrets (dev) or environment/Secrets Manager (prod) — never from a committed file.
    public class JwtSettings
    {
        public string Issuer { get; set; } = "OpenFinance";
        public string Audience { get; set; } = "OpenFinanceApp";
        public string Key { get; set; } = string.Empty;
        public int ExpiryMinutes { get; set; } = 60;
    }
}
