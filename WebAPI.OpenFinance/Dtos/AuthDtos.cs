namespace WebAPI.OpenFinance.Dtos
{
    public record LoginRequest(string Email, string Password);

    public record SignupRequest(string Email, string Password, string Name, string Address);

    // Returned on successful login/signup. The token authenticates subsequent requests.
    public record AuthResponse(int ClientId, string ClientName, string Token);
}
