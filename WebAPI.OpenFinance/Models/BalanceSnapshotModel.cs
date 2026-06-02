using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.OpenFinance.Models
{
    // A point-in-time record of a client's total net worth, written after each sync.
    // Powers the net-worth-over-time analysis/charts in the app.
    [Table("balance_snapshots")]
    public class BalanceSnapshotModel
    {
        [Key]
        [Column("snapshot_id")]
        public int SnapshotId { get; init; }

        [Column("client_id")]
        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public ClientsModel Client { get; set; }

        [Column("snapshot_date")]
        public DateTime SnapshotDate { get; set; }

        [Column("total_net_worth")]
        public decimal TotalNetWorth { get; set; }

        [Required]
        [Column("currency")]
        public string Currency { get; set; }
    }
}
