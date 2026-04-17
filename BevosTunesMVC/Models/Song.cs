using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class Song
{
    [Key]
    public int SongID { get; set; }

    [Required]
    [StringLength(300)]
    public string Title { get; set; } = "";

    [Column(TypeName = "decimal(6,2)")]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public int ArtistID { get; set; }
    public Artist Artist { get; set; } = null!;

    public ICollection<SongGenre> SongGenres { get; set; } = new List<SongGenre>();
    public ICollection<AlbumSong> AlbumSongs { get; set; } = new List<AlbumSong>();
    public ICollection<SongReview> SongReviews { get; set; } = new List<SongReview>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<Discount> Discounts { get; set; } = new List<Discount>();
    public ICollection<FeaturedItem> FeaturedItems { get; set; } = new List<FeaturedItem>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
