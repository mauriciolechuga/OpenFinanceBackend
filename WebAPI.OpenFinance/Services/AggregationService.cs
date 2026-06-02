using Microsoft.EntityFrameworkCore;
using WebAPI.OpenFinance.Aggregation;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Dtos;
using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Services
{
    // Drives the link → exchange → sync pipeline against an IAggregationProvider, normalizing the
    // provider's data into the Accounts/Securities/Holdings/Transactions tables. Re-syncs are
    // idempotent: rows are reconciled on their provider ids rather than duplicated.
    public class AggregationService : IAggregationService
    {
        private const string DefaultCurrency = "CAD";

        private readonly OpenFinanceContext _context;
        private readonly IAggregationProvider _provider;
        private readonly ITokenProtector _tokenProtector;
        private readonly IPortfolioService _portfolio;
        private readonly ILogger<AggregationService> _logger;

        public AggregationService(
            OpenFinanceContext context,
            IAggregationProvider provider,
            ITokenProtector tokenProtector,
            IPortfolioService portfolio,
            ILogger<AggregationService> logger)
        {
            _context = context;
            _provider = provider;
            _tokenProtector = tokenProtector;
            _portfolio = portfolio;
            _logger = logger;
        }

        public async Task<LinkSessionResponse> CreateLinkSessionAsync(int clientId, CancellationToken ct = default)
        {
            var session = await _provider.CreateLinkSessionAsync(clientId, ct);
            return new LinkSessionResponse(session.LinkToken, session.LinkUrl, _provider.Name);
        }

        public async Task<SyncResultResponse?> CompleteLinkAsync(int clientId, ExchangeLinkRequest request, CancellationToken ct = default)
        {
            if (!await _context.Banks.AnyAsync(b => b.bankID == request.BankId, ct))
            {
                return null;
            }

            var link = await _provider.ExchangeTokenAsync(request.PublicToken, ct);

            var connection = new ConnectionsModel
            {
                clientID = clientId,
                bankID = request.BankId,
                accountNumber = 0,
                Provider = _provider.Name,
                ProviderItemId = link.ProviderItemId,
                AccessTokenEncrypted = _tokenProtector.Protect(link.AccessToken),
                Status = ConnectionStatus.Active
            };

            _context.Connections.Add(connection);
            await _context.SaveChangesAsync(ct);

            return await SyncConnectionAsync(connection.connectionID, ct);
        }

        public async Task<SyncResultResponse> SyncConnectionAsync(int connectionId, CancellationToken ct = default)
        {
            var connection = await _context.Connections.FirstOrDefaultAsync(c => c.connectionID == connectionId, ct)
                ?? throw new InvalidOperationException($"Connection {connectionId} not found");

            if (string.IsNullOrEmpty(connection.AccessTokenEncrypted))
            {
                throw new InvalidOperationException($"Connection {connectionId} has no stored access token");
            }

            try
            {
                var token = _tokenProtector.Unprotect(connection.AccessTokenEncrypted);
                var snapshot = await _provider.FetchSnapshotAsync(token, ct);

                await UpsertAccountsAndSecuritiesAsync(connectionId, snapshot, ct);

                var accounts = await _context.Accounts.Where(a => a.ConnectionId == connectionId).ToListAsync(ct);
                var accountByExternal = accounts.ToDictionary(a => a.ExternalAccountId, a => a);
                var securityBySymbol = await SecuritiesBySymbolAsync(snapshot, ct);

                await UpsertHoldingsAsync(accounts, accountByExternal, securityBySymbol, snapshot, ct);
                await InsertNewTransactionsAsync(accounts, accountByExternal, snapshot, ct);

                connection.Status = ConnectionStatus.Active;
                connection.LastSyncedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                await WriteBalanceSnapshotAsync(connection.clientID, ct);

                return new SyncResultResponse(
                    connectionId, snapshot.Accounts.Count, snapshot.Holdings.Count, snapshot.Transactions.Count, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed for connection {ConnectionId}", connectionId);
                connection.Status = ConnectionStatus.Error;
                await _context.SaveChangesAsync(ct);
                throw;
            }
        }

        public async Task<SyncResultResponse?> SyncOwnedConnectionAsync(int clientId, int connectionId, CancellationToken ct = default)
        {
            var owns = await _context.Connections.AnyAsync(c => c.connectionID == connectionId && c.clientID == clientId, ct);
            return owns ? await SyncConnectionAsync(connectionId, ct) : null;
        }

        public async Task<int> SyncAllActiveAsync(CancellationToken ct = default)
        {
            var ids = await _context.Connections
                .Where(c => c.Status == ConnectionStatus.Active && c.AccessTokenEncrypted != null)
                .Select(c => c.connectionID)
                .ToListAsync(ct);

            var synced = 0;
            foreach (var id in ids)
            {
                try
                {
                    await SyncConnectionAsync(id, ct);
                    synced++;
                }
                catch (Exception ex)
                {
                    // One bad connection should not abort the whole batch.
                    _logger.LogError(ex, "Background sync failed for connection {ConnectionId}", id);
                }
            }
            return synced;
        }

        private async Task UpsertAccountsAndSecuritiesAsync(int connectionId, ProviderSnapshot snapshot, CancellationToken ct)
        {
            var existingAccounts = await _context.Accounts.Where(a => a.ConnectionId == connectionId).ToListAsync(ct);

            foreach (var pa in snapshot.Accounts)
            {
                var account = existingAccounts.FirstOrDefault(a => a.ExternalAccountId == pa.ExternalAccountId);
                if (account == null)
                {
                    account = new AccountModel { ConnectionId = connectionId, ExternalAccountId = pa.ExternalAccountId };
                    _context.Accounts.Add(account);
                }

                account.Name = pa.Name;
                account.Type = pa.Type;
                account.Subtype = pa.Subtype;
                account.Currency = pa.Currency;
                account.CurrentBalance = pa.CurrentBalance;
                account.AvailableBalance = pa.AvailableBalance;
                account.LastUpdated = DateTime.UtcNow;
            }

            var symbols = snapshot.Holdings.Select(h => h.Security.Symbol).Distinct().ToList();
            var existingSecurities = await _context.Securities.Where(s => symbols.Contains(s.Symbol)).ToListAsync(ct);

            foreach (var ph in snapshot.Holdings)
            {
                var security = existingSecurities.FirstOrDefault(s => s.Symbol == ph.Security.Symbol);
                if (security == null)
                {
                    security = new SecurityModel { Symbol = ph.Security.Symbol };
                    _context.Securities.Add(security);
                    existingSecurities.Add(security);
                }

                security.Name = ph.Security.Name;
                security.Type = ph.Security.Type;
                security.Currency = ph.Security.Currency;
                security.LastPrice = ph.Security.LastPrice;
                security.LastUpdated = DateTime.UtcNow;
            }

            // Persist so accounts and securities have database ids before holdings/transactions reference them.
            await _context.SaveChangesAsync(ct);
        }

        private async Task<Dictionary<string, SecurityModel>> SecuritiesBySymbolAsync(ProviderSnapshot snapshot, CancellationToken ct)
        {
            var symbols = snapshot.Holdings.Select(h => h.Security.Symbol).Distinct().ToList();
            return await _context.Securities.Where(s => symbols.Contains(s.Symbol)).ToDictionaryAsync(s => s.Symbol, s => s, ct);
        }

        private async Task UpsertHoldingsAsync(
            List<AccountModel> accounts,
            Dictionary<string, AccountModel> accountByExternal,
            Dictionary<string, SecurityModel> securityBySymbol,
            ProviderSnapshot snapshot,
            CancellationToken ct)
        {
            var accountIds = accounts.Select(a => a.AccountId).ToList();
            var existingHoldings = await _context.Holdings.Where(h => accountIds.Contains(h.AccountId)).ToListAsync(ct);

            foreach (var ph in snapshot.Holdings)
            {
                if (!accountByExternal.TryGetValue(ph.ExternalAccountId, out var account)) continue;
                if (!securityBySymbol.TryGetValue(ph.Security.Symbol, out var security)) continue;

                var holding = existingHoldings.FirstOrDefault(h => h.AccountId == account.AccountId && h.SecurityId == security.SecurityId);
                if (holding == null)
                {
                    holding = new HoldingModel { AccountId = account.AccountId, SecurityId = security.SecurityId };
                    _context.Holdings.Add(holding);
                }

                holding.Quantity = ph.Quantity;
                holding.CostBasis = ph.CostBasis;
                holding.LastUpdated = DateTime.UtcNow;
            }
        }

        private async Task InsertNewTransactionsAsync(
            List<AccountModel> accounts,
            Dictionary<string, AccountModel> accountByExternal,
            ProviderSnapshot snapshot,
            CancellationToken ct)
        {
            var accountIds = accounts.Select(a => a.AccountId).ToList();
            var existing = await _context.Transactions
                .Where(t => accountIds.Contains(t.AccountId))
                .Select(t => new { t.AccountId, t.ExternalTransactionId })
                .ToListAsync(ct);
            var existingKeys = existing.Select(x => (x.AccountId, x.ExternalTransactionId)).ToHashSet();

            foreach (var pt in snapshot.Transactions)
            {
                if (!accountByExternal.TryGetValue(pt.ExternalAccountId, out var account)) continue;
                if (existingKeys.Contains((account.AccountId, pt.ExternalTransactionId))) continue;

                _context.Transactions.Add(new TransactionModel
                {
                    AccountId = account.AccountId,
                    ExternalTransactionId = pt.ExternalTransactionId,
                    Date = pt.Date,
                    Amount = pt.Amount,
                    Currency = pt.Currency,
                    Description = pt.Description,
                    Category = pt.Category,
                    Type = pt.Type
                });
            }
        }

        private async Task WriteBalanceSnapshotAsync(int clientId, CancellationToken ct)
        {
            var netWorth = await _portfolio.GetNetWorthAsync(clientId);
            _context.BalanceSnapshots.Add(new BalanceSnapshotModel
            {
                ClientId = clientId,
                SnapshotDate = DateTime.UtcNow,
                TotalNetWorth = netWorth.NetWorth,
                Currency = DefaultCurrency
            });
            await _context.SaveChangesAsync(ct);
        }
    }
}
