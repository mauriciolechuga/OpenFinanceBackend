using WebAPI.OpenFinance.Dtos;

namespace WebAPI.OpenFinance.Services
{
    public interface IAggregationService
    {
        // Starts a provider link flow for the client.
        Task<LinkSessionResponse> CreateLinkSessionAsync(int clientId, CancellationToken ct = default);

        // Completes the link: exchanges the public token, stores the connection, and runs an initial sync.
        // Returns null if the referenced bank does not exist.
        Task<SyncResultResponse?> CompleteLinkAsync(int clientId, ExchangeLinkRequest request, CancellationToken ct = default);

        // Re-syncs a single connection (used by the webhook and background sync).
        Task<SyncResultResponse> SyncConnectionAsync(int connectionId, CancellationToken ct = default);

        // Re-syncs a connection only if it belongs to the given client; returns null otherwise.
        Task<SyncResultResponse?> SyncOwnedConnectionAsync(int clientId, int connectionId, CancellationToken ct = default);

        // Re-syncs every active connection (used by the periodic background sync).
        Task<int> SyncAllActiveAsync(CancellationToken ct = default);
    }
}
