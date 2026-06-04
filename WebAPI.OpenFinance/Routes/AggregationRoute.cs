using System.Security.Claims;
using WebAPI.OpenFinance.Auth;
using WebAPI.OpenFinance.Dtos;
using WebAPI.OpenFinance.Services;

namespace WebAPI.OpenFinance.Routes
{
    public static class AggregationRoute
    {
        public static void AggregationRoutes(this WebApplication app)
        {
            MapConnectionRoutes(app);
            MapPortfolioRoutes(app);
            MapWebhookRoutes(app);
        }

        // Linking and syncing connections. Client identity always comes from the JWT, never the body.
        private static void MapConnectionRoutes(WebApplication app)
        {
            var route = app.MapGroup("connections").RequireAuthorization();

            // POST /connections/link → start the provider link flow.
            route.MapPost("/link", async (IAggregationService aggregation, ClaimsPrincipal user) =>
            {
                var clientId = user.GetClientId();
                if (clientId is null) return Results.Unauthorized();
                return Results.Ok(await aggregation.CreateLinkSessionAsync(clientId.Value));
            });

            // POST /connections/exchange → complete the link and run the initial sync.
            route.MapPost("/exchange", async (IAggregationService aggregation, ClaimsPrincipal user, ExchangeLinkRequest request) =>
            {
                var clientId = user.GetClientId();
                if (clientId is null) return Results.Unauthorized();

                var result = await aggregation.CompleteLinkAsync(clientId.Value, request);
                return result is null
                    ? Results.BadRequest(new { error = "Unknown bank" })
                    : Results.Ok(result);
            });

            // POST /connections/{connectionId}/sync → manual refresh of a connection the client owns.
            route.MapPost("/{connectionId:int}/sync", async (IAggregationService aggregation, ClaimsPrincipal user, int connectionId) =>
            {
                var clientId = user.GetClientId();
                if (clientId is null) return Results.Unauthorized();

                var result = await aggregation.SyncOwnedConnectionAsync(clientId.Value, connectionId);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
        }

        // Read models built from synced data. Identity comes from the JWT.
        private static void MapPortfolioRoutes(WebApplication app)
        {
            var route = app.MapGroup("portfolio").RequireAuthorization();

            route.MapGet("/networth", async (IPortfolioService portfolio, ClaimsPrincipal user) =>
                await WithClient(user, id => portfolio.GetNetWorthAsync(id)));

            // Net-worth-over-time series; optional ?days=N limits to the last N days.
            route.MapGet("/networth/history", async (IPortfolioService portfolio, ClaimsPrincipal user, int? days) =>
                await WithClient(user, id => portfolio.GetNetWorthHistoryAsync(id, days)));

            route.MapGet("/accounts", async (IPortfolioService portfolio, ClaimsPrincipal user) =>
                await WithClient(user, id => portfolio.GetAccountsAsync(id)));

            route.MapGet("/holdings", async (IPortfolioService portfolio, ClaimsPrincipal user) =>
                await WithClient(user, id => portfolio.GetHoldingsAsync(id)));

            route.MapGet("/transactions", async (IPortfolioService portfolio, ClaimsPrincipal user) =>
                await WithClient(user, id => portfolio.GetTransactionsAsync(id)));
        }

        // Provider push notifications. In production this must verify a provider signature; the mock
        // provider simply names the connection to refresh.
        private static void MapWebhookRoutes(WebApplication app)
        {
            app.MapPost("/webhooks/aggregator", async (IAggregationService aggregation, AggregatorWebhook payload) =>
            {
                await aggregation.SyncConnectionAsync(payload.ConnectionId);
                return Results.Ok();
            }).AllowAnonymous();
        }

        // Resolves the client id from the token and runs the query, returning 401 if unauthenticated.
        private static async Task<IResult> WithClient<T>(ClaimsPrincipal user, Func<int, Task<T>> query)
        {
            var clientId = user.GetClientId();
            if (clientId is null) return Results.Unauthorized();
            return Results.Ok(await query(clientId.Value));
        }
    }

    public record AggregatorWebhook(int ConnectionId);
}
