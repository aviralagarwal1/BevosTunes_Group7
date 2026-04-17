using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BevosTunesMVC.Models;

public enum ReviewStatus { Pending, Approved, Rejected }

public class SongReview
{
    public int SongReviewID { get; set; }

    public int CustomerID { get; set; }
    public Customer Customer { get; set; } = null!;

    public int SongID { get; set; }
    public Song Song { get; set; } = null!;

    public int Rating { get; set; }

    [StringLength(100)]
    public string? ReviewText { get; set; }

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public int? ApprovingEmployeeID { get; set; }
    public Employee? ApprovingEmployee { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.Now;
}
