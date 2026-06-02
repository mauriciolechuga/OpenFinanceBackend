using Microsoft.EntityFrameworkCore;
using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Data
{
    public class OpenFinanceContext : DbContext
    {
        public OpenFinanceContext(DbContextOptions<OpenFinanceContext> options) : base(options)
        {
        }

        public DbSet<BanksModel> Banks { get; set; }
        public DbSet<ClientsModel> Clients { get; set; }
        public DbSet<ConnectionsModel> Connections { get; set; }
        public DbSet<ClientCredentialModel> ClientCredentials { get; set; }

        public DbSet<ProductTypesModel> ProductsTypes { get; set; }

        // Legacy per-product tables (manual entry).
        public DbSet<CashModel> Cash { get; set; }
        public DbSet<CashInfoModel> CashInfo { get; set; }
        public DbSet<StockModel> Stock { get; set; }
        public DbSet<StockInfoModel> StockInfo { get; set; }
        public DbSet<MutualFundModel> MutualFund { get; set; }
        public DbSet<MutualFundInfoModel> MutualFundInfo { get; set; }

        // Normalized aggregation model (populated from aggregation providers).
        public DbSet<AccountModel> Accounts { get; set; }
        public DbSet<SecurityModel> Securities { get; set; }
        public DbSet<HoldingModel> Holdings { get; set; }
        public DbSet<TransactionModel> Transactions { get; set; }
        public DbSet<BalanceSnapshotModel> BalanceSnapshots { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientCredentialModel>(entity =>
            {
                entity.Property(e => e.remainingLoginAttempts).HasDefaultValue(3);
                entity.Property(e => e.isBlocked).HasDefaultValue(false);
            });

            // Store enums as readable strings rather than integers.
            // Status defaults to "Pending" so legacy rows (added before this column) read back as a valid value.
            modelBuilder.Entity<ConnectionsModel>().Property(e => e.Status)
                .HasConversion<string>()
                .HasDefaultValue(ConnectionStatus.Pending);
            modelBuilder.Entity<AccountModel>().Property(e => e.Type).HasConversion<string>();
            modelBuilder.Entity<SecurityModel>().Property(e => e.Type).HasConversion<string>();
            modelBuilder.Entity<TransactionModel>().Property(e => e.Type).HasConversion<string>();

            // Reconciliation keys: a provider account/transaction id is unique within its scope,
            // so re-syncing updates the existing row instead of inserting duplicates.
            modelBuilder.Entity<AccountModel>()
                .HasIndex(a => new { a.ConnectionId, a.ExternalAccountId })
                .IsUnique();
            modelBuilder.Entity<TransactionModel>()
                .HasIndex(t => new { t.AccountId, t.ExternalTransactionId })
                .IsUnique();
            modelBuilder.Entity<HoldingModel>()
                .HasIndex(h => new { h.AccountId, h.SecurityId })
                .IsUnique();
        }
    }
}
