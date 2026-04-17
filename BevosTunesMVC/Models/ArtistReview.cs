using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public class ArtistReview
{
    public int ArtistReviewID { get; set; }

    public int CustomerID { get; set; }
    public Customer Customer { get; set; } = null!;

    public int ArtistID { get; set; }
    public Artist Artist { get; set; } = null!;

    public int Rating { get; set; }

    [StringLength(100)]
    public string? ReviewText { get; set; }

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public int? ApprovingEmployeeID { get; set; }
    public Employee? ApprovingEmployee { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.Now;
}
