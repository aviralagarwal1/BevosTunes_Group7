using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Models;
using BevosTunesMVC.Services;

namespace BevosTunesMVC.Controllers;

public class CheckoutController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailSender _email;

    public CheckoutController(AppDbContext db, EmailSender email)
    {
        _db = db;
        _email = email;
    }

    private int? CustomerId => HttpContext.Session.GetInt32("CustomerID");

    // Step 1: Review cart + select card + optional gift

    public IActionResult Index()
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account", new { returnUrl = "/Checkout" });

        var customer = _db.Customers.Find(custId.Value);
        if (customer == null) return RedirectToAction("Logout", "Account");
        if (!customer.IsActive)
        {
            TempData["ErrorMessage"] = "Your account is disabled and cannot complete checkout.";
            return RedirectToAction("Index", "Cart");
        }

        var cartItems = GetCartItems(custId.Value);
        if (!cartItems.Any())
        {
            TempData["ErrorMessage"] = "Your cart is empty. Please add items before checking out.";
            return RedirectToAction("Index", "Cart");
        }

        // Check for duplicate songs (album + individual song)
        var dupes = FindDuplicateSongs(custId.Value);
        if (dupes.Any())
        {
            ViewBag.DuplicateSongs = dupes;
            ViewBag.CartItems      = cartItems;
            return View("DuplicateError");
        }

        var cards = _db.CreditCards
            .Where(c => c.CustomerID == custId && c.IsActive)
            .ToList();

        ViewBag.CartItems = cartItems;
        ViewBag.Cards     = cards;
        ViewBag.SelectedExistingCardId = ReadTempDataInt("CheckoutExistingCardId");
        ViewBag.NewCardNumber          = TempData["CheckoutNewCardNumber"] as string;
        ViewBag.IsGift                 = ReadTempDataBool("CheckoutIsGift");
        ViewBag.GiftEmail              = TempData["CheckoutGiftEmail"] as string;
        SetTotals(cartItems);
        return View();
    }

    // Step 2: Confirm screen

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Confirm(int? existingCardId, string? newCardNumber,
        bool isGift, string? giftEmail)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var customer = _db.Customers.Find(custId.Value);
        if (customer == null) return RedirectToAction("Logout", "Account");

        var cartItems = GetCartItems(custId.Value);
        if (!cartItems.Any())
        {
            TempData["ErrorMessage"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        // Resolve credit card
        CreditCard? card = null;
        string digitsOnly = new((newCardNumber ?? "").Where(char.IsDigit).ToArray());

        if (!string.IsNullOrWhiteSpace(digitsOnly))
        {
            var (cardType, error) = AccountController.DetectCardType(digitsOnly);
            if (error != null)
                return RedirectToCheckoutWithState(existingCardId, newCardNumber, isGift, giftEmail, error);

            // Create a temporary card object (not saved yet - saved on Process)
            card = new CreditCard
            {
                CreditCardID = -1,  // sentinel: new card
                CustomerID   = custId.Value,
                CardNumber   = digitsOnly,
                CardType     = cardType!,
                IsActive     = true
            };
        }
        else if (existingCardId.HasValue && existingCardId > 0)
        {
            card = _db.CreditCards.FirstOrDefault(c =>
                c.CreditCardID == existingCardId && c.CustomerID == custId && c.IsActive);
            if (card == null)
            {
                return RedirectToCheckoutWithState(existingCardId, newCardNumber, isGift, giftEmail,
                    "Selected credit card not found.");
            }
        }
        else
        {
            return RedirectToCheckoutWithState(existingCardId, newCardNumber, isGift, giftEmail,
                "Please select a saved credit card or enter a different card.");
        }

        // Gift validation
        Customer? giftRecipient = null;
        if (isGift)
        {
            if (string.IsNullOrWhiteSpace(giftEmail))
                return RedirectToCheckoutWithState(existingCardId, newCardNumber, isGift, giftEmail,
                    "Please enter the gift recipient's email.");

            giftRecipient = _db.Customers.FirstOrDefault(c => c.Email == giftEmail.Trim());
            if (giftRecipient == null)
                return RedirectToCheckoutWithState(existingCardId, newCardNumber, isGift, giftEmail,
                    $"No customer found with email \"{giftEmail.Trim()}\".");
        }

        var ownedSongIds = new HashSet<int>();
        var ownedAlbumIds = new HashSet<int>();
        if (!isGift)
        {
            (ownedSongIds, ownedAlbumIds) = GetOwnedMusicIds(customer);
            SetTotals(GetPurchasableCartItems(cartItems, ownedSongIds, ownedAlbumIds));
        }
        else
        {
            SetTotals(cartItems);
        }

        ViewBag.CartItems     = cartItems;
        ViewBag.Card          = card;
        ViewBag.IsGift        = isGift;
        ViewBag.GiftRecipient = giftRecipient;
        ViewBag.OwnedSongIds  = ownedSongIds;
        ViewBag.OwnedAlbumIds = ownedAlbumIds;
        ViewBag.HasOwnedItems = !isGift && cartItems.Any(ci => IsAlreadyOwned(ci, ownedSongIds, ownedAlbumIds));
        ViewBag.AllItemsOwned = !isGift && !GetPurchasableCartItems(cartItems, ownedSongIds, ownedAlbumIds).Any();

        // Store choices in TempData to survive the POST->redirect
        TempData["CardId"]      = card.CreditCardID;
        TempData["CardNumber"]  = card.CardNumber;
        TempData["CardType"]    = card.CardType;
        TempData["IsGift"]      = isGift;
        TempData["GiftEmail"]   = giftEmail?.Trim();

        return View();
    }

    // Step 3: Process order

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Process()
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var customer = _db.Customers.Find(custId.Value);
        if (customer == null) return RedirectToAction("Logout", "Account");

        var cartItems = GetCartItems(custId.Value);
        if (!cartItems.Any())
        {
            TempData["ErrorMessage"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        // Recover card
        int    cardId     = (int)(TempData["CardId"]     ?? 0);
        string cardNumber = (string)(TempData["CardNumber"] ?? "");
        string cardType   = (string)(TempData["CardType"]   ?? "");
        bool   isGift     = (bool)(TempData["IsGift"]     ?? false);
        string giftEmail  = (string)(TempData["GiftEmail"]  ?? "");

        var purchasableCartItems = cartItems;
        int skippedOwnedCount = 0;
        if (!isGift)
        {
            var (ownedSongIds, ownedAlbumIds) = GetOwnedMusicIds(customer);
            purchasableCartItems = GetPurchasableCartItems(cartItems, ownedSongIds, ownedAlbumIds);
            skippedOwnedCount = cartItems.Count - purchasableCartItems.Count;

            if (!purchasableCartItems.Any())
            {
                TempData["ErrorMessage"] = "Everything in your cart is already in My Music. Nothing new was charged.";
                return RedirectToAction("MyMusic", "Account");
            }
        }

        CreditCard? card;
        if (cardId == -1)
        {
            // Checkout-only cards are saved inactive so order history can show
            // the card used without adding a third stored card to the account.
            int nextCardId = _db.CreditCards.Any() ? _db.CreditCards.Max(c => c.CreditCardID) + 1 : 2000;
            card = new CreditCard
            {
                CreditCardID = nextCardId,
                CustomerID   = custId.Value,
                CardNumber   = cardNumber,
                CardType     = cardType,
                IsActive     = false
            };
            _db.CreditCards.Add(card);
            _db.SaveChanges();
        }
        else
        {
            card = _db.CreditCards.Find(cardId);
            if (card == null)
            {
                TempData["ErrorMessage"] = "Credit card not found. Please start checkout again.";
                return RedirectToAction("Index");
            }
        }

        // Build order
        int nextOrderId = _db.Orders.Any() ? _db.Orders.Max(o => o.OrderID) + 1 : 212000;

        var order = new Order
        {
            OrderID      = nextOrderId,
            CustomerID   = custId.Value,
            CreditCardID = card.CreditCardID,
            OrderDate    = DateTime.Now,
            Status       = OrderStatus.Ordered,
            IsGift       = isGift,
            GiftRecipientEmail = isGift ? giftEmail : null,
            IsRefunded   = false
        };
        _db.Orders.Add(order);
        _db.SaveChanges();

        // Add order items
        foreach (var ci in purchasableCartItems)
        {
            decimal price;
            if (ci.Song != null)
            {
                var d = ci.Song.Discounts.FirstOrDefault();
                price = d != null ? ci.Song.Price - d.DiscountAmount : ci.Song.Price;
                _db.OrderItems.Add(new OrderItem
                {
                    OrderID = order.OrderID,
                    SongID  = ci.SongID,
                    Price   = price
                });
            }
            else if (ci.Album != null)
            {
                var d = ci.Album.Discounts.FirstOrDefault();
                price = d != null ? ci.Album.Price - d.DiscountAmount : ci.Album.Price;
                _db.OrderItems.Add(new OrderItem
                {
                    OrderID = order.OrderID,
                    AlbumID = ci.AlbumID,
                    Price   = price
                });
            }
        }
        _db.SaveChanges();

        // Clear cart
        var toRemove = _db.CartItems.Where(ci => ci.CustomerID == custId).ToList();
        _db.CartItems.RemoveRange(toRemove);
        _db.SaveChanges();

        // Send emails
        SendOrderEmails(order, customer, card, purchasableCartItems);

        TempData["SuccessMessage"] = skippedOwnedCount > 0
            ? $"Order #{order.OrderID} confirmed! Already-owned items were not charged and are available in My Music."
            : $"Order #{order.OrderID} confirmed! Thank you for your purchase.";
        return RedirectToAction("Confirmation", new { orderId = order.OrderID });
    }

    // Confirmation page

    public IActionResult Confirmation(int orderId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var order = _db.Orders
            .Include(o => o.CreditCard)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Song)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Album)
            .FirstOrDefault(o => o.OrderID == orderId && o.CustomerID == custId);

        if (order == null) return NotFound();

        SetTotals(order.OrderItems.ToList());
        return View(order);
    }

    // Refund

    [HttpGet]
    public IActionResult Refund(int orderId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var order = _db.Orders
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Song)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Album)
            .FirstOrDefault(o => o.OrderID == orderId && o.CustomerID == custId);

        if (order == null) return NotFound();
        if (order.IsRefunded)
        {
            TempData["ErrorMessage"] = "This order has already been refunded.";
            return RedirectToAction("OrderHistory", "Account");
        }

        return View(order);
    }

    [HttpPost, ActionName("Refund")]
    [ValidateAntiForgeryToken]
    public IActionResult RefundConfirmed(int orderId)
    {
        int? custId = CustomerId;
        if (!custId.HasValue) return RedirectToAction("Login", "Account");

        var order = _db.Orders
            .Include(o => o.Customer)
            .FirstOrDefault(o => o.OrderID == orderId && o.CustomerID == custId);

        if (order == null) return NotFound();

        order.IsRefunded = true;
        order.Status     = OrderStatus.Refunded;
        _db.SaveChanges();

        // Send refund email(s)
        var customer = order.Customer;
        _email.TrySend(customer.Email,
            $"{_email.SubjectPrefix} Order #{order.OrderID} - Refund Confirmed",
            $"Hi {customer.FirstName},\n\nYour order #{order.OrderID} has been successfully refunded. " +
            $"The music has been removed from your account.\n\n" +
            $"If you have questions, please contact BevosTunes support.\n\n- The BevosTunes Team");

        if (order.IsGift && !string.IsNullOrEmpty(order.GiftRecipientEmail))
        {
            var recipient = _db.Customers.FirstOrDefault(c => c.Email == order.GiftRecipientEmail);
            if (recipient != null)
            {
                _email.TrySend(recipient.Email,
                    $"{_email.SubjectPrefix} Order #{order.OrderID} - Gift Refund Confirmed",
                    $"Hi {recipient.FirstName},\n\nA gift you received (Order #{order.OrderID}) has been refunded by the sender. " +
                    $"The music has been removed from your account.\n\n- The BevosTunes Team");
            }
        }

        TempData["SuccessMessage"] = $"Order #{order.OrderID} has been refunded.";
        return RedirectToAction("OrderHistory", "Account");
    }

    // Helpers

    private List<CartItem> GetCartItems(int custId) =>
        _db.CartItems
            .Include(ci => ci.Song).ThenInclude(s => s!.Artist)
            .Include(ci => ci.Song).ThenInclude(s => s!.SongReviews.Where(r => r.Status == ReviewStatus.Approved))
            .Include(ci => ci.Song).ThenInclude(s => s!.Discounts.Where(d => d.IsActive))
            .Include(ci => ci.Album).ThenInclude(a => a!.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Include(ci => ci.Album).ThenInclude(a => a!.AlbumReviews.Where(r => r.Status == ReviewStatus.Approved))
            .Include(ci => ci.Album).ThenInclude(a => a!.Discounts.Where(d => d.IsActive))
            .Include(ci => ci.Album).ThenInclude(a => a!.AlbumSongs)
            .Where(ci => ci.CustomerID == custId)
            .ToList();

    private List<string> FindDuplicateSongs(int custId)
    {
        var cartItems = GetCartItems(custId);
        var dupeNames = new List<string>();

        // Collect all individual song IDs in cart
        var cartSongIds = cartItems
            .Where(ci => ci.SongID.HasValue)
            .Select(ci => ci.SongID!.Value)
            .ToHashSet();

        // For each album in cart, check if any of its songs are also in cart individually
        foreach (var ci in cartItems.Where(ci => ci.AlbumID.HasValue && ci.Album != null))
        {
            foreach (var als in ci.Album!.AlbumSongs)
            {
                if (cartSongIds.Contains(als.SongID))
                {
                    var songName = _db.Songs.Find(als.SongID)?.Title ?? $"Song #{als.SongID}";
                    dupeNames.Add(songName);
                }
            }
        }
        return dupeNames;
    }

    private (HashSet<int> OwnedSongIds, HashSet<int> OwnedAlbumIds) GetOwnedMusicIds(Customer customer)
    {
        string customerEmail = customer.Email.Trim();

        var ownedItems = _db.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.Status == OrderStatus.Ordered
                      && !oi.Order.IsRefunded
                      && ((!oi.Order.IsGift && oi.Order.CustomerID == customer.CustomerID)
                       || (oi.Order.IsGift && oi.Order.GiftRecipientEmail == customerEmail)))
            .ToList();

        var ownedSongIds = ownedItems
            .Where(oi => oi.SongID.HasValue)
            .Select(oi => oi.SongID!.Value)
            .ToHashSet();

        var ownedAlbumIds = ownedItems
            .Where(oi => oi.AlbumID.HasValue)
            .Select(oi => oi.AlbumID!.Value)
            .ToHashSet();

        return (ownedSongIds, ownedAlbumIds);
    }

    private List<CartItem> GetPurchasableCartItems(List<CartItem> cartItems, HashSet<int> ownedSongIds, HashSet<int> ownedAlbumIds) =>
        cartItems.Where(ci => !IsAlreadyOwned(ci, ownedSongIds, ownedAlbumIds)).ToList();

    private static bool IsAlreadyOwned(CartItem ci, HashSet<int> ownedSongIds, HashSet<int> ownedAlbumIds) =>
        (ci.SongID.HasValue && ownedSongIds.Contains(ci.SongID.Value))
        || (ci.AlbumID.HasValue && ownedAlbumIds.Contains(ci.AlbumID.Value));

    private void SetTotals(List<CartItem> items)
    {
        decimal subtotal = 0;
        foreach (var ci in items)
        {
            if (ci.Song != null)
            {
                var d = ci.Song.Discounts.FirstOrDefault();
                subtotal += d != null ? ci.Song.Price - d.DiscountAmount : ci.Song.Price;
            }
            else if (ci.Album != null)
            {
                var d = ci.Album.Discounts.FirstOrDefault();
                subtotal += d != null ? ci.Album.Price - d.DiscountAmount : ci.Album.Price;
            }
        }
        decimal tax   = subtotal * 0.0825m;
        ViewBag.Subtotal = subtotal;
        ViewBag.Tax      = tax;
        ViewBag.Total    = subtotal + tax;
    }

    private void SetTotals(List<OrderItem> items)
    {
        decimal subtotal = items.Sum(i => i.Price);
        decimal tax      = subtotal * 0.0825m;
        ViewBag.Subtotal = subtotal;
        ViewBag.Tax      = tax;
        ViewBag.Total    = subtotal + tax;
    }

    private void SendOrderEmails(Order order, Customer buyer, CreditCard card, List<CartItem> cartItems)
    {
        var itemLines = cartItems.Select(ci =>
        {
            if (ci.Song != null)
            {
                var d = ci.Song.Discounts.FirstOrDefault();
                decimal price = d != null ? ci.Song.Price - d.DiscountAmount : ci.Song.Price;
                string disc   = d != null ? $" (was ${ci.Song.Price:0.00}, saving ${d.DiscountAmount:0.00})" : "";
                return $"  - {ci.Song.Title} - ${price:0.00}{disc}";
            }
            if (ci.Album != null)
            {
                var d = ci.Album.Discounts.FirstOrDefault();
                decimal price = d != null ? ci.Album.Price - d.DiscountAmount : ci.Album.Price;
                string disc   = d != null ? $" (was ${ci.Album.Price:0.00}, saving ${d.DiscountAmount:0.00})" : "";
                string artists = string.Join(", ", ci.Album.AlbumArtists.Select(aa => aa.Artist?.Name ?? ""));
                return $"  - {ci.Album.Title} (Album{(artists.Length > 0 ? " by " + artists : "")}) - ${price:0.00}{disc}";
            }
            return "";
        }).Where(s => s.Length > 0);

        string itemList = string.Join("\n", itemLines);

        decimal subtotal = cartItems.Sum(ci =>
        {
            if (ci.Song  != null) { var d = ci.Song.Discounts.FirstOrDefault();  return d != null ? ci.Song.Price  - d.DiscountAmount : ci.Song.Price;  }
            if (ci.Album != null) { var d = ci.Album.Discounts.FirstOrDefault(); return d != null ? ci.Album.Price - d.DiscountAmount : ci.Album.Price; }
            return 0;
        });
        decimal tax   = subtotal * 0.0825m;
        decimal total = subtotal + tax;

        string refundUrl = Url.Action("Refund", "Checkout", new { orderId = order.OrderID }, Request.Scheme)
            ?? $"/Checkout/Refund?orderId={order.OrderID}";
        string loginUrl = Url.Action("Login", "Account", values: null, protocol: Request.Scheme)
            ?? "/Account/Login";

        if (!order.IsGift)
        {
            // Single email to buyer
            _email.TrySend(buyer.Email,
                $"{_email.SubjectPrefix} Order #{order.OrderID} - Confirmation",
                $"Hi {buyer.FirstName},\n\n" +
                $"Thank you for your purchase on BevosTunes! Your order has been confirmed.\n\n" +
                $"Order #{order.OrderID}  |  {order.OrderDate:MMMM d, yyyy}\n" +
                $"Payment: {card.DisplayCardType} ending in {card.CardNumber[^4..]}\n\n" +
                $"Items Purchased:\n{itemList}\n\n" +
                $"Subtotal: ${subtotal:0.00}\n" +
                $"Tax (8.25%): ${tax:0.00}\n" +
                $"Total: ${total:0.00}\n\n" +
                $"If this purchase was made in error, you may request a refund by visiting:\n{refundUrl}\n\n" +
                $"Thanks for listening!\n- The BevosTunes Team");
        }
        else
        {
            // Email to buyer confirming gift was sent
            _email.TrySend(buyer.Email,
                $"{_email.SubjectPrefix} Order #{order.OrderID} - Gift Order Sent",
                $"Hi {buyer.FirstName},\n\n" +
                $"Your gift order has been sent to {order.GiftRecipientEmail}!\n\n" +
                $"Order #{order.OrderID}  |  {order.OrderDate:MMMM d, yyyy}\n" +
                $"Payment: {card.DisplayCardType} ending in {card.CardNumber[^4..]}\n\n" +
                $"Items Gifted:\n{itemList}\n\n" +
                $"Subtotal: ${subtotal:0.00}\n" +
                $"Tax (8.25%): ${tax:0.00}\n" +
                $"Total: ${total:0.00}\n\n" +
                $"To request a refund, visit:\n{refundUrl}\n\n" +
                $"- The BevosTunes Team");

            // Email to gift recipient with artist recommendation
            var recipient = _db.Customers.FirstOrDefault(c => c.Email == order.GiftRecipientEmail);
            if (recipient != null)
            {
                string recommendation = GetArtistRecommendation(cartItems);
                _email.TrySend(recipient.Email,
                    $"{_email.SubjectPrefix} {buyer.FirstName} {buyer.LastName} Sent You Music!",
                    $"Hi {recipient.FirstName},\n\n" +
                    $"{buyer.FirstName} {buyer.LastName} has gifted you music on BevosTunes!\n\n" +
                    $"Your New Music:\n{itemList}\n\n" +
                    (string.IsNullOrEmpty(recommendation)
                        ? ""
                        : $"You might also enjoy:\n  {recommendation}\n\n") +
                    $"Log in to BevosTunes to enjoy your music:\n{loginUrl}\n\n- The BevosTunes Team");
            }
        }
    }

    private string GetArtistRecommendation(List<CartItem> cartItems)
    {
        // Pick a genre from any song/album in the purchase
        int? genreId = null;
        foreach (var ci in cartItems)
        {
            if (ci.SongID.HasValue)
            {
                genreId = _db.SongGenres.Where(sg => sg.SongID == ci.SongID).Select(sg => sg.GenreID).FirstOrDefault();
                if (genreId > 0) break;
            }
            else if (ci.AlbumID.HasValue)
            {
                genreId = _db.AlbumGenres.Where(ag => ag.AlbumID == ci.AlbumID).Select(ag => ag.GenreID).FirstOrDefault();
                if (genreId > 0) break;
            }
        }
        if (!genreId.HasValue || genreId == 0) return "";

        // Recommend a band, preferring direct artist ratings and then
        // derived song ratings for bands that produce music in this genre.
        var artistIdsInGenre = _db.ArtistGenres
            .Where(ag => ag.GenreID == genreId)
            .Select(ag => ag.ArtistID)
            .ToList();

        var genreName = _db.Genres.Find(genreId)?.Name ?? "this genre";

        var topArtistByArtistReviews = _db.Artists
            .Include(a => a.ArtistReviews.Where(r => r.Status == ReviewStatus.Approved))
            .Where(a => artistIdsInGenre.Contains(a.ArtistID) && a.ArtistReviews.Any(r => r.Status == ReviewStatus.Approved))
            .AsEnumerable()
            .OrderByDescending(a => a.ArtistReviews.Where(r => r.Status == ReviewStatus.Approved).Average(r => r.Rating))
            .FirstOrDefault();

        if (topArtistByArtistReviews != null)
            return $"Check out {topArtistByArtistReviews.Name} - one of the top-rated artists in {genreName}!";

        var topArtistBySongReviews = _db.Songs
            .Include(s => s.Artist)
            .Include(s => s.SongGenres)
            .Include(s => s.SongReviews.Where(r => r.Status == ReviewStatus.Approved))
            .Where(s => artistIdsInGenre.Contains(s.ArtistID)
                     && s.SongGenres.Any(sg => sg.GenreID == genreId)
                     && s.SongReviews.Any(r => r.Status == ReviewStatus.Approved))
            .AsEnumerable()
            .GroupBy(s => s.Artist)
            .Where(g => g.Key != null)
            .Select(g => new
            {
                Artist = g.Key!,
                AverageRating = g.SelectMany(s => s.SongReviews.Where(r => r.Status == ReviewStatus.Approved)).Average(r => r.Rating)
            })
            .OrderByDescending(x => x.AverageRating)
            .FirstOrDefault();

        if (topArtistBySongReviews != null)
            return $"Check out {topArtistBySongReviews.Artist.Name} - their songs are highly rated in {genreName}!";

        var fallbackArtist = _db.Artists
            .Where(a => artistIdsInGenre.Contains(a.ArtistID))
            .OrderBy(a => a.Name)
            .FirstOrDefault();

        return fallbackArtist == null
            ? ""
            : $"You might also like {fallbackArtist.Name}, another artist with music in {genreName}.";
    }

    private IActionResult RedirectToCheckoutWithState(int? existingCardId, string? newCardNumber,
        bool isGift, string? giftEmail, string errorMessage)
    {
        TempData["ErrorMessage"] = errorMessage;
        if (existingCardId.HasValue)
            TempData["CheckoutExistingCardId"] = existingCardId.Value;
        TempData["CheckoutNewCardNumber"] = newCardNumber?.Trim();
        TempData["CheckoutIsGift"] = isGift;
        TempData["CheckoutGiftEmail"] = giftEmail?.Trim();
        return RedirectToAction("Index");
    }

    private int? ReadTempDataInt(string key) =>
        TempData[key] is int value ? value : null;

    private bool ReadTempDataBool(string key) =>
        TempData[key] is bool value && value;
}
