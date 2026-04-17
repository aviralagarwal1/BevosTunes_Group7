using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class AlbumSong
{
    public int AlbumSongID { get; set; }
    public int AlbumID { get; set; }
    public int SongID { get; set; }

    public Album Album { get; set; } = null!;
    public Song Song { get; set; } = null!;
}
