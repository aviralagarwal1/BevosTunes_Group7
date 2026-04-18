using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Models;

namespace BevosTunesMVC.Controllers;

public class AlbumsController : Controller
{
    private readonly AppDbContext _db;
    public AlbumsController(AppDbContext db) => _db = db;

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

    private bool CustomerOwnsAlbum(int customerId, string customerEmail, int albumId) =>
        _db.OrderItems.Any(oi =>
            oi.AlbumID == albumId &&
            oi.Order.Status == OrderStatus.Ordered &&
            !oi.Order.IsRefunded &&
            ((!oi.Order.IsGift && oi.Order.CustomerID == customerId) ||
             (oi.Order.IsGift && oi.Order.GiftRecipientEmail == customerEmail)));

    // Search

    public IActionResult Index(string? title, string? artist, int[]? genreIds,
        string? ratingOp, decimal? ratingVal, string? sortBy)
    {
        var allGenres = _db.Genres.OrderBy(g => g.Name).ToList();
        ViewBag.AllGenres = allGenres;
        // Not ViewBag.Title - that key is overwritten by ViewData["Title"] in the Razor page.
        ViewBag.SearchTitle  = title;
        ViewBag.SearchArtist = artist;
        ViewBag.GenreIds  = genreIds ?? Array.Empty<int>();
        ViewBag.RatingOp  = ratingOp;
        ViewBag.RatingVal = ratingVal;
        ViewBag.SortBy    = sortBy;

        var query = _db.Albums
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Include(a => a.AlbumGenres).ThenInclude(ag => ag.Genre)
            .Include(a => a.AlbumReviews.Where(r => r.Status == ReviewStatus.Approved))
            .Include(a => a.Discounts.Where(d => d.IsActive))
            .Where(a => a.IsActive)
            .AsQueryable();

        // Separate keyword fields - AND across any that are filled (per spec).
        if (!string.IsNullOrWhiteSpace(title))
        {
            var t = title.Trim();
            query = query.Where(a => a.Title.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(artist))
        {
            var ar = artist.Trim();
            query = query.Where(a => a.AlbumArtists.Any(aa => aa.Artist.Name.Contains(ar)));
        }

        if (genreIds != null && genreIds.Length > 0)
        {
            var ids = genreIds.ToList();
            query = query.Where(a => a.AlbumGenres.Any(ag => ids.Contains(ag.GenreID)));
        }

        int totalCount = _db.Albums.Count(a => a.IsActive);
        var results    = query.ToList();

        if (!string.IsNullOrEmpty(ratingOp) && ratingVal.HasValue)
        {
            results = results.Where(a =>
            {
                var approved = a.AlbumReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                if (!approved.Any()) return false;
                double avg = approved.Average(r => r.Rating);
                return ratingOp == "lt" ? avg < (double)ratingVal.Value
                                        : avg > (double)ratingVal.Value;
            }).ToList();
        }

        results = sortBy switch
        {
            "title_desc"  => results.OrderByDescending(a => a.Title).ToList(),
            "artist"      => results.OrderBy(a => a.AlbumArtists.FirstOrDefault()?.Artist?.Name).ToList(),
            "artist_desc" => results.OrderByDescending(a => a.AlbumArtists.FirstOrDefault()?.Artist?.Name).ToList(),
            "rating" => results.OrderBy(a =>
            {
                var ap = a.AlbumReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                return ap.Any() ? ap.Average(r => r.Rating) : 0;
            }).ToList(),
            "rating_desc" => results.OrderByDescending(a =>
            {
                var ap = a.AlbumReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                return ap.Any() ? ap.Average(r => r.Rating) : 0;
            }).ToList(),
            _ => results.OrderBy(a => a.Title).ToList()
        };

        ViewBag.TotalCount   = totalCount;
        ViewBag.DisplayCount = results.Count;
        return View(results);
    }

    // Details

    public IActionResult Details(int id)
    {
        var album = _db.Albums
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Include(a => a.AlbumGenres).ThenInclude(ag => ag.Genre)
            .Include(a => a.AlbumSongs).ThenInclude(als => als.Song).ThenInclude(s => s.Artist)
            .Include(a => a.AlbumReviews).ThenInclude(r => r.Customer)
            .Include(a => a.Discounts.Where(d => d.IsActive))
            .FirstOrDefault(a => a.AlbumID == id && a.IsActive);

        if (album == null) return NotFound();

        var approvedReviews = album.AlbumReviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
        double avgRating    = approvedReviews.Any() ? approvedReviews.Average(r => r.Rating) : 0;

        ViewBag.Album           = album;
        ViewBag.ApprovedReviews = approvedReviews;
        ViewBag.AvgRating       = avgRating;
        ViewBag.IsEmployee      = IsEmployee;

        int? custId = CustomerId;
        if (custId.HasValue)
        {
            var customer = _db.Customers.Find(custId.Value);
            bool inCart = _db.CartItems.Any(ci => ci.CustomerID == custId && ci.AlbumID == id);
            ViewBag.InCart = inCart;

            var albumSongIds = album.AlbumSongs.Select(als => als.SongID).ToList();
            bool purchasedAlbum = false;
            bool purchasedAllSongs = false;
            if (customer != null)
            {
                string customerEmail = customer.Email.Trim();
                purchasedAlbum = CustomerOwnsAlbum(custId.Value, customerEmail, id);
                purchasedAllSongs = albumSongIds.Any() &&
                    albumSongIds.All(sid => CustomerOwnsSong(custId.Value, customerEmail, sid));
            }

            ViewBag.Purchased = purchasedAlbum || purchasedAllSongs;

            var existingReview = _db.AlbumReviews
                .FirstOrDefault(r => r.AlbumID == id && r.CustomerID == custId);
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

        var review = _db.AlbumReviews
            .Include(r => r.Album)
            .FirstOrDefault(r => r.AlbumReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        var model = new ActionConfirmationViewModel
        {
            Title = "Delete Review Text",
            Heading = "Delete Review Text",
            Message = "This removes the written review text but keeps your rating.",
            ConfirmAction = nameof(DeleteReviewText),
            ConfirmButtonText = "Delete Review Text",
            ConfirmButtonClass = "btn-danger",
            CancelUrl = Url.Action(nameof(Details), new { id = review.AlbumID }) ?? $"/Albums/Details/{review.AlbumID}",
            HiddenFields = new Dictionary<string, string>
            {
                ["reviewId"] = review.AlbumReviewID.ToString()
            },
            Details = new List<string>
            {
                $"Album: {review.Album?.Title ?? $"#{review.AlbumID}"}",
                $"Rating preserved: {review.Rating} / 5"
            }
        };

        return View("~/Views/Shared/ActionConfirmation.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddReview(int albumId, int rating, string? reviewText)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var customer = _db.Customers.Find(custId.Value);
        if (customer == null) return RedirectToAction("Logout", "Account");

        string customerEmail = customer.Email.Trim();
        bool purchasedAlbum = CustomerOwnsAlbum(custId.Value, customerEmail, albumId);

        var albumSongIds = _db.AlbumSongs.Where(als => als.AlbumID == albumId).Select(als => als.SongID).ToList();
        bool purchasedAllSongs = albumSongIds.Any() && albumSongIds.All(sid =>
            CustomerOwnsSong(custId.Value, customerEmail, sid));

        if (!purchasedAlbum && !purchasedAllSongs)
        {
            TempData["ErrorMessage"] = "You must own this album (or all its songs) to review it.";
            return RedirectToAction("Details", new { id = albumId });
        }
        if (_db.AlbumReviews.Any(r => r.AlbumID == albumId && r.CustomerID == custId))
        {
            TempData["ErrorMessage"] = "You have already reviewed this album.";
            return RedirectToAction("Details", new { id = albumId });
        }
        if (rating < 1 || rating > 5)
        {
            TempData["ErrorMessage"] = "Rating must be between 1 and 5.";
            return RedirectToAction("Details", new { id = albumId });
        }

        var text = reviewText?.Trim();
        var review = new AlbumReview
        {
            AlbumID     = albumId,
            CustomerID  = custId.Value,
            Rating      = rating,
            ReviewText  = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)],
            Status      = ReviewStatus.Pending,
            DateCreated = DateTime.Now
        };
        _db.AlbumReviews.Add(review);
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review submitted for approval.";
        return RedirectToAction("Details", new { id = albumId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditReview(int reviewId, int rating, string? reviewText)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var review = _db.AlbumReviews.FirstOrDefault(r =>
            r.AlbumReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        if (rating < 1 || rating > 5)
        {
            TempData["ErrorMessage"] = "Rating must be between 1 and 5.";
            return RedirectToAction("Details", new { id = review.AlbumID });
        }

        var text          = reviewText?.Trim();
        review.Rating     = rating;
        review.ReviewText = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)];
        review.Status     = ReviewStatus.Pending;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review updated and re-submitted for approval.";
        return RedirectToAction("Details", new { id = review.AlbumID });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteReviewText(int reviewId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var review = _db.AlbumReviews.FirstOrDefault(r =>
            r.AlbumReviewID == reviewId && r.CustomerID == custId);
        if (review == null) return NotFound();

        int albumId       = review.AlbumID;
        review.ReviewText = null;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Review text deleted. Rating is preserved.";
        return RedirectToAction("Details", new { id = albumId });
    }
}
