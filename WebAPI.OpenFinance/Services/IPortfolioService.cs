using WebAPI.OpenFinance.Dtos;

namespace WebAPI.OpenFinance.Services
{
    public interface IPortfolioService
    {
        Task<bool> HasConnectionsAsync(int clientId);

        // Legacy (manual-entry) product totals.
        Task<PortfolioTotalResponse> GetPortfolioTotalAsync(int clientId);
        Task<AssetsSummaryResponse> GetAssetsSummaryAsync(int clientId);

        // Aggregation model (synced from providers).
        Task<NetWorthResponse> GetNetWorthAsync(int clientId);
        Task<IReadOnlyList<AccountDto>> GetAccountsAsync(int clientId);
        Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(int clientId);
        Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(int clientId);
    }
}
