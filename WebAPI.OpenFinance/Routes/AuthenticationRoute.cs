using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Helpers;

namespace WebAPI.OpenFinance.Routes
{
    public static class AuthenticationRoute
    {
        public static void AuthenticationRoutes(this WebApplication app)
        {
            var route = app.MapGroup("authentication");

            // POST /authentication/login
            // Accepts an email and password; returns the clientID and name on success.
            route.MapPost("/login", async (OpenFinanceContext context, Login login) =>
            {
                var email = login.Email;
                var password = login.Password;

                if (!await AuthenticationHelper.CheckEmailExists(context, email))
                {
                    return Results.BadRequest("Client not found");
                }

                var client = await AuthenticationHelper.GetClientByEmail(context, email);

                // A wrong password decrements the client's remaining login attempts.
                if (!await AuthenticationHelper.CheckPassword(context, client.clientID, password))
                {
                    return Results.BadRequest("Incorrect Password");
                }

                if (await AuthenticationHelper.CheckIfClientIsBlocked(context, client.clientID))
                {
                    return Results.BadRequest("Client is Blocked");
                }

                // Successful login resets the attempt counter and records the timestamp.
                await AuthenticationHelper.UpdateLastLogin(context, client.clientID);

                var loginResponse = new
                {
                    clientID = client.clientID,
                    clientName = client.clientName
                };

                return Results.Ok(loginResponse);
            });

            // POST /authentication/signup
            // Validates the input, creates the client and its (hashed) credential,
            // and returns the new clientID and name.
            route.MapPost("/signup", async (OpenFinanceContext context, Signup signup) =>
            {
                var email = signup.Email;
                var password = signup.Password;
                var name = signup.Name;
                var address = signup.Address;

                if (!ValidationHelper.IsValidEmail(email))
                {
                    return Results.BadRequest("Invalid Email");
                }

                if (!ValidationHelper.IsValidPassword(password))
                {
                    return Results.BadRequest("Invalid Password");
                }

                if (!ValidationHelper.IsValidName(name))
                {
                    return Results.BadRequest("Invalid Name");
                }

                if (!ValidationHelper.IsValidAddress(address))
                {
                    return Results.BadRequest("Invalid Address");
                }

                if (await AuthenticationHelper.CheckEmailExists(context, email))
                {
                    return Results.BadRequest("Email already in use");
                }

                var newClientID = await AuthenticationHelper.RegisterClient(context, name, email, address);
                await AuthenticationHelper.RegisterClientCredential(context, newClientID, password);

                var signupResponse = new
                {
                    clientID = newClientID,
                    clientName = name
                };

                return Results.Ok(signupResponse);
            });
        }
    }

    // Request body for POST /authentication/login.
    public class Login
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    // Request body for POST /authentication/signup.
    public class Signup
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
    }
}
