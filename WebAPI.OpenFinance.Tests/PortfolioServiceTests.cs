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
    }
}
