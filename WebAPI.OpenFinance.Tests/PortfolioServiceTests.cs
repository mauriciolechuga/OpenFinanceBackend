using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Models;
using WebAPI.OpenFinance.Services;
using Xunit;

namespace WebAPI.OpenFinance.Tests
{
    public class PortfolioServiceTests
    {
        // Seeds one client with one connection holding a chequing account and a single equity position.
        private static async Task<int> SeedAsync(OpenFinanceContext ctx)
        {
            var client = new ClientsModel { clientName = "Test", clientEmail = "t@e.com", clientAddress = "123 Main Street" };
            var bank = new BanksModel { bankName = "RBC" };
            ctx.Clients.Add(client);
            ctx.Banks.Add(bank);
            await ctx.SaveChangesAsync();

            var connection = new ConnectionsModel
            {
                clientID = client.clientID,
                bankID = bank.bankID,
                accountNumber = 1,
                Provider = "Mock",
                Status = ConnectionStatus.Active
            };
            ctx.Connections.Add(connection);
            await ctx.SaveChangesAsync();

            var chequing = new AccountModel
            {
                ConnectionId = connection.connectionID,
                ExternalAccountId = "a1",
                Name = "Chequing",
                Type = AccountType.Chequing,
                Currency = "CAD",
                CurrentBalance = 1000m,
                LastUpdated = DateTime.UtcNow
            };
            var brokerage = new AccountModel
            {
                ConnectionId = connection.connectionID,
                ExternalAccountId = "a2",
                Name = "TFSA",
                Type = AccountType.Tfsa,
                Currency = "CAD",
                CurrentBalance = 0m,
                LastUpdated = DateTime.UtcNow
            };
            ctx.Accounts.AddRange(chequing, brokerage);

            var security = new SecurityModel
            {
                Symbol = "VFV.TO", Name = "Vanguard S&P 500", Type = SecurityType.Etf,
                Currency = "CAD", LastPrice = 100m, LastUpdated = DateTime.UtcNow
            };
            ctx.Securities.Add(security);
            await ctx.SaveChangesAsync();

            ctx.Holdings.Add(new HoldingModel
            {
                AccountId = brokerage.AccountId, SecurityId = security.SecurityId,
                Quantity = 10m, LastUpdated = DateTime.UtcNow
            });
            ctx.Transactions.Add(new TransactionModel
            {
                AccountId = chequing.AccountId, ExternalTransactionId = "t1", Date = DateTime.UtcNow,
                Amount = 50m, Currency = "CAD", Description = "Coffee", Type = TransactionType.Debit
            });
            await ctx.SaveChangesAsync();

            return client.clientID;
        }

        [Fact]
        public async Task NetWorth_is_balances_plus_holdings_value()
        {
            var db = Guid.NewGuid().ToString();
            int clientId;
            await using (var ctx = TestSupport.NewContext(db)) { clientId = await SeedAsync(ctx); }

            await using var read = TestSupport.NewContext(db);
            var netWorth = await new PortfolioService(read).GetNetWorthAsync(clientId);

            // 1000 (chequing) + 10 * 100 (holding) = 2000
            Assert.Equal(2000m, netWorth.NetWorth);
            Assert.Equal("CAD", netWorth.Currency);
        }

        [Fact]
        public async Task Queries_return_the_clients_accounts_holdings_and_transactions()
        {
            var db = Guid.NewGuid().ToString();
            int clientId;
            await using (var ctx = TestSupport.NewContext(db)) { clientId = await SeedAsync(ctx); }

            await using var read = TestSupport.NewContext(db);
            var service = new PortfolioService(read);

            Assert.Equal(2, (await service.GetAccountsAsync(clientId)).Count);

            var holdings = await service.GetHoldingsAsync(clientId);
            Assert.Single(holdings);
            Assert.Equal(1000m, holdings[0].MarketValue); // 10 * 100

            Assert.Single(await service.GetTransactionsAsync(clientId));
        }

        [Fact]
        public async Task NetWorth_history_collapses_to_one_point_per_day_and_honours_days_filter()
        {
            var db = Guid.NewGuid().ToString();
            int clientId;
            await using (var ctx = TestSupport.NewContext(db))
            {
                clientId = await SeedAsync(ctx);

                var today = DateTime.UtcNow.Date;
                ctx.BalanceSnapshots.AddRange(
                    Snapshot(clientId, today.AddDays(-2).AddHours(9), 1000m),
                    // Two snapshots on the same day: the later one wins.
                    Snapshot(clientId, today.AddDays(-1).AddHours(8), 1500m),
                    Snapshot(clientId, today.AddDays(-1).AddHours(17), 1600m),
                    Snapshot(clientId, today.AddHours(10), 2000m));
                await ctx.SaveChangesAsync();
            }

            await using var read = TestSupport.NewContext(db);
            var service = new PortfolioService(read);

            var history = await service.GetNetWorthHistoryAsync(clientId);

            // 4 rows over 3 calendar days collapse to 3 ascending points, same-day latest wins.
            Assert.Equal(3, history.Points.Count);
            Assert.Equal(new[] { 1000m, 1600m, 2000m }, history.Points.Select(p => p.NetWorth).ToArray());
            Assert.True(history.Points[0].Date < history.Points[1].Date);
            Assert.Equal("CAD", history.Points[0].Currency);

            // days=2 keeps only yesterday and today.
            var recent = await service.GetNetWorthHistoryAsync(clientId, days: 2);
            Assert.Equal(new[] { 1600m, 2000m }, recent.Points.Select(p => p.NetWorth).ToArray());
        }

        private static BalanceSnapshotModel Snapshot(int clientId, DateTime date, decimal netWorth) =>
            new() { ClientId = clientId, SnapshotDate = date, TotalNetWorth = netWorth, Currency = "CAD" };
    }
}
