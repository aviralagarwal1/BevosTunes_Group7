using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class Employee
{
    [Key]
    public int EmployeeID { get; set; }

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
    public bool IsManager { get; set; } = false;

    public ICollection<SongReview> ApprovedSongReviews { get; set; } = new List<SongReview>();
    public ICollection<AlbumReview> ApprovedAlbumReviews { get; set; } = new List<AlbumReview>();
    public ICollection<ArtistReview> ApprovedArtistReviews { get; set; } = new List<ArtistReview>();
}
