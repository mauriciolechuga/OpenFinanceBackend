using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.OpenFinance.Models
{
    // A link between a client and an institution. Originally just (client, bank, account number);
    // now also carries the aggregation-provider linkage used to sync accounts/holdings/transactions.
    [Table("connections")]
    public class ConnectionsModel
    {
        [Key]
        [Column("connection_id")]
        public int connectionID { get; init; }

        [Column("client_id")]
        public int clientID { get; set; }
        [ForeignKey("clientID")]
        public ClientsModel Client { get; set; }

        [Column("bank_id")]
        public int bankID { get; set; }
        [ForeignKey("bankID")]
        public BanksModel Bank { get; set; }

        [Required]
        [Column("account_number")]
        public int accountNumber { get; set; }

        // --- Aggregation provider linkage (nullable: legacy rows predate this) ---

        // Which provider owns this connection, e.g. "Mock", "Flinks", "SnapTrade".
        [Column("provider")]
        public string? Provider { get; set; }

        // The provider's identifier for this linked login/item.
        [Column("provider_item_id")]
        public string? ProviderItemId { get; set; }

        // The provider access token, encrypted at rest (never stored in plaintext).
        [Column("access_token_encrypted")]
        public string? AccessTokenEncrypted { get; set; }

        [Column("status")]
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;

        [Column("last_synced_at")]
        public DateTime? LastSyncedAt { get; set; }
    }
}
