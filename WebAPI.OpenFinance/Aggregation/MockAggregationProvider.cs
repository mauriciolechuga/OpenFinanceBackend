using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Aggregation
{
    // Zero-cost provider that returns deterministic sandbox data, so the whole link → sync → display
    // pipeline can be built and tested without paying for or integrating a real aggregator.
    // Replace with FlinksProvider / SnapTradeProvider when a sandbox key is available.
    public class MockAggregationProvider : IAggregationProvider
    {
        public string Name => "Mock";

        public Task<LinkSession> CreateLinkSessionAsync(int clientId, CancellationToken ct = default)
        {
            // A real provider returns an opaque token consumed by its client SDK; here we fabricate one.
            var token = $"mock-link-{clientId}-{Guid.NewGuid():N}";
            return Task.FromResult(new LinkSession(token, $"https://mock-aggregator.local/link?token={token}"));
        }

        public Task<LinkResult> ExchangeTokenAsync(string publicToken, CancellationToken ct = default)
        {
            var itemId = $"mock-item-{Guid.NewGuid():N}";
            var accessToken = $"mock-access-{Guid.NewGuid():N}";
            return Task.FromResult(new LinkResult(itemId, accessToken));
        }

        public Task<ProviderSnapshot> FetchSnapshotAsync(string accessToken, CancellationToken ct = default)
        {
            // Deterministic per access token so repeated syncs are stable (idempotent reconciliation).
            var seed = accessToken.Aggregate(0, (acc, c) => acc + c);
            var rng = new Random(seed);

            const string cad = "CAD";
            var now = DateTime.UtcNow;

            var chequing = new ProviderAccount("acct-chequing", "Everyday Chequing", AccountType.Chequing, "personal", cad,
                Math.Round(1000 + (decimal)rng.NextDouble() * 4000, 2), null);
            var savings = new ProviderAccount("acct-savings", "High-Interest Savings", AccountType.Savings, "personal", cad,
                Math.Round(5000 + (decimal)rng.NextDouble() * 15000, 2), null);
            var tfsa = new ProviderAccount("acct-tfsa", "TFSA Investment", AccountType.Tfsa, "registered", cad,
                0m, null); // brokerage balance is derived from holdings below

            var vfv = new ProviderSecurity("VFV.TO", "Vanguard S&P 500 Index ETF", SecurityType.Etf, cad,
                Math.Round(110 + (decimal)rng.NextDouble() * 30, 2));
            var xeqt = new ProviderSecurity("XEQT.TO", "iShares Core Equity ETF Portfolio", SecurityType.Etf, cad,
                Math.Round(28 + (decimal)rng.NextDouble() * 8, 2));

            var holdings = new List<ProviderHolding>
            {
                new("acct-tfsa", vfv, rng.Next(10, 60), Math.Round(1000 + (decimal)rng.NextDouble() * 4000, 2)),
                new("acct-tfsa", xeqt, rng.Next(20, 120), Math.Round(800 + (decimal)rng.NextDouble() * 3000, 2)),
            };

            var transactions = new List<ProviderTransaction>
            {
                new("acct-chequing", "txn-1", now.AddDays(-1), 2500.00m, cad, "Payroll deposit", "Income", TransactionType.Credit),
                new("acct-chequing", "txn-2", now.AddDays(-2), 84.20m, cad, "Grocery store", "Groceries", TransactionType.Debit),
                new("acct-chequing", "txn-3", now.AddDays(-3), 19.99m, cad, "Streaming subscription", "Entertainment", TransactionType.Debit),
                new("acct-savings", "txn-4", now.AddDays(-5), 12.55m, cad, "Interest earned", "Interest", TransactionType.Credit),
            };

            var snapshot = new ProviderSnapshot(
                new[] { chequing, savings, tfsa },
                holdings,
                transactions);

            return Task.FromResult(snapshot);
        }
    }
}
