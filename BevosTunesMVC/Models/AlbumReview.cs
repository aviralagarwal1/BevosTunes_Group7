using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class AlbumReview
{
    public int AlbumReviewID { get; set; }

    public int CustomerID { get; set; }
    public Customer Customer { get; set; } = null!;

    public int AlbumID { get; set; }
    public Album Album { get; set; } = null!;

    public int Rating { get; set; }

    [StringLength(100)]
    public string? ReviewText { get; set; }

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public int? ApprovingEmployeeID { get; set; }
    public Employee? ApprovingEmployee { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.Now;
}
