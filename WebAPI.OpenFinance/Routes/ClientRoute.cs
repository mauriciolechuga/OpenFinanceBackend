using System.Security.Claims;
using WebAPI.OpenFinance.Auth;
using WebAPI.OpenFinance.Services;

namespace WebAPI.OpenFinance.Routes
{
    public static class ClientRoute
    {
        public static void ClientRoutes(this WebApplication app)
        {
            // All client endpoints require a valid JWT.
            var route = app.MapGroup("clients").RequireAuthorization();

            // GET /clients/{clientID}/PortfolioTotalAmount (legacy manual-entry products).
            route.MapGet("/{clientID}/PortfolioTotalAmount", async (IPortfolioService portfolio, ClaimsPrincipal user, int clientID) =>
            {
                var guard = AuthorizeClient(user, clientID);
                if (guard is not null) return guard;

                if (!await portfolio.HasConnectionsAsync(clientID))
                {
                    return Results.BadRequest(new { error = "Client has no connections" });
                }

                return Results.Ok(await portfolio.GetPortfolioTotalAsync(clientID));
            });

            // GET /clients/{clientID}/AssetsSummary (legacy manual-entry products).
            route.MapGet("/{clientID}/AssetsSummary", async (IPortfolioService portfolio, ClaimsPrincipal user, int clientID) =>
            {
                var guard = AuthorizeClient(user, clientID);
                if (guard is not null) return guard;

                if (!await portfolio.HasConnectionsAsync(clientID))
                {
                    return Results.BadRequest(new { error = "Client has no connections" });
                }

                var summary = await portfolio.GetAssetsSummaryAsync(clientID);
                if (summary.NumProducts == 0)
                {
                    return Results.BadRequest(new { error = "Client has no products" });
                }

                return Results.Ok(summary);
            });
        }

        // Ensures the authenticated client can only read their own data.
        // Returns an error result to short-circuit, or null when access is allowed.
        private static IResult? AuthorizeClient(ClaimsPrincipal user, int clientID)
        {
            var authenticatedId = user.GetClientId();
            if (authenticatedId is null) return Results.Unauthorized();
            if (authenticatedId != clientID) return Results.Forbid();
            return null;
        }
    }
}
