using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebAPI.OpenFinance.Aggregation;
using WebAPI.OpenFinance.Auth;
using WebAPI.OpenFinance.Data;

namespace WebAPI.OpenFinance.Tests
{
    // Shared helpers for the test suite.
    internal static class TestSupport
    {
        // Creates a context over a named in-memory database. Reuse the same name across contexts
        // in one test to read back what another context wrote.
        public static OpenFinanceContext NewContext(string databaseName) =>
            new(new DbContextOptionsBuilder<OpenFinanceContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);

        public static IJwtTokenService JwtTokenService() =>
            new JwtTokenService(Options.Create(new JwtSettings
            {
                Issuer = "test",
                Audience = "test",
                Key = "test-signing-key-test-signing-key-0123456789",
                ExpiryMinutes = 60
            }));
    }

    // Identity protector for tests (the real one needs Data Protection infrastructure).
    internal sealed class FakeTokenProtector : ITokenProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }
}
