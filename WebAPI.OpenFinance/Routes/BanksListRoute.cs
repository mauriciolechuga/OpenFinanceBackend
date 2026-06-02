using Microsoft.EntityFrameworkCore;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Routes
{
    public static class BanksListRoute
    {
        public static void BanksListRoutes(this WebApplication app)
        {
            // Requires a valid JWT (the bank list is consumed by the authenticated linking flow).
            var route = app.MapGroup("bankslist").RequireAuthorization();

            // GET /bankslist → all banks.
            route.MapGet("/", async (OpenFinanceContext context) =>
            {
                var banks = await context.Banks.ToListAsync();
                return Results.Ok(banks);
            });

            // POST /bankslist/addBanks → bulk-insert banks (seed/admin helper).
            route.MapPost("/addBanks", async (OpenFinanceContext context, List<BanksModel> banks) =>
            {
                context.Banks.AddRange(banks);
                await context.SaveChangesAsync();
                return Results.Ok("Banks inserted!");
            });
        }
    }
}
