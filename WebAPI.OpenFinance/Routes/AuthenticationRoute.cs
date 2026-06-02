using FluentValidation;
using WebAPI.OpenFinance.Dtos;
using WebAPI.OpenFinance.Services;

namespace WebAPI.OpenFinance.Routes
{
    public static class AuthenticationRoute
    {
        public static void AuthenticationRoutes(this WebApplication app)
        {
            var route = app.MapGroup("authentication");

            // POST /authentication/login → returns clientId, name, and a JWT on success.
            route.MapPost("/login", async (IAuthService auth, IValidator<LoginRequest> validator, LoginRequest request) =>
            {
                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var outcome = await auth.LoginAsync(request);
                return outcome.Succeeded
                    ? Results.Ok(outcome.Response)
                    : Results.BadRequest(new { error = outcome.Error });
            }).AllowAnonymous();

            // POST /authentication/signup → creates the client (hashed credential) and returns a JWT.
            route.MapPost("/signup", async (IAuthService auth, IValidator<SignupRequest> validator, SignupRequest request) =>
            {
                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var outcome = await auth.SignupAsync(request);
                return outcome.Succeeded
                    ? Results.Ok(outcome.Response)
                    : Results.BadRequest(new { error = outcome.Error });
            }).AllowAnonymous();
        }
    }
}
