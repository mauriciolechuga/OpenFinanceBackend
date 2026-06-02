namespace WebAPI.OpenFinance.Auth
{
    public interface IJwtTokenService
    {
        // Issues a signed JWT whose subject is the client's id.
        string CreateToken(int clientId, string clientName);
    }
}
