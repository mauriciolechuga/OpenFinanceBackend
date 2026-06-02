namespace WebAPI.OpenFinance.Dtos
{
    // Response when starting an account link; the client uses these to launch the provider flow.
    public record LinkSessionResponse(string LinkToken, string LinkUrl, string Provider);

    // Body posted back after the client completes the provider link flow.
    public record ExchangeLinkRequest(int BankId, string PublicToken);

    public record SyncResultResponse(int ConnectionId, int Accounts, int Holdings, int Transactions, DateTime SyncedAt);

    public record AccountDto(
        int AccountId,
        string Name,
        string Type,
        string? Subtype,
        string Currency,
        decimal CurrentBalance,
        decimal? AvailableBalance);

    public record HoldingDto(
        int HoldingId,
        string Symbol,
        string SecurityName,
        decimal Quantity,
        decimal LastPrice,
        decimal MarketValue,
        decimal? CostBasis);

    public record TransactionDto(
        int TransactionId,
        DateTime Date,
        decimal Amount,
        string Currency,
        string Description,
        string? Category,
        string Type);

    public record NetWorthResponse(int ClientId, decimal NetWorth, string Currency, DateTime Timestamp);
}
