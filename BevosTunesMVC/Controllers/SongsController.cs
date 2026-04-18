using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Models;

namespace BevosTunesMVC.Controllers;

public class SongsController : Controller
{
    private readonly AppDbContext _db;
    public SongsController(AppDbContext db) => _db = db;

    private int? CustomerId => HttpContext.Session.GetInt32("CustomerID");
    private int? EmployeeId => HttpContext.Session.GetInt32("EmployeeID");
    private bool IsEmployee => EmployeeId.HasValue;

    private bool CustomerOwnsSong(int customerId, string customerEmail, int songId) =>
        _db.OrderItems.Any(oi =>
            oi.SongID == songId &&
            oi.Order.Status == OrderStatus.Ordered &&
            !oi.Order.IsRefunded &&
            ((!oi.Order.IsGift && oi.Order.CustomerID == customerId) ||
             (oi.Order.IsGift && oi.Order.GiftRecipientEmail == customerEmail)));

    // Search

    public IActionResult Index(string? title, string? artist, string? album,
        int[]? genreIds, string? ratingOp, decimal? ratingVal, string? sortBy)
    {
        var allGenres = _db.Genres.OrderBy(g => g.Name).ToList();
        ViewBag.AllGenres = allGenres;
        // Not ViewBag.Title / ViewBag.Album - those keys conflict with page ViewData in Razor.
        ViewBag.SearchTitle  = title;
        ViewBag.SearchArtist = artist;
        ViewBag.SearchAlbum  = album;
        ViewBag.GenreIds  = genreIds ?? Array.Empty<int>();
        ViewBag.RatingOp  = ratingOp;
        ViewBag.RatingVal = ratingVal;
        ViewBag.SortBy    = sortBy;

        var query = _db.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .Include(s => s.SongReviews.Where(r => r.Status == ReviewStatus.Approved))
            .Include(s => s.Discounts.Where(d => d.IsActive))
            .Where(s => s.IsActive)
            .AsQueryable();

        // Separate keyword fields - AND across any that are filled (per spec).
        if (!string.IsNullOrWhiteSpace(title))
        {
            var t = title.Trim();
            query = query.Where(s => s.Title.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(artist))
        {
            var a = artist.Trim();
            query = query.Where(s => s.Artist.Name.Contains(a));
        }
        if (!string.IsNullOrWhiteSpace(album))
        {
            var al = album.Trim();
            query = query.Where(s => s.AlbumSongs.Any(als => als.Album.Title.Contains(al)));
        }

        // Genre (OR across selected genres)
        if (genreIds != null && genreIds.Length > 0)
        {
            var ids = genreIds.ToList();
            query = query.Where(s => s.SongGenres.Any(sg => ids.Contains(sg.GenreID)));
        }

        int totalCount = _db.Songs.Count(s => s.IsActive);
        var results    = query.ToList();

        // Rating filter (in-memory after materializing, avg computed per song)
        if (!string.IsNullOrEmpty(ratingOp) && ratingVal.HasValue)
        {
            results = results.Where(s =>
            {
                var approved = s.SongReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                if (!approved.Any()) return false;
                double avg = approved.Average(r => r.Rating);
                return ratingOp == "lt" ? avg < (double)ratingVal.Value
                                        : avg > (double)ratingVal.Value;
            }).ToList();
        }

        // Sort
        results = sortBy switch
        {
            "title_desc"  => results.OrderByDescending(s => s.Title).ToList(),
            "artist"      => results.OrderBy(s => s.Artist?.Name).ToList(),
            "artist_desc" => results.OrderByDescending(s => s.Artist?.Name).ToList(),
            "rating" => results.OrderBy(s =>
            {
                var a = s.SongReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                return a.Any() ? a.Average(r => r.Rating) : 0;
            }).ToList(),
            "rating_desc" => results.OrderByDescending(s =>
            {
                var a = s.SongReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                return a.Any() ? a.Average(r => r.Rating) : 0;
            }).ToList(),
            _ => results.OrderBy(s => s.Title).ToList()
        };

        ViewBag.TotalCount   = totalCount;
        ViewBag.DisplayCount = results.Count;
        return View(results);
    }

    // Details

    public IActionResult Details(int id)
    {
        var song = _db.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres).ThenInclude(sg => sg.Genre)
            .Include(s => s.AlbumSongs).ThenInclude(als => als.Album)
            .Include(s => s.SongReviews).ThenInclude(r => r.Customer)
            .Include(s => s.Discounts.Where(d => d.IsActive))
            .FirstOrDefault(s => s.SongID == id && s.IsActive);

        if (song == null) return NotFound();

        var approvedReviews = song.SongReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
        double avgRating    = approvedReviews.Any() ? approvedReviews.Average(r => r.Rating) : 0;

        ViewBag.Song            = song;
        ViewBag.ApprovedReviews = approvedReviews;
        ViewBag.AvgRating       = avgRating;
        ViewBag.IsEmployee      = IsEmployee;

        int? custId = CustomerId;
        if (custId.HasValue)
        {
            var customer = _db.Customers.Find(custId.Value);
            bool inCart = _db.CartItems.Any(ci => ci.CustomerID == custId && ci.SongID == id);
            ViewBag.InCart = inCart;

            bool purchased = customer != null &&
                CustomerOwnsSong(custId.Value, customer.Email.Trim(), id);
            ViewBag.Purchased = purchased;

            var existingReview = _db.SongReviews
                .FirstOrDefault(r => r.SongID == id && r.CustomerID == custId);
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

        var review = _db.SongReviews
            .Include(r => r.Song)
            .FirstOrDefault(r => r.SongReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        var model = new ActionConfirmationViewModel
        {
            Title = "Delete Review Text",
            Heading = "Delete Review Text",
            Message = "This removes the written review text but keeps your rating.",
            ConfirmAction = nameof(DeleteReviewText),
            ConfirmButtonText = "Delete Review Text",
            ConfirmButtonClass = "btn-danger",
            CancelUrl = Url.Action(nameof(Details), new { id = review.SongID }) ?? $"/Songs/Details/{review.SongID}",
            HiddenFields = new Dictionary<string, string>
            {
                ["reviewId"] = review.SongReviewID.ToString()
            },
            Details = new List<string>
            {
                $"Song: {review.Song?.Title ?? $"#{review.SongID}"}",
                $"Rating preserved: {review.Rating} / 5"
            }
        };

        return View("~/Views/Shared/ActionConfirmation.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddReview(int songId, int rating, string? reviewText)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var customer = _db.Customers.Find(custId.Value);
        if (customer == null) return RedirectToAction("Logout", "Account");

        bool purchased = CustomerOwnsSong(custId.Value, customer.Email.Trim(), songId);

        if (!purchased)
        {
            TempData["ErrorMessage"] = "You must purchase this song before reviewing it.";
            return RedirectToAction("Details", new { id = songId });
        }
        if (_db.SongReviews.Any(r => r.SongID == songId && r.CustomerID == custId))
        {
            TempData["ErrorMessage"] = "You have already reviewed this song.";
            return RedirectToAction("Details", new { id = songId });
        }
        if (rating < 1 || rating > 5)
        {
            TempData["ErrorMessage"] = "Rating must be between 1 and 5.";
            return RedirectToAction("Details", new { id = songId });
        }

        var text = reviewText?.Trim();
        var review = new SongReview
        {
            SongID      = songId,
            CustomerID  = custId.Value,
            Rating      = rating,
            ReviewText  = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)],
            Status      = ReviewStatus.Pending,
            DateCreated = DateTime.Now
        };
        _db.SongReviews.Add(review);
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review submitted. It will appear after employee approval.";
        return RedirectToAction("Details", new { id = songId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditReview(int reviewId, int rating, string? reviewText)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var review = _db.SongReviews.FirstOrDefault(r =>
            r.SongReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        if (rating < 1 || rating > 5)
        {
            TempData["ErrorMessage"] = "Rating must be between 1 and 5.";
            return RedirectToAction("Details", new { id = review.SongID });
        }

        var text          = reviewText?.Trim();
        review.Rating     = rating;
        review.ReviewText = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)];
        review.Status     = ReviewStatus.Pending;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review updated and re-submitted for approval.";
        return RedirectToAction("Details", new { id = review.SongID });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteReviewText(int reviewId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var review = _db.SongReviews.FirstOrDefault(r =>
            r.SongReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        int songId        = review.SongID;
        review.ReviewText = null;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review text deleted. Rating is preserved.";
        return RedirectToAction("Details", new { id = songId });
    }
}
