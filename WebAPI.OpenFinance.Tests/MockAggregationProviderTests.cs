using WebAPI.OpenFinance.Aggregation;
using Xunit;

namespace WebAPI.OpenFinance.Tests
{
    public class MockAggregationProviderTests
    {
        private readonly MockAggregationProvider _provider = new();

        [Fact]
        public async Task FetchSnapshot_returns_accounts_holdings_and_transactions()
        {
            var snapshot = await _provider.FetchSnapshotAsync("mock-access-token");

            Assert.Equal(3, snapshot.Accounts.Count);
            Assert.Equal(2, snapshot.Holdings.Count);
            Assert.Equal(4, snapshot.Transactions.Count);
        }

        [Fact]
        public async Task FetchSnapshot_is_deterministic_for_the_same_token()
        {
            var first = await _provider.FetchSnapshotAsync("same-token");
            var second = await _provider.FetchSnapshotAsync("same-token");

            // Same balances each call → idempotent reconciliation on re-sync.
            Assert.Equal(first.Accounts[0].CurrentBalance, second.Accounts[0].CurrentBalance);
            Assert.Equal(first.Holdings[0].Quantity, second.Holdings[0].Quantity);
        }

        [Fact]
        public async Task ExchangeToken_returns_item_and_access_token()
        {
            var result = await _provider.ExchangeTokenAsync("public-token");

            Assert.False(string.IsNullOrWhiteSpace(result.ProviderItemId));
            Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        }
    }
}
