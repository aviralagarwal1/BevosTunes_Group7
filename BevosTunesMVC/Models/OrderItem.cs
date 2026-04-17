using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class OrderItem
{
    public int OrderItemID { get; set; }

    public int OrderID { get; set; }
    public Order Order { get; set; } = null!;

    public int? SongID { get; set; }
    public Song? Song { get; set; }

    public int? AlbumID { get; set; }
    public Album? Album { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal Price { get; set; }
}
