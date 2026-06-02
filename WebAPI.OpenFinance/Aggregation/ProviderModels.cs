using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Aggregation
{
    // Provider-agnostic shapes returned by an IAggregationProvider. Each concrete provider maps its
    // own API responses into these so the rest of the system never depends on a specific vendor.

    // The handoff needed to start a provider's account-linking flow on the client.
    public record LinkSession(string LinkToken, string LinkUrl);

    // The result of exchanging a provider's public token for a durable item/access token.
    public record LinkResult(string ProviderItemId, string AccessToken);

    public record ProviderAccount(
        string ExternalAccountId,
        string Name,
        AccountType Type,
        string? Subtype,
        string Currency,
        decimal CurrentBalance,
        decimal? AvailableBalance);

    public record ProviderSecurity(
        string Symbol,
        string Name,
        SecurityType Type,
        string Currency,
        decimal LastPrice);

    public record ProviderHolding(
        string ExternalAccountId,
        ProviderSecurity Security,
        decimal Quantity,
        decimal? CostBasis);

    public record ProviderTransaction(
        string ExternalAccountId,
        string ExternalTransactionId,
        DateTime Date,
        decimal Amount,
        string Currency,
        string Description,
        string? Category,
        TransactionType Type);

    // Everything pulled for a connection in a single sync pass.
    public record ProviderSnapshot(
        IReadOnlyList<ProviderAccount> Accounts,
        IReadOnlyList<ProviderHolding> Holdings,
        IReadOnlyList<ProviderTransaction> Transactions);
}
