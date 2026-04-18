using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Models;

namespace BevosTunesMVC.Controllers;

public class ArtistsController : Controller
{
    private readonly AppDbContext _db;
    public ArtistsController(AppDbContext db) => _db = db;

    private int? CustomerId => HttpContext.Session.GetInt32("CustomerID");
    private int? EmployeeId => HttpContext.Session.GetInt32("EmployeeID");
    private bool IsEmployee => EmployeeId.HasValue;

    private IQueryable<OrderItem> OwnedOrderItems(int customerId, string customerEmail) =>
        _db.OrderItems.Where(oi =>
            oi.Order.Status == OrderStatus.Ordered &&
            !oi.Order.IsRefunded &&
            ((!oi.Order.IsGift && oi.Order.CustomerID == customerId) ||
             (oi.Order.IsGift && oi.Order.GiftRecipientEmail == customerEmail)));

    // Search

    public IActionResult Index(string? keyword, int[]? genreIds, string? ratingOp,
        decimal? ratingVal, string? sortBy)
    {
        var allGenres = _db.Genres.OrderBy(g => g.Name).ToList();
        ViewBag.AllGenres = allGenres;
        ViewBag.Keyword   = keyword;
        ViewBag.GenreIds  = genreIds ?? Array.Empty<int>();
        ViewBag.RatingOp  = ratingOp;
        ViewBag.RatingVal = ratingVal;
        ViewBag.SortBy    = sortBy;

        var query = _db.Artists
            .Include(a => a.ArtistGenres).ThenInclude(ag => ag.Genre)
            .Include(a => a.ArtistReviews.Where(r => r.Status == ReviewStatus.Approved))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(a => a.Name.Contains(kw));
        }

        if (genreIds != null && genreIds.Length > 0)
        {
            var ids = genreIds.ToList();
            query = query.Where(a => a.ArtistGenres.Any(ag => ids.Contains(ag.GenreID)));
        }

        int totalCount = _db.Artists.Count();
        var results    = query.ToList();

        if (!string.IsNullOrEmpty(ratingOp) && ratingVal.HasValue)
        {
            results = results.Where(a =>
            {
                var approved = a.ArtistReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                if (!approved.Any()) return false;
                double avg = approved.Average(r => r.Rating);
                return ratingOp == "lt" ? avg < (double)ratingVal.Value
                                        : avg > (double)ratingVal.Value;
            }).ToList();
        }

        results = sortBy switch
        {
            "name_desc"   => results.OrderByDescending(a => a.Name).ToList(),
            "rating" => results.OrderBy(a =>
            {
                var ap = a.ArtistReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                return ap.Any() ? ap.Average(r => r.Rating) : 0;
            }).ToList(),
            "rating_desc" => results.OrderByDescending(a =>
            {
                var ap = a.ArtistReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                return ap.Any() ? ap.Average(r => r.Rating) : 0;
            }).ToList(),
            _ => results.OrderBy(a => a.Name).ToList()
        };

