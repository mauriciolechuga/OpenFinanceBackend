using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.OpenFinance.Models
{
    // A position: a quantity of a Security held within a (brokerage/investment) Account.
    [Table("holdings")]
    public class HoldingModel
    {
        [Key]
        [Column("holding_id")]
        public int HoldingId { get; init; }

        [Column("account_id")]
        public int AccountId { get; set; }
        [ForeignKey("AccountId")]
        public AccountModel Account { get; set; }

        [Column("security_id")]
        public int SecurityId { get; set; }
        [ForeignKey("SecurityId")]
        public SecurityModel Security { get; set; }

        [Column("quantity")]
        public decimal Quantity { get; set; }

        // Total amount originally paid for the position (for gain/loss), when the provider reports it.
        [Column("cost_basis")]
        public decimal? CostBasis { get; set; }

        [Column("last_updated")]
        public DateTime LastUpdated { get; set; }
    }
}
