using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class AlbumGenre
{
    public int AlbumGenreID { get; set; }
    public int AlbumID { get; set; }
    public int GenreID { get; set; }

    public Album Album { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
}
