namespace WebAPI.OpenFinance.Aggregation
{
    // The single seam between OpenFinance and any external financial-data source.
    //
    // Implementations: MockAggregationProvider (now, $0), then Flinks/Plaid (banking) and SnapTrade
    // (brokerage), and eventually a native Canadian open-banking provider. The application and domain
    // depend only on this interface, so adding or swapping a provider is a new class, not a migration.
    public interface IAggregationProvider
    {
        // Identifier persisted on the connection, e.g. "Mock", "Flinks", "SnapTrade".
        string Name { get; }

        // Begins the account-linking flow; the returned token/URL is handed to the client UI.
        Task<LinkSession> CreateLinkSessionAsync(int clientId, CancellationToken ct = default);

        // Exchanges the short-lived public token from the link flow for a durable access token.
        Task<LinkResult> ExchangeTokenAsync(string publicToken, CancellationToken ct = default);

        // Pulls the current accounts, holdings, and transactions for a linked connection.
        Task<ProviderSnapshot> FetchSnapshotAsync(string accessToken, CancellationToken ct = default);
    }
}
