using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class CartItem
{
    public int CartItemID { get; set; }

    public int CustomerID { get; set; }
    public Customer Customer { get; set; } = null!;

    public int? SongID { get; set; }
    public Song? Song { get; set; }

    public int? AlbumID { get; set; }
    public Album? Album { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.Now;
}
