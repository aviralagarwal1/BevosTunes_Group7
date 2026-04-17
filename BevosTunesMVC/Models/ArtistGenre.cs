using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class ArtistGenre
{
    public int ArtistGenreID { get; set; }
    public int ArtistID { get; set; }
    public int GenreID { get; set; }

    public Artist Artist { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
}
