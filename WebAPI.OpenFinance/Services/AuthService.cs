using WebAPI.OpenFinance.Auth;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Dtos;
using WebAPI.OpenFinance.Helpers;

namespace WebAPI.OpenFinance.Services
{
    // Orchestrates login and signup. Data-access primitives (lookup, password hashing, blocking)
    // live in AuthenticationHelper; this service composes them and issues the JWT.
    public class AuthService : IAuthService
    {
        private readonly OpenFinanceContext _context;
        private readonly IJwtTokenService _tokens;

        public AuthService(OpenFinanceContext context, IJwtTokenService tokens)
        {
            _context = context;
            _tokens = tokens;
        }

        public async Task<AuthOutcome> LoginAsync(LoginRequest request)
        {
            if (!await AuthenticationHelper.CheckEmailExists(_context, request.Email))
            {
                return AuthOutcome.Fail("Client not found");
            }

            var client = await AuthenticationHelper.GetClientByEmail(_context, request.Email);

            // Blocked check first: a blocked client should not keep burning password attempts.
            if (await AuthenticationHelper.CheckIfClientIsBlocked(_context, client.clientID))
            {
                return AuthOutcome.Fail("Client is Blocked");
            }

            if (!await AuthenticationHelper.CheckPassword(_context, client.clientID, request.Password))
            {
                return AuthOutcome.Fail("Incorrect Password");
            }

            await AuthenticationHelper.UpdateLastLogin(_context, client.clientID);

            var token = _tokens.CreateToken(client.clientID, client.clientName);
            return AuthOutcome.Ok(new AuthResponse(client.clientID, client.clientName, token));
        }

        public async Task<AuthOutcome> SignupAsync(SignupRequest request)
        {
            if (await AuthenticationHelper.CheckEmailExists(_context, request.Email))
            {
                return AuthOutcome.Fail("Email already in use");
            }

            var newClientID = await AuthenticationHelper.RegisterClient(_context, request.Name, request.Email, request.Address);
            await AuthenticationHelper.RegisterClientCredential(_context, newClientID, request.Password);

            var token = _tokens.CreateToken(newClientID, request.Name);
            return AuthOutcome.Ok(new AuthResponse(newClientID, request.Name, token));
        }
    }
}
