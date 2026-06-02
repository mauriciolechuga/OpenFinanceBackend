using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAPI.OpenFinance.Models
{
    [Table("cash")]
    public class CashModel
    {
        /*
         * Table: cash
         * cash_id (PK, int, not null)
         * cash_description (varchar(100), not null)
         * last_updated (datetime, not null)
         */
        [Key]
        [Column("cash_id")]
        public int cashId { get; set; }
        
        [Column("product_id")]
        public int productId { get; set; }
        [ForeignKey("product_types")]
        public ProductTypesModel Product { get; set; }

        [Required]
        [Column("cash_description")]
        public string cashDescription { get; set; }

        // TODO: add a last_updated column with a DB-generated default (configure in OnModelCreating).
    }
}
