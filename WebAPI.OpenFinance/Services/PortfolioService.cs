using Microsoft.EntityFrameworkCore;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Dtos;
using WebAPI.OpenFinance.Helpers;
using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Services
{
    public class PortfolioService : IPortfolioService
    {
        // Single-currency assumption for the MVP; multi-currency conversion is a later concern.
        private const string DefaultCurrency = "CAD";

        private readonly OpenFinanceContext _context;

        public PortfolioService(OpenFinanceContext context)
        {
            _context = context;
        }

        public Task<bool> HasConnectionsAsync(int clientId) =>
            ClientHelper.CheckClientConnections(_context, clientId);

        public async Task<PortfolioTotalResponse> GetPortfolioTotalAsync(int clientId)
        {
            var cashTotal = await ClientHelper.GetClientCashTotalAmount(_context, clientId);
            var stockTotal = await ClientHelper.GetClienStockTotalAmount(_context, clientId);
            var mutualFundTotal = await ClientHelper.GetClientMutualFundTotalAmount(_context, clientId);

            var productTotals = new List<ProductTotalDto>
            {
                new("Stock", stockTotal),
                new("Mutual Fund", mutualFundTotal)
            };

            var totalAmount = cashTotal + stockTotal + mutualFundTotal;
            return new PortfolioTotalResponse(clientId, totalAmount, productTotals, DateTime.UtcNow);
        }

        public async Task<AssetsSummaryResponse> GetAssetsSummaryAsync(int clientId)
        {
            var productDetail = new List<ProductDetails>
            {
                new() { ProductName = "Stock", ProdTotal = await ClientHelper.GetClienStockTotalAmount(_context, clientId) },
                new() { ProductName = "Mutual Fund", ProdTotal = await ClientHelper.GetClientMutualFundTotalAmount(_context, clientId) }
            };

            ClientHelper.CalculatePercentageForEachProduct(productDetail);

            var details = productDetail
                .Select(p => new ProductDetailDto(p.ProductName, p.ProdTotal, p.PortfolioPercentage))
                .ToList();

            return new AssetsSummaryResponse(
                clientId,
                ClientHelper.GetNumProducts(productDetail),
                ClientHelper.CalculateTotalAmount(productDetail),
                details,
                DateTime.UtcNow);
        }

        public async Task<NetWorthResponse> GetNetWorthAsync(int clientId)
        {
            var connectionIds = await ConnectionIdsAsync(clientId);

            var cashAndBalances = await _context.Accounts
                .Where(a => connectionIds.Contains(a.ConnectionId))
                .SumAsync(a => a.CurrentBalance);

            var holdingsValue = await _context.Holdings
                .Where(h => connectionIds.Contains(h.Account.ConnectionId))
                .SumAsync(h => h.Quantity * h.Security.LastPrice);

            return new NetWorthResponse(clientId, cashAndBalances + holdingsValue, DefaultCurrency, DateTime.UtcNow);
        }

        public async Task<NetWorthHistoryResponse> GetNetWorthHistoryAsync(int clientId, int? days = null)
        {
            // A snapshot is written on every sync, so a day can hold several rows; collapse to one
            // point per calendar day (the latest snapshot of that day) for a clean chart series.
            var snapshots = await _context.BalanceSnapshots
                .Where(s => s.ClientId == clientId)
                .OrderBy(s => s.SnapshotDate)
                .ToListAsync();

            var points = snapshots
                .GroupBy(s => s.SnapshotDate.Date)
                .Select(g =>
                {
                    var latest = g.OrderBy(s => s.SnapshotDate).Last();
                    return new NetWorthPointDto(g.Key, latest.TotalNetWorth, latest.Currency);
                })
                .OrderBy(p => p.Date)
                .ToList();

            if (days is > 0)
            {
                var cutoff = DateTime.UtcNow.Date.AddDays(-(days.Value - 1));
                points = points.Where(p => p.Date >= cutoff).ToList();
            }

            return new NetWorthHistoryResponse(clientId, points, DateTime.UtcNow);
        }

        public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(int clientId)
        {
            var connectionIds = await ConnectionIdsAsync(clientId);

            return await _context.Accounts
                .Where(a => connectionIds.Contains(a.ConnectionId))
                .Select(a => new AccountDto(
                    a.AccountId, a.Name, a.Type.ToString(), a.Subtype,
                    a.Currency, a.CurrentBalance, a.AvailableBalance))
                .ToListAsync();
        }

        public async Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(int clientId)
        {
            var connectionIds = await ConnectionIdsAsync(clientId);

            return await _context.Holdings
                .Where(h => connectionIds.Contains(h.Account.ConnectionId))
                .Select(h => new HoldingDto(
                    h.HoldingId, h.Security.Symbol, h.Security.Name, h.Quantity,
                    h.Security.LastPrice, h.Quantity * h.Security.LastPrice, h.CostBasis))
                .ToListAsync();
        }

        public async Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(int clientId)
        {
            var connectionIds = await ConnectionIdsAsync(clientId);

            return await _context.Transactions
                .Where(t => connectionIds.Contains(t.Account.ConnectionId))
                .OrderByDescending(t => t.Date)
                .Take(100)
                .Select(t => new TransactionDto(
                    t.TransactionId, t.Date, t.Amount, t.Currency,
                    t.Description, t.Category, t.Type.ToString()))
                .ToListAsync();
        }

        private async Task<List<int>> ConnectionIdsAsync(int clientId) =>
            await _context.Connections
                .Where(c => c.clientID == clientId)
                .Select(c => c.connectionID)
                .ToListAsync();
    }
}
