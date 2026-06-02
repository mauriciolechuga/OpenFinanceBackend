using Microsoft.EntityFrameworkCore;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Helpers
{
    // Queries and calculations for a client's portfolio.
    public static class ClientHelper
    {
        // All connection IDs that belong to the given client.
        public static async Task<List<int>> GetClientConnectionsByClientID(OpenFinanceContext context, int clientID)
        {
            return await context.Connections
                .Where(c => c.clientID == clientID)
                .Select(c => c.connectionID)
                .ToListAsync();
        }

        public static async Task<bool> CheckClientExists(OpenFinanceContext context, int clientID)
        {
            return await context.Clients.AnyAsync(c => c.clientID == clientID);
        }

        public static async Task<bool> CheckClientConnections(OpenFinanceContext context, int clientID)
        {
            return await context.Connections.AnyAsync(c => c.clientID == clientID);
        }

        // Sets PortfolioPercentage on each product as its share of the portfolio total, rounded to 2 decimals.
        public static void CalculatePercentageForEachProduct(List<ProductDetails> productDetails)
        {
            decimal totalAmount = CalculateTotalAmount(productDetails);

            foreach (var product in productDetails)
            {
                product.PortfolioPercentage = product.ProdTotal > 0
                    ? Math.Round((product.ProdTotal / totalAmount) * 100, 2)
                    : 0;
            }
        }

        public static decimal CalculateTotalAmount(List<ProductDetails> productDetails)
        {
            decimal totalAmount = 0;
            foreach (var product in productDetails)
            {
                totalAmount += product.ProdTotal;
            }
            return totalAmount;
        }

        // Counts only products the client actually holds (total > 0).
        public static int GetNumProducts(List<ProductDetails> productDetails)
        {
            int numProducts = 0;
            foreach (var product in productDetails)
            {
                if (product.ProdTotal > 0)
                {
                    numProducts++;
                }
            }
            return numProducts;
        }

        // Total cash across all of the client's connections.
        public static async Task<decimal> GetClientCashTotalAmount(OpenFinanceContext context, int clientID)
        {
            var clientConnections = await GetClientConnectionsByClientID(context, clientID);

            return await context.CashInfo
                .Where(c => clientConnections.Contains(c.connectionId))
                .SumAsync(c => c.amount);
        }

        // Total stock value: sum of quantity * last day price over the client's holdings.
        public static async Task<decimal> GetClienStockTotalAmount(OpenFinanceContext context, int clientID)
        {
            var clientConnections = await GetClientConnectionsByClientID(context, clientID);

            return await context.StockInfo
                .Where(si => clientConnections.Contains(si.connectionId))
                .Join(context.Stock,
                    si => si.stockId,
                    s => s.stockId,
                    (si, s) => si.quantity * s.lastDayPrice)
                .SumAsync();
        }

        // Total mutual fund value: sum of shares held * fund NAV over the client's holdings.
        public static async Task<decimal> GetClientMutualFundTotalAmount(OpenFinanceContext context, int clientID)
        {
            var clientConnections = await GetClientConnectionsByClientID(context, clientID);

            return await context.MutualFundInfo
                .Where(mf => clientConnections.Contains(mf.ConnectionID))
                .Join(context.MutualFund,
                    mf => mf.MFID,
                    m => m.MFID,
                    (mf, m) => mf.QuantityShares * m.MFNAV)
                .SumAsync();
        }
    }
}
