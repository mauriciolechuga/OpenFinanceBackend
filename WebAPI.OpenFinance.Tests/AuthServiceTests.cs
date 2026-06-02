using Microsoft.EntityFrameworkCore;
using WebAPI.OpenFinance.Dtos;
using WebAPI.OpenFinance.Services;
using Xunit;

namespace WebAPI.OpenFinance.Tests
{
    public class AuthServiceTests
    {
        private static AuthService NewService(string db) =>
            new(TestSupport.NewContext(db), TestSupport.JwtTokenService());

        private static SignupRequest ValidSignup() =>
            new("user@example.com", "Str0ng!Pass", "Test User", "123 Main Street");

        [Fact]
        public async Task Signup_creates_client_with_hashed_password_and_token()
        {
            var db = Guid.NewGuid().ToString();
            var service = NewService(db);

            var outcome = await service.SignupAsync(ValidSignup());

            Assert.True(outcome.Succeeded);
            Assert.False(string.IsNullOrWhiteSpace(outcome.Response!.Token));

            await using var ctx = TestSupport.NewContext(db);
            var credential = await ctx.ClientCredentials.SingleAsync();
            Assert.NotEqual("Str0ng!Pass", credential.clientPassword); // stored as a hash
            Assert.Equal(3, credential.remainingLoginAttempts);
        }

        [Fact]
        public async Task Signup_with_existing_email_fails()
        {
            var db = Guid.NewGuid().ToString();
            await NewService(db).SignupAsync(ValidSignup());

            var outcome = await NewService(db).SignupAsync(ValidSignup());

            Assert.False(outcome.Succeeded);
            Assert.Equal("Email already in use", outcome.Error);
        }

        [Fact]
        public async Task Login_succeeds_with_correct_password()
        {
            var db = Guid.NewGuid().ToString();
            await NewService(db).SignupAsync(ValidSignup());

            var outcome = await NewService(db).LoginAsync(new LoginRequest("user@example.com", "Str0ng!Pass"));

            Assert.True(outcome.Succeeded);
            Assert.False(string.IsNullOrWhiteSpace(outcome.Response!.Token));
        }

        [Fact]
        public async Task Login_with_wrong_password_fails_and_decrements_attempts()
        {
            var db = Guid.NewGuid().ToString();
            await NewService(db).SignupAsync(ValidSignup());

            var outcome = await NewService(db).LoginAsync(new LoginRequest("user@example.com", "WrongPass1!"));

            Assert.False(outcome.Succeeded);
            Assert.Equal("Incorrect Password", outcome.Error);

            await using var ctx = TestSupport.NewContext(db);
            var credential = await ctx.ClientCredentials.SingleAsync();
            Assert.Equal(2, credential.remainingLoginAttempts);
        }

        [Fact]
        public async Task Three_wrong_passwords_block_the_client()
        {
            var db = Guid.NewGuid().ToString();
            await NewService(db).SignupAsync(ValidSignup());

            for (var i = 0; i < 3; i++)
            {
                await NewService(db).LoginAsync(new LoginRequest("user@example.com", "WrongPass1!"));
            }

            // Even the correct password is rejected while blocked.
            var outcome = await NewService(db).LoginAsync(new LoginRequest("user@example.com", "Str0ng!Pass"));

            Assert.False(outcome.Succeeded);
            Assert.Equal("Client is Blocked", outcome.Error);
        }

        [Fact]
        public async Task Login_unknown_email_fails()
        {
            var db = Guid.NewGuid().ToString();

            var outcome = await NewService(db).LoginAsync(new LoginRequest("nobody@example.com", "whatever"));

            Assert.False(outcome.Succeeded);
            Assert.Equal("Client not found", outcome.Error);
        }
    }
}
