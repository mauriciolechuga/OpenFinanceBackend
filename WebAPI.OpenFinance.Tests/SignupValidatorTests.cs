using WebAPI.OpenFinance.Dtos;
using WebAPI.OpenFinance.Validation;
using Xunit;

namespace WebAPI.OpenFinance.Tests
{
    public class SignupValidatorTests
    {
        private readonly SignupRequestValidator _validator = new();

        [Fact]
        public void Valid_request_passes()
        {
            var result = _validator.Validate(new SignupRequest("user@example.com", "Str0ng!Pass", "Test User", "123 Main Street"));
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("not-an-email", "Str0ng!Pass", "Test User", "123 Main Street")] // bad email
        [InlineData("user@example.com", "weak", "Test User", "123 Main Street")]     // weak password
        [InlineData("user@example.com", "Str0ng!Pass", "A1", "123 Main Street")]     // bad name (digits, too short)
        [InlineData("user@example.com", "Str0ng!Pass", "Test User", "123")]          // address too short
        public void Invalid_requests_fail(string email, string password, string name, string address)
        {
            var result = _validator.Validate(new SignupRequest(email, password, name, address));
            Assert.False(result.IsValid);
        }
    }
}
