using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class Discount
{
    public int DiscountID { get; set; }

    public int? SongID { get; set; }
    public Song? Song { get; set; }

    public int? AlbumID { get; set; }
    public Album? Album { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal DiscountAmount { get; set; }

    public bool IsActive { get; set; } = true;
}
