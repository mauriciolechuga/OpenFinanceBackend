namespace WebAPI.OpenFinance.Dtos
{
    public record ProductTotalDto(string Product, decimal Total);

    public record PortfolioTotalResponse(
        int ClientId,
        decimal TotalAmount,
        IReadOnlyList<ProductTotalDto> ProductTotals,
        DateTime Timestamp);

    public record ProductDetailDto(string ProductName, decimal Total, decimal PortfolioPercentage);

    public record AssetsSummaryResponse(
        int ClientId,
        int NumProducts,
        decimal TotalAmount,
        IReadOnlyList<ProductDetailDto> ProductDetails,
        DateTime Timestamp);

    // One day's net-worth value in the history series.
    public record NetWorthPointDto(DateTime Date, decimal NetWorth, string Currency);

    // Net-worth-over-time series powering the analysis/charts screen.
    public record NetWorthHistoryResponse(
        int ClientId,
        IReadOnlyList<NetWorthPointDto> Points,
        DateTime Timestamp);
}
