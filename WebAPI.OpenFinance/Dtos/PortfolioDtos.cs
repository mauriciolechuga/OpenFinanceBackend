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
}
