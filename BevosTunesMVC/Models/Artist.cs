using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class Artist
{
    [Key]
    public int ArtistID { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = "";

    public ICollection<ArtistGenre> ArtistGenres { get; set; } = new List<ArtistGenre>();
    public ICollection<Song> Songs { get; set; } = new List<Song>();
    public ICollection<AlbumArtist> AlbumArtists { get; set; } = new List<AlbumArtist>();
    public ICollection<ArtistReview> ArtistReviews { get; set; } = new List<ArtistReview>();
    public ICollection<FeaturedItem> FeaturedItems { get; set; } = new List<FeaturedItem>();
}
