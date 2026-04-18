using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Models;
using BevosTunesMVC.Services;
using System.ComponentModel.DataAnnotations;

namespace BevosTunesMVC.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailSender _email;

    public AccountController(AppDbContext db, EmailSender email)
    {
        _db = db;
        _email = email;
    }

    // Session helpers

    private int? CustomerId   => HttpContext.Session.GetInt32("CustomerID");
    private int? EmployeeId   => HttpContext.Session.GetInt32("EmployeeID");
    private bool IsManager    => HttpContext.Session.GetString("IsManager") == "true";
    private bool IsCustomer   => CustomerId.HasValue;
    private bool IsEmployee   => EmployeeId.HasValue;

    // Login

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsCustomer || IsEmployee) return RedirectToAction("Index", "Home");
        SetLoginViewBag(null, returnUrl);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(string email, string password, string? returnUrl = null)
    {
        SetLoginViewBag(email, returnUrl);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Email and password are required.";
            return View();
        }

        // Check employees first
        var employee = _db.Employees.FirstOrDefault(e =>
            e.Email == email && e.Password == password);

        if (employee != null)
        {
            if (!employee.IsActive)
            {
                ViewBag.Error = "Your account has been deactivated. Please contact a manager.";
                return View();
            }
            HttpContext.Session.SetInt32("EmployeeID", employee.EmployeeID);
            HttpContext.Session.SetString("IsManager", employee.IsManager ? "true" : "false");
            HttpContext.Session.SetString("UserName", $"{employee.FirstName} {employee.LastName}");
            return RedirectToAction("Dashboard", "Employee");
        }

        // Check customers
        var customer = _db.Customers.FirstOrDefault(c =>
            c.Email == email && c.Password == password);

        if (customer != null)
        {
            HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
            HttpContext.Session.SetString("UserName", $"{customer.FirstName} {customer.LastName}");
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = "Invalid email or password.";
        return View();
    }

    // Logout

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    // Register

    [HttpGet]
    public IActionResult Register()
    {
        if (IsCustomer || IsEmployee) return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(string firstName, string lastName, string email,
        string password, string confirmPassword, string phoneNumber,
        string street, string zipCode)
    {
        SetRegisterViewBag(firstName, lastName, email, phoneNumber, street, zipCode);

        // Validation
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(street) ||
            string.IsNullOrWhiteSpace(zipCode))
        {
            ViewBag.Error = "All fields are required.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "Passwords do not match.";
            return View();
        }

        if (_db.Customers.Any(c => c.Email == email))
        {
            ViewBag.Error = "An account with that email already exists.";
            return View();
        }
        if (!IsValidEmailAddress(email))
        {
            ViewBag.Error = "Please enter a valid email address.";
            return View();
        }

        string? formattedPhone = FormatPhoneNumber(phoneNumber);
        if (formattedPhone == null)
        {
            ViewBag.Error = "Phone number must contain exactly 10 digits.";
            return View();
        }

        var (city, state) = LookupZip(zipCode.Trim());
        if (city == null)
        {
            ViewBag.Error = "Could not find city/state for that ZIP code.";
            return View();
        }

        var customer = new Customer
        {
            FirstName   = firstName.Trim(),
            LastName    = lastName.Trim(),
            Email       = email.Trim(),
            Password    = password,
            PhoneNumber = formattedPhone,
            Street      = street.Trim(),
            City        = city,
            State       = state!,
            ZipCode     = zipCode.Trim(),
            IsActive    = true
        };

        _db.Customers.Add(customer);
        _db.SaveChanges();

        var loginUrl = Url.Action("Login", "Account", values: null, protocol: Request.Scheme)
            ?? "/Account/Login";

        // Send confirmation email (best-effort)
        _email.TrySend(customer.Email,
            $"{_email.SubjectPrefix} Welcome to BevosTunes!",
            $"Hi {customer.FirstName},\n\nYour BevosTunes account has been created successfully.\n\nEmail: {customer.Email}\n\nLog in anytime at {loginUrl} to start browsing and purchasing music.\n\nEnjoy the music!\n\n- The BevosTunes Team");

        HttpContext.Session.SetInt32("CustomerID", customer.CustomerID);
        HttpContext.Session.SetString("UserName", $"{customer.FirstName} {customer.LastName}");

        TempData["SuccessMessage"] = "Welcome to BevosTunes! Your account has been created.";
        return RedirectToAction("Index", "Home");
    }

    // Account Details

    public IActionResult Details()
    {
        if (!IsCustomer) return RedirectToAction("Login");
        var customer = _db.Customers
            .Include(c => c.CreditCards)
            .FirstOrDefault(c => c.CustomerID == CustomerId);
        if (customer == null) return RedirectToAction("Logout");
        return View(customer);
    }

    // Edit Profile

    [HttpGet]
    public IActionResult Edit()
    {
        if (!IsCustomer) return RedirectToAction("Login");
        var customer = _db.Customers.Find(CustomerId);
        if (customer == null) return RedirectToAction("Logout");
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(string firstName, string lastName, string email,
        string phoneNumber, string street, string zipCode)
    {
        if (!IsCustomer) return RedirectToAction("Login");

        var customer = _db.Customers.Find(CustomerId);
        if (customer == null) return RedirectToAction("Logout");
        ApplyCustomerFormValues(customer, firstName, lastName, email, phoneNumber, street, zipCode);

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phoneNumber) ||
            string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(zipCode))
        {
            ViewBag.Error = "All fields are required.";
            return View(customer);
        }

        // Check email uniqueness (allow own email)
        if (_db.Customers.Any(c => c.Email == email.Trim() && c.CustomerID != customer.CustomerID))
        {
            ViewBag.Error = "That email is already in use by another account.";
            return View(customer);
        }
        if (!IsValidEmailAddress(email))
        {
            ViewBag.Error = "Please enter a valid email address.";
            return View(customer);
        }

        string? formattedPhone = FormatPhoneNumber(phoneNumber);
        if (formattedPhone == null)
        {
            ViewBag.Error = "Phone number must contain exactly 10 digits.";
            return View(customer);
        }

        var (city, state) = LookupZip(zipCode.Trim());
        if (city == null)
        {
            ViewBag.Error = "Could not find city/state for that ZIP code.";
            return View(customer);
        }

        customer.FirstName   = firstName.Trim();
        customer.LastName    = lastName.Trim();
        customer.Email       = email.Trim();
        customer.PhoneNumber = formattedPhone;
        customer.Street      = street.Trim();
        customer.City        = city;
        customer.State       = state!;
        customer.ZipCode     = zipCode.Trim();

        _db.SaveChanges();
        HttpContext.Session.SetString("UserName", $"{customer.FirstName} {customer.LastName}");

        TempData["SuccessMessage"] = "Profile updated successfully.";
        return RedirectToAction("Details");
    }

    // Change Password

    [HttpGet]
    public IActionResult ChangePassword()
    {
        if (!IsCustomer) return RedirectToAction("Login");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
    {
        if (!IsCustomer) return RedirectToAction("Login");

        var customer = _db.Customers.Find(CustomerId);
        if (customer == null) return RedirectToAction("Logout");

        if (customer.Password != oldPassword)
        {
            ViewBag.Error = "Current password is incorrect.";
            return View();
        }
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ViewBag.Error = "New password cannot be empty.";
            return View();
        }
        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "New passwords do not match.";
            return View();
        }

        customer.Password = newPassword;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Password changed successfully.";
        return RedirectToAction("Details");
    }

    // Credit Cards

    public IActionResult CreditCards()
    {
        if (!IsCustomer) return RedirectToAction("Login");
        return CreditCardsView();
    }

    [HttpGet]
    public IActionResult ConfirmRemoveCard(int cardId)
    {
        if (!IsCustomer) return RedirectToAction("Login");

        var card = _db.CreditCards.FirstOrDefault(c =>
            c.CreditCardID == cardId && c.CustomerID == CustomerId && c.IsActive);
        if (card == null) return NotFound();

        var model = new ActionConfirmationViewModel
        {
            Title = "Remove Credit Card",
            Heading = "Remove Credit Card",
            Message = "This card will be removed from your account.",
            ConfirmAction = nameof(RemoveCard),
            ConfirmButtonText = "Remove Card",
            ConfirmButtonClass = "btn-danger",
            CancelUrl = Url.Action(nameof(CreditCards)) ?? "/Account/CreditCards",
            HiddenFields = new Dictionary<string, string>
            {
                ["cardId"] = card.CreditCardID.ToString()
            },
            Details = new List<string>
            {
                $"{card.DisplayCardType} ending in {card.CardNumber[^4..]}"
            }
        };

        return View("~/Views/Shared/ActionConfirmation.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddCard(string cardNumber)
    {
        if (!IsCustomer) return RedirectToAction("Login");

        var activeCards = _db.CreditCards.Count(c => c.CustomerID == CustomerId && c.IsActive);
        if (activeCards >= 2)
        {
            return CreditCardsView("You may only have up to 2 credit cards on file.", cardNumber);
        }

        string digitsOnly = new(cardNumber?.Where(char.IsDigit).ToArray() ?? []);
        var (cardType, error) = DetectCardType(digitsOnly);
        if (error != null)
        {
            return CreditCardsView(error, cardNumber);
        }

        // Use next available ID (CreditCard uses explicit IDs, so find max+1)
        int nextId = _db.CreditCards.Any() ? _db.CreditCards.Max(c => c.CreditCardID) + 1 : 2000;

        var card = new CreditCard
        {
            CreditCardID = nextId,
            CustomerID   = CustomerId!.Value,
            CardNumber   = digitsOnly,
            CardType     = cardType!,
            IsActive     = true
        };
        _db.CreditCards.Add(card);
        _db.SaveChanges();

        TempData["SuccessMessage"] = $"{cardType} ending in {digitsOnly[^4..]} added.";
        return RedirectToAction("CreditCards");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveCard(int cardId)
    {
        if (!IsCustomer) return RedirectToAction("Login");

        var card = _db.CreditCards.FirstOrDefault(c =>
            c.CreditCardID == cardId && c.CustomerID == CustomerId);
        if (card != null)
        {
            card.IsActive = false;
            _db.SaveChanges();
            TempData["SuccessMessage"] = "Credit card removed.";
        }
        return RedirectToAction("CreditCards");
    }

    // My Reviews

    public IActionResult MyReviews()
    {
        if (!IsCustomer) return RedirectToAction("Login");

        var songReviews = _db.SongReviews
            .Include(r => r.Song).ThenInclude(s => s.Artist)
            .Where(r => r.CustomerID == CustomerId)
            .OrderByDescending(r => r.DateCreated)
            .ToList();

        var albumReviews = _db.AlbumReviews
            .Include(r => r.Album)
            .Where(r => r.CustomerID == CustomerId)
            .OrderByDescending(r => r.DateCreated)
            .ToList();

        var artistReviews = _db.ArtistReviews
            .Include(r => r.Artist)
            .Where(r => r.CustomerID == CustomerId)
            .OrderByDescending(r => r.DateCreated)
            .ToList();

        ViewBag.SongReviews   = songReviews;
        ViewBag.AlbumReviews  = albumReviews;
        ViewBag.ArtistReviews = artistReviews;
        ViewBag.TotalCount    = songReviews.Count + albumReviews.Count + artistReviews.Count;
        return View();
    }

    // Order History

    public IActionResult OrderHistory()
    {
        if (!IsCustomer) return RedirectToAction("Login");

        var orders = _db.Orders
            .Include(o => o.CreditCard)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Song)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Album)
            .Where(o => o.CustomerID == CustomerId && o.Status != OrderStatus.InCart)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        return View("OrderHistory", orders);
    }

    // My Music

    public IActionResult MyMusic(string? filterTitle, string? filterArtist,
        string? filterAlbum, string? filterGenre, string? sortBy)
    {
        if (!IsCustomer) return RedirectToAction("Login");

        var customer = _db.Customers.Find(CustomerId);
        if (customer == null) return RedirectToAction("Logout");

        string customerEmail = customer.Email.Trim();

        // Collect all non-refunded order items owned by this customer.
        // Gifts belong to the recipient, not the purchaser.
        var songItems = _db.OrderItems
            .Include(oi => oi.Song).ThenInclude(s => s!.Artist)
            .Include(oi => oi.Song).ThenInclude(s => s!.SongGenres).ThenInclude(sg => sg.Genre)
            .Include(oi => oi.Song).ThenInclude(s => s!.AlbumSongs).ThenInclude(als => als.Album)
            .Include(oi => oi.Album)
            .Include(oi => oi.Order).ThenInclude(o => o.Customer)
            .Where(oi => ((!oi.Order.IsGift && oi.Order.CustomerID == CustomerId)
                       || (oi.Order.IsGift && oi.Order.GiftRecipientEmail == customerEmail))
                      && oi.Order.Status == OrderStatus.Ordered
                      && !oi.Order.IsRefunded)
            .ToList();

        // Build flat list: (song, albumContext, isGift, giftFromName, giftDate)
        var music = new List<(Song Song, string? AlbumContext, bool IsGift, string? GiftFromName, DateTime? GiftDate)>();
        foreach (var oi in songItems)
        {
            bool isGift = oi.Order.IsGift;
            string? fromName = isGift ? $"{oi.Order.Customer.FirstName} {oi.Order.Customer.LastName}".Trim() : null;
            DateTime? giftDate = isGift ? oi.Order.OrderDate : null;

            if (oi.SongID != null && oi.Song != null)
                music.Add((oi.Song, null, isGift, fromName, giftDate));
            else if (oi.AlbumID != null && oi.Album != null)
            {
                var songs = _db.AlbumSongs
                    .Include(als => als.Song).ThenInclude(s => s.Artist)
                    .Include(als => als.Song).ThenInclude(s => s.SongGenres).ThenInclude(sg => sg.Genre)
                    .Where(als => als.AlbumID == oi.AlbumID)
                    .Select(als => als.Song)
                    .ToList();
                foreach (var s in songs)
                    music.Add((s, oi.Album.Title, isGift, fromName, giftDate));
            }
        }

        // Deduplicate by SongID. If any ownership record for the same song was a gift,
        // surface the gift badge (keeping giver/date from the gift record).
        var seen = new Dictionary<int, (Song Song, string? AlbumContext, bool IsGift, string? GiftFromName, DateTime? GiftDate)>();
        foreach (var row in music)
        {
            if (!seen.TryGetValue(row.Song.SongID, out var existing))
                seen[row.Song.SongID] = row;
            else if (row.IsGift && !existing.IsGift)
                seen[row.Song.SongID] = (existing.Song, existing.AlbumContext, true, row.GiftFromName, row.GiftDate);
        }
        var deduped = seen.Values.ToList();

        // Filters
        if (!string.IsNullOrWhiteSpace(filterTitle))
            deduped = deduped.Where(x => x.Song.Title.Contains(filterTitle, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(filterArtist))
            deduped = deduped.Where(x => x.Song.Artist?.Name.Contains(filterArtist, StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (!string.IsNullOrWhiteSpace(filterAlbum))
            deduped = deduped.Where(x => x.AlbumContext?.Contains(filterAlbum, StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (!string.IsNullOrWhiteSpace(filterGenre))
            deduped = deduped.Where(x => x.Song.SongGenres.Any(sg => sg.Genre.Name.Contains(filterGenre, StringComparison.OrdinalIgnoreCase))).ToList();

        // Sort
        deduped = sortBy switch
        {
            "artist"     => deduped.OrderBy(x => x.Song.Artist?.Name).ToList(),
            "artist_desc"=> deduped.OrderByDescending(x => x.Song.Artist?.Name).ToList(),
            "title_desc" => deduped.OrderByDescending(x => x.Song.Title).ToList(),
            "genre"      => deduped.OrderBy(x => x.Song.SongGenres.FirstOrDefault()?.Genre.Name).ToList(),
            _            => deduped.OrderBy(x => x.Song.Title).ToList()
        };

        ViewBag.Music      = deduped;
        ViewBag.FilterTitle  = filterTitle;
        ViewBag.FilterArtist = filterArtist;
        ViewBag.FilterAlbum  = filterAlbum;
        ViewBag.FilterGenre  = filterGenre;
        ViewBag.SortBy       = sortBy;
        return View();
    }

    // Helpers

    public static (string? City, string? State) LookupZip(string zip)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var json = client.GetStringAsync($"https://api.zippopotam.us/us/{zip}").Result;
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var places = doc.RootElement.GetProperty("places");
            var place = places[0];
            var city  = place.GetProperty("place name").GetString() ?? "";
            var state = place.GetProperty("state abbreviation").GetString() ?? "";
            return (city, state);
        }
        catch
        {
            return (null, null);
        }
    }

    public static (string? CardType, string? Error) DetectCardType(string cardNumber)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 15)
            return ("American Express", null);
        if (digits.Length == 16)
        {
            if (digits.StartsWith("54"))   return ("Mastercard", null);
            if (digits.StartsWith("4"))    return ("Visa", null);
            if (digits.StartsWith("6"))    return ("Discover", null);
            return (null, "Could not determine card type. Card must start with 54 (MC), 4 (Visa), or 6 (Discover).");
        }
        return (null, "Card number must be 15 digits (Amex) or 16 digits (Visa/MC/Discover).");
    }

    public static bool IsValidEmailAddress(string? email) =>
        !string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email.Trim());

    private void SetRegisterViewBag(string? firstName, string? lastName, string? email,
        string? phoneNumber, string? street, string? zipCode)
    {
        ViewBag.FirstName = firstName;
        ViewBag.LastName = lastName;
        ViewBag.Email = email;
        ViewBag.PhoneNumber = phoneNumber;
        ViewBag.Street = street;
        ViewBag.ZipCode = zipCode;
    }

    private void SetLoginViewBag(string? email, string? returnUrl)
    {
        ViewBag.Email = email;
        ViewBag.ReturnUrl = returnUrl;
    }

    private IActionResult CreditCardsView(string? error = null, string? cardNumber = null)
    {
        ViewBag.Error = error;
        ViewBag.CardNumber = cardNumber;
        var cards = _db.CreditCards
            .Where(c => c.CustomerID == CustomerId && c.IsActive)
            .ToList();
        return View("CreditCards", cards);
    }

    private void ApplyCustomerFormValues(Customer customer, string? firstName, string? lastName,
        string? email, string? phoneNumber, string? street, string? zipCode)
    {
        customer.FirstName = firstName?.Trim() ?? "";
        customer.LastName = lastName?.Trim() ?? "";
        customer.Email = email?.Trim() ?? "";
        customer.PhoneNumber = phoneNumber ?? "";
        customer.Street = street?.Trim() ?? "";
        customer.ZipCode = zipCode?.Trim() ?? "";
    }

    public static string? FormatPhoneNumber(string phoneNumber)
    {
        string digits = new(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length != 10) return null;
        return $"({digits[..3]}) {digits.Substring(3, 3)}-{digits[6..]}";
    }

}
