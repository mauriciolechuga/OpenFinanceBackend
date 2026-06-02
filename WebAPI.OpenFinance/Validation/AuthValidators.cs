using FluentValidation;
using WebAPI.OpenFinance.Dtos;

namespace WebAPI.OpenFinance.Validation
{
    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class SignupRequestValidator : AbstractValidator<SignupRequest>
    {
        // Mirrors the original ValidationHelper regex rules.
        public SignupRequestValidator()
        {
            RuleFor(x => x.Email)
                .Matches(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$")
                .WithMessage("Invalid Email");

            // At least 8 chars, with upper, lower, digit, and special character.
            RuleFor(x => x.Password)
                .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$")
                .WithMessage("Invalid Password");

            // Letters and spaces only, at least 3 chars.
            RuleFor(x => x.Name)
                .Matches(@"^[a-zA-Z\s]{3,}$")
                .WithMessage("Invalid Name");

            RuleFor(x => x.Address)
                .MinimumLength(5)
                .WithMessage("Invalid Address");
        }
    }
}
