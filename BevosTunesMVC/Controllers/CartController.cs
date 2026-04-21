using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Models;

namespace BevosTunesMVC.Controllers;

public class CartController : Controller
{
    private readonly AppDbContext _db;
    public CartController(AppDbContext db) => _db = db;

    private int? CustomerId => HttpContext.Session.GetInt32("CustomerID");

    private IQueryable<CartItem> CustomerCart(int custId) =>
        _db.CartItems
            .Include(ci => ci.Song).ThenInclude(s => s!.Artist)
            .Include(ci => ci.Song).ThenInclude(s => s!.SongReviews.Where(r => r.Status == ReviewStatus.Approved))
            .Include(ci => ci.Song).ThenInclude(s => s!.Discounts.Where(d => d.IsActive))
            .Include(ci => ci.Album).ThenInclude(a => a!.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Include(ci => ci.Album).ThenInclude(a => a!.AlbumReviews.Where(r => r.Status == ReviewStatus.Approved))
            .Include(ci => ci.Album).ThenInclude(a => a!.Discounts.Where(d => d.IsActive))
            .Include(ci => ci.Album).ThenInclude(a => a!.AlbumSongs)
            .Where(ci => ci.CustomerID == custId);

    // View Cart

    public IActionResult Index()
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account", new { returnUrl = "/Cart" });

        var items = CustomerCart(custId.Value).ToList();
        ViewBag.DuplicateSongs = FindDuplicateSongs(items);
        SetCartViewBag(items);
        return View(items);
    }

    // Add Song

    [HttpGet]
    public IActionResult ConfirmRemove(int cartItemId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account", new { returnUrl = "/Cart" });

        var item = CustomerCart(custId.Value).FirstOrDefault(ci => ci.CartItemID == cartItemId);
        if (item == null) return NotFound();

        string itemName;
        string itemType;
        if (item.Song != null)
        {
            itemType = "song";
            itemName = item.Song.Title;
        }
        else if (item.Album != null)
        {
            itemType = "album";
            itemName = item.Album.Title;
        }
        else
        {
            return NotFound();
        }

        var model = new ActionConfirmationViewModel
        {
            Title = "Remove Cart Item",
            Heading = "Remove Item From Cart",
            Message = $"Remove this {itemType} from your shopping cart?",
            ConfirmAction = nameof(Remove),
            ConfirmButtonText = "Remove Item",
            ConfirmButtonClass = "btn-danger",
            CancelUrl = Url.Action(nameof(Index)) ?? "/Cart",
            HiddenFields = new Dictionary<string, string>
            {
                ["cartItemId"] = item.CartItemID.ToString()
            },
            Details = new List<string> { itemName }
        };

        return View("~/Views/Shared/ActionConfirmation.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddSong(int songId, string? returnUrl)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        // Already in cart as individual song?
        if (_db.CartItems.Any(ci => ci.CustomerID == custId && ci.SongID == songId))
        {
            TempData["ErrorMessage"] = "This song is already in your cart.";
            return Redirect(returnUrl ?? $"/Songs/Details/{songId}");
        }

        // Is this song inside an album that's already in cart?
        var albumsWithSong = _db.AlbumSongs
            .Where(als => als.SongID == songId)
            .Select(als => als.AlbumID)
            .ToList();
        if (albumsWithSong.Any() &&
            _db.CartItems.Any(ci => ci.CustomerID == custId && ci.AlbumID != null && albumsWithSong.Contains(ci.AlbumID.Value)))
        {
            TempData["ErrorMessage"] = "An album containing this song is already in your cart.";
            return Redirect(returnUrl ?? $"/Songs/Details/{songId}");
        }

        _db.CartItems.Add(new CartItem
        {
            CustomerID = custId.Value,
            SongID     = songId,
            DateAdded  = DateTime.Now
        });
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Song added to cart.";
        return Redirect(returnUrl ?? $"/Songs/Details/{songId}");
    }

    // Add Album

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddAlbum(int albumId, string? returnUrl)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        // Already in cart?
        if (_db.CartItems.Any(ci => ci.CustomerID == custId && ci.AlbumID == albumId))
        {
            TempData["ErrorMessage"] = "This album is already in your cart.";
            return Redirect(returnUrl ?? $"/Albums/Details/{albumId}");
        }

        _db.CartItems.Add(new CartItem
        {
            CustomerID = custId.Value,
            AlbumID    = albumId,
            DateAdded  = DateTime.Now
        });
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Album added to cart.";
        var duplicateSongs = FindDuplicateSongs(CustomerCart(custId.Value).ToList());
        if (duplicateSongs.Any())
        {
            TempData["ErrorMessage"] = "Your cart now has duplicate songs because an album contains a song already in your cart. Remove the duplicate before checkout.";
            return RedirectToAction("Index");
        }

        return Redirect(returnUrl ?? $"/Albums/Details/{albumId}");
    }

    // Remove Item

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int cartItemId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var item = _db.CartItems.FirstOrDefault(ci =>
            ci.CartItemID == cartItemId && ci.CustomerID == custId);
        if (item != null)
        {
            _db.CartItems.Remove(item);
            _db.SaveChanges();
            TempData["SuccessMessage"] = "Item removed from cart.";
        }
        return RedirectToAction("Index");
    }

    // Helper: compute prices with live discounts

    private void SetCartViewBag(List<CartItem> items)
    {
        decimal subtotal = 0;
        foreach (var item in items)
        {
            if (item.Song != null)
            {
                var d = item.Song.Discounts.FirstOrDefault();
                subtotal += d != null ? item.Song.Price - d.DiscountAmount : item.Song.Price;
            }
            else if (item.Album != null)
            {
                var d = item.Album.Discounts.FirstOrDefault();
                subtotal += d != null ? item.Album.Price - d.DiscountAmount : item.Album.Price;
            }
        }
        decimal tax   = subtotal * 0.0825m;
        decimal total = subtotal + tax;

        ViewBag.Subtotal = subtotal;
        ViewBag.Tax      = tax;
        ViewBag.Total    = total;
    }

    private List<string> FindDuplicateSongs(List<CartItem> cartItems)
    {
        var cartSongIds = cartItems
            .Where(ci => ci.SongID.HasValue)
            .Select(ci => ci.SongID!.Value)
            .ToHashSet();

        var duplicateSongIds = cartItems
            .Where(ci => ci.AlbumID.HasValue && ci.Album != null)
            .SelectMany(ci => ci.Album!.AlbumSongs)
            .Where(als => cartSongIds.Contains(als.SongID))
            .Select(als => als.SongID)
            .Distinct()
            .ToList();

        if (!duplicateSongIds.Any()) return new List<string>();

        return _db.Songs
            .Where(s => duplicateSongIds.Contains(s.SongID))
            .OrderBy(s => s.Title)
            .Select(s => s.Title)
            .ToList();
    }
}
