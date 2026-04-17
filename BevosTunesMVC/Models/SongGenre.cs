using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class SongGenre
{
    public int SongGenreID { get; set; }
    public int SongID { get; set; }
    public int GenreID { get; set; }

    public Song Song { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
}
