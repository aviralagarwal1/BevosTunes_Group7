using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class Genre
{
    [Key]
    public int GenreID { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = "";

    public ICollection<ArtistGenre> ArtistGenres { get; set; } = new List<ArtistGenre>();
    public ICollection<SongGenre> SongGenres { get; set; } = new List<SongGenre>();
    public ICollection<AlbumGenre> AlbumGenres { get; set; } = new List<AlbumGenre>();
}
