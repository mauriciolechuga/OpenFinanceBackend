using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebAPI.OpenFinance.Aggregation;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Dtos;
using WebAPI.OpenFinance.Models;
using WebAPI.OpenFinance.Services;
using Xunit;

namespace WebAPI.OpenFinance.Tests
{
    public class AggregationServiceTests
    {
        private static AggregationService NewService(OpenFinanceContext ctx) =>
            new(ctx, new MockAggregationProvider(), new FakeTokenProtector(),
                new PortfolioService(ctx), NullLogger<AggregationService>.Instance);

        private static async Task<(int clientId, int bankId)> SeedClientAndBankAsync(OpenFinanceContext ctx)
        {
            var client = new ClientsModel { clientName = "Test", clientEmail = "t@e.com", clientAddress = "123 Main Street" };
            var bank = new BanksModel { bankName = "RBC" };
            ctx.Clients.Add(client);
            ctx.Banks.Add(bank);
            await ctx.SaveChangesAsync();
            return (client.clientID, bank.bankID);
        }

        [Fact]
        public async Task CompleteLink_creates_connection_and_syncs_data()
        {
            var db = Guid.NewGuid().ToString();
            int clientId, bankId;
            await using (var seed = TestSupport.NewContext(db))
            {
                (clientId, bankId) = await SeedClientAndBankAsync(seed);
            }

            SyncResultResponse? result;
            await using (var ctx = TestSupport.NewContext(db))
            {
                result = await NewService(ctx).CompleteLinkAsync(clientId, new ExchangeLinkRequest(bankId, "public-token"));
            }

            Assert.NotNull(result);
            Assert.Equal(3, result!.Accounts);
            Assert.Equal(2, result.Holdings);
            Assert.Equal(4, result.Transactions);

            await using var assert = TestSupport.NewContext(db);
            Assert.Equal(3, await assert.Accounts.CountAsync());
            Assert.Equal(2, await assert.Holdings.CountAsync());
            Assert.Equal(4, await assert.Transactions.CountAsync());
            Assert.Equal(1, await assert.BalanceSnapshots.CountAsync());

            var connection = await assert.Connections.SingleAsync();
            Assert.Equal("Mock", connection.Provider);
            Assert.Equal(ConnectionStatus.Active, connection.Status);
            Assert.False(string.IsNullOrEmpty(connection.AccessTokenEncrypted));
        }

        [Fact]
        public async Task CompleteLink_with_unknown_bank_returns_null()
        {
            var db = Guid.NewGuid().ToString();
            int clientId;
            await using (var seed = TestSupport.NewContext(db))
            {
                (clientId, _) = await SeedClientAndBankAsync(seed);
            }

            await using var ctx = TestSupport.NewContext(db);
            var result = await NewService(ctx).CompleteLinkAsync(clientId, new ExchangeLinkRequest(9999, "public-token"));

            Assert.Null(result);
        }

        [Fact]
        public async Task Resync_is_idempotent()
        {
            var db = Guid.NewGuid().ToString();
            int clientId, bankId, connectionId;
            await using (var seed = TestSupport.NewContext(db))
            {
                (clientId, bankId) = await SeedClientAndBankAsync(seed);
            }
            await using (var ctx = TestSupport.NewContext(db))
            {
                await NewService(ctx).CompleteLinkAsync(clientId, new ExchangeLinkRequest(bankId, "public-token"));
            }
            await using (var ctx = TestSupport.NewContext(db))
            {
                connectionId = (await ctx.Connections.SingleAsync()).connectionID;
                await NewService(ctx).SyncOwnedConnectionAsync(clientId, connectionId);
            }

            await using var assert = TestSupport.NewContext(db);
            // Rows reconciled on their provider ids, not duplicated.
            Assert.Equal(3, await assert.Accounts.CountAsync());
            Assert.Equal(2, await assert.Holdings.CountAsync());
            Assert.Equal(4, await assert.Transactions.CountAsync());
            // One snapshot recorded per sync.
            Assert.Equal(2, await assert.BalanceSnapshots.CountAsync());
        }

        [Fact]
        public async Task Sync_of_unowned_connection_returns_null()
        {
            var db = Guid.NewGuid().ToString();
            int ownerId, bankId, connectionId;
            await using (var seed = TestSupport.NewContext(db))
            {
                (ownerId, bankId) = await SeedClientAndBankAsync(seed);
            }
            await using (var ctx = TestSupport.NewContext(db))
            {
                await NewService(ctx).CompleteLinkAsync(ownerId, new ExchangeLinkRequest(bankId, "public-token"));
            }

            await using var attacker = TestSupport.NewContext(db);
            connectionId = (await attacker.Connections.SingleAsync()).connectionID;
            var result = await NewService(attacker).SyncOwnedConnectionAsync(ownerId + 1, connectionId);

            Assert.Null(result);
        }
    }
}