        ViewBag.TotalCount   = totalCount;
        ViewBag.DisplayCount = results.Count;
        return View(results);
    }

    // Details

    public IActionResult Details(int id)
    {
        var artist = _db.Artists
            .Include(a => a.ArtistGenres).ThenInclude(ag => ag.Genre)
            .Include(a => a.Songs).ThenInclude(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Album)
            .Include(a => a.ArtistReviews).ThenInclude(r => r.Customer)
            .FirstOrDefault(a => a.ArtistID == id);

        if (artist == null) return NotFound();

        var approvedReviews = artist.ArtistReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
        double avgRating    = approvedReviews.Any() ? approvedReviews.Average(r => r.Rating) : 0;

        ViewBag.Artist          = artist;
        ViewBag.ApprovedReviews = approvedReviews;
        ViewBag.AvgRating       = avgRating;
        ViewBag.IsEmployee      = IsEmployee;

        int? custId = CustomerId;
        if (custId.HasValue)
        {
            var customer = _db.Customers.Find(custId.Value);
            // Eligible to review if purchased at least one song or album by this artist
            var artistSongIds = artist.Songs.Select(s => s.SongID).ToList();
            var artistAlbumIds = artist.AlbumArtists.Select(aa => aa.AlbumID).ToList();

            bool purchased = customer != null && OwnedOrderItems(custId.Value, customer.Email.Trim()).Any(oi =>
                (oi.SongID != null && artistSongIds.Contains(oi.SongID.Value)) ||
                (oi.AlbumID != null && artistAlbumIds.Contains(oi.AlbumID.Value)));

            ViewBag.Purchased = purchased;

            var existingReview = _db.ArtistReviews
                .FirstOrDefault(r => r.ArtistID == id && r.CustomerID == custId);
            ViewBag.ExistingReview = existingReview;
        }

        return View();
    }

    // Reviews

    [HttpGet]
    public IActionResult ConfirmDeleteReviewText(int reviewId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var review = _db.ArtistReviews
            .Include(r => r.Artist)
            .FirstOrDefault(r => r.ArtistReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        var model = new ActionConfirmationViewModel
        {
            Title = "Delete Review Text",
            Heading = "Delete Review Text",
            Message = "This removes the written review text but keeps your rating.",
            ConfirmAction = nameof(DeleteReviewText),
            ConfirmButtonText = "Delete Review Text",
            ConfirmButtonClass = "btn-danger",
            CancelUrl = Url.Action(nameof(Details), new { id = review.ArtistID }) ?? $"/Artists/Details/{review.ArtistID}",
            HiddenFields = new Dictionary<string, string>
            {
                ["reviewId"] = review.ArtistReviewID.ToString()
            },
            Details = new List<string>
            {
                $"Artist: {review.Artist?.Name ?? $"#{review.ArtistID}"}",
                $"Rating preserved: {review.Rating} / 5"
            }
        };

        return View("~/Views/Shared/ActionConfirmation.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddReview(int artistId, int rating, string? reviewText)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var customer = _db.Customers.Find(custId.Value);
        if (customer == null) return RedirectToAction("Logout", "Account");

        var artistSongIds  = _db.Songs.Where(s => s.ArtistID == artistId).Select(s => s.SongID).ToList();
        var artistAlbumIds = _db.AlbumArtists.Where(aa => aa.ArtistID == artistId).Select(aa => aa.AlbumID).ToList();

        bool purchased = OwnedOrderItems(custId.Value, customer.Email.Trim()).Any(oi =>
            (oi.SongID != null && artistSongIds.Contains(oi.SongID.Value)) ||
            (oi.AlbumID != null && artistAlbumIds.Contains(oi.AlbumID.Value)));

        if (!purchased)
        {
            TempData["ErrorMessage"] = "You must purchase at least one song or album by this artist to review them.";
            return RedirectToAction("Details", new { id = artistId });
        }
        if (_db.ArtistReviews.Any(r => r.ArtistID == artistId && r.CustomerID == custId))
        {
            TempData["ErrorMessage"] = "You have already reviewed this artist.";
            return RedirectToAction("Details", new { id = artistId });
        }
        if (rating < 1 || rating > 5)
        {
            TempData["ErrorMessage"] = "Rating must be between 1 and 5.";
            return RedirectToAction("Details", new { id = artistId });
        }

        var text = reviewText?.Trim();
        var review = new ArtistReview
        {
            ArtistID    = artistId,
            CustomerID  = custId.Value,
            Rating      = rating,
            ReviewText  = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)],
            Status      = ReviewStatus.Pending,
            DateCreated = DateTime.Now
        };
        _db.ArtistReviews.Add(review);
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review submitted for approval.";
        return RedirectToAction("Details", new { id = artistId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditReview(int reviewId, int rating, string? reviewText)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var review = _db.ArtistReviews.FirstOrDefault(r =>
            r.ArtistReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        if (rating < 1 || rating > 5)
        {
            TempData["ErrorMessage"] = "Rating must be between 1 and 5.";
            return RedirectToAction("Details", new { id = review.ArtistID });
        }

        var text          = reviewText?.Trim();
        review.Rating     = rating;
        review.ReviewText = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)];
        review.Status     = ReviewStatus.Pending;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review updated and re-submitted for approval.";
        return RedirectToAction("Details", new { id = review.ArtistID });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteReviewText(int reviewId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var review = _db.ArtistReviews.FirstOrDefault(r =>
            r.ArtistReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        int artistId      = review.ArtistID;
        review.ReviewText = null;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review text deleted. Rating is preserved.";
        return RedirectToAction("Details", new { id = artistId });
    }
}
