using WebAPI.OpenFinance.Dtos;

namespace WebAPI.OpenFinance.Services
{
    // Outcome of an auth operation: either a populated Response, or an Error message for a 400.
    public record AuthOutcome(AuthResponse? Response, string? Error)
    {
        public bool Succeeded => Response is not null;
        public static AuthOutcome Ok(AuthResponse response) => new(response, null);
        public static AuthOutcome Fail(string error) => new(null, error);
    }

    public interface IAuthService
    {
        Task<AuthOutcome> LoginAsync(LoginRequest request);
        Task<AuthOutcome> SignupAsync(SignupRequest request);
    }
}
