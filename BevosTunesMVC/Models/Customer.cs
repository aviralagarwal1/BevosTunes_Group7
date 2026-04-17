using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class Customer
{
    [Key]
    public int CustomerID { get; set; }

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";

    [Required]
    [StringLength(15)]
    public string PhoneNumber { get; set; } = "";

    [Required]
    [StringLength(200)]
    public string Street { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string City { get; set; } = "";

    [Required]
    [StringLength(2)]
    public string State { get; set; } = "";

    [Required]
    [StringLength(10)]
    public string ZipCode { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public ICollection<CreditCard> CreditCards { get; set; } = new List<CreditCard>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<SongReview> SongReviews { get; set; } = new List<SongReview>();
    public ICollection<AlbumReview> AlbumReviews { get; set; } = new List<AlbumReview>();
    public ICollection<ArtistReview> ArtistReviews { get; set; } = new List<ArtistReview>();
}
