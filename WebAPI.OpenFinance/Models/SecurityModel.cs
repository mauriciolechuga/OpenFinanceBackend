using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.OpenFinance.Models
{
    // Reference data for a tradable instrument. Shared across holdings so price/metadata is stored once.
    [Table("securities")]
    public class SecurityModel
    {
        [Key]
        [Column("security_id")]
        public int SecurityId { get; init; }

        [Required]
        [Column("symbol")]
        public string Symbol { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; }

        [Column("type")]
        public SecurityType Type { get; set; }

        [Required]
        [Column("currency")]
        public string Currency { get; set; }

        // Latest known price per unit, used to value holdings.
        [Column("last_price")]
        public decimal LastPrice { get; set; }

        [Column("last_updated")]
        public DateTime LastUpdated { get; set; }
    }
}
