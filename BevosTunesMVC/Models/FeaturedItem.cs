using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class FeaturedItem
{
    public int FeaturedItemID { get; set; }

    public int? SongID { get; set; }
    public Song? Song { get; set; }

    public int? AlbumID { get; set; }
    public Album? Album { get; set; }

    public int? ArtistID { get; set; }
    public Artist? Artist { get; set; }

    public bool IsActive { get; set; } = true;
}
