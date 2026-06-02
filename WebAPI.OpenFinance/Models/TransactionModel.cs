using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.OpenFinance.Models
{
    // A normalized transaction on an Account, deduplicated on (account, external id) at sync time.
    [Table("transactions")]
    public class TransactionModel
    {
        [Key]
        [Column("transaction_id")]
        public int TransactionId { get; init; }

        [Column("account_id")]
        public int AccountId { get; set; }
        [ForeignKey("AccountId")]
        public AccountModel Account { get; set; }

        // The provider's own identifier for this transaction (used to avoid duplicates on re-sync).
        [Required]
        [Column("external_transaction_id")]
        public string ExternalTransactionId { get; set; }

        [Column("date")]
        public DateTime Date { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Required]
        [Column("currency")]
        public string Currency { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; }

        [Column("category")]
        public string? Category { get; set; }

        [Column("type")]
        public TransactionType Type { get; set; }
    }
}
