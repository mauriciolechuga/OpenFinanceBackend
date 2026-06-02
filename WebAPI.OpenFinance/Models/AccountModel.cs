using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.OpenFinance.Models
{
    // A single account (chequing, savings, credit card, brokerage, etc.) discovered within a
    // Connection via an aggregation provider. Normalized so every provider maps to the same shape.
    [Table("accounts")]
    public class AccountModel
    {
        [Key]
        [Column("account_id")]
        public int AccountId { get; init; }

        [Column("connection_id")]
        public int ConnectionId { get; set; }
        [ForeignKey("ConnectionId")]
        public ConnectionsModel Connection { get; set; }

        // The provider's own identifier for this account (used to reconcile on re-sync).
        [Required]
        [Column("external_account_id")]
        public string ExternalAccountId { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; }

        [Column("type")]
        public AccountType Type { get; set; }

        [Column("subtype")]
        public string? Subtype { get; set; }

        [Required]
        [Column("currency")]
        public string Currency { get; set; }

        [Column("current_balance")]
        public decimal CurrentBalance { get; set; }

        [Column("available_balance")]
        public decimal? AvailableBalance { get; set; }

        [Column("last_updated")]
        public DateTime LastUpdated { get; set; }
    }
}
