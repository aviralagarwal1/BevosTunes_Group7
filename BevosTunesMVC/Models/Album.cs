using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class Album
{
    [Key]
    public int AlbumID { get; set; }

    [Required]
    [StringLength(300)]
    public string Title { get; set; } = "";

    [Column(TypeName = "decimal(6,2)")]
    public decimal Price { get; set; }

    [StringLength(1000)]
    public string? AlbumCoverURL { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<AlbumArtist> AlbumArtists { get; set; } = new List<AlbumArtist>();
    public ICollection<AlbumGenre> AlbumGenres { get; set; } = new List<AlbumGenre>();
    public ICollection<AlbumSong> AlbumSongs { get; set; } = new List<AlbumSong>();
    public ICollection<AlbumReview> AlbumReviews { get; set; } = new List<AlbumReview>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<Discount> Discounts { get; set; } = new List<Discount>();
    public ICollection<FeaturedItem> FeaturedItems { get; set; } = new List<FeaturedItem>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
