using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class AlbumArtist
{
    public int AlbumArtistID { get; set; }
    public int AlbumID { get; set; }
    public int ArtistID { get; set; }

    public Album Album { get; set; } = null!;
    public Artist Artist { get; set; } = null!;
}
