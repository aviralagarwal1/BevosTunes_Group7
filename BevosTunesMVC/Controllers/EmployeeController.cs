using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Models;
using BevosTunesMVC.Services;

namespace BevosTunesMVC.Controllers;

public class EmployeeController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailSender _email;

    public EmployeeController(AppDbContext db, EmailSender email)
    {
        _db = db;
        _email = email;
    }

    private int? EmployeeId => HttpContext.Session.GetInt32("EmployeeID");
    private bool IsManager  => HttpContext.Session.GetString("IsManager") == "true";

    private IActionResult RequireEmployee()
    {
        if (!EmployeeId.HasValue) return RedirectToAction("Login", "Account");
        return null!;
    }

    // Dashboard

    public IActionResult Dashboard()
    {
        var r = RequireEmployee(); if (r != null) return r;

        var employee = _db.Employees.Find(EmployeeId!.Value);
        ViewBag.Employee  = employee;
        ViewBag.IsManager = IsManager;

        // Pending reviews count
        ViewBag.PendingSongReviews   = _db.SongReviews.Count(r => r.Status == ReviewStatus.Pending);
        ViewBag.PendingAlbumReviews  = _db.AlbumReviews.Count(r => r.Status == ReviewStatus.Pending);
        ViewBag.PendingArtistReviews = _db.ArtistReviews.Count(r => r.Status == ReviewStatus.Pending);

        return View();
    }

    // Employee Account Edit

    [HttpGet]
    public IActionResult EditAccount()
    {
        var r = RequireEmployee(); if (r != null) return r;
        var emp = _db.Employees.Find(EmployeeId!.Value);
        if (emp == null) return RedirectToAction("Logout", "Account");
        return View(emp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditAccount(string street, string zipCode, string phoneNumber)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var emp = _db.Employees.Find(EmployeeId!.Value);
        if (emp == null) return RedirectToAction("Logout", "Account");

        emp.Street = street?.Trim() ?? "";
        emp.ZipCode = zipCode?.Trim() ?? "";
        emp.PhoneNumber = phoneNumber ?? "";

        if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(zipCode) || string.IsNullOrWhiteSpace(phoneNumber))
        {
            ViewBag.Error = "All fields are required.";
            return View(emp);
        }

        var (city, state) = AccountController.LookupZip(zipCode.Trim());
        if (city == null)
        {
            ViewBag.Error = "Could not find city/state for that ZIP code.";
            return View(emp);
        }

        emp.Street      = street.Trim();
        emp.City        = city;
        emp.State       = state!;
        emp.ZipCode     = zipCode.Trim();
        emp.PhoneNumber = phoneNumber.Trim();
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Account updated.";
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        var r = RequireEmployee(); if (r != null) return r;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var emp = _db.Employees.Find(EmployeeId!.Value);
        if (emp == null) return RedirectToAction("Logout", "Account");

        if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            ViewBag.Error = "All password fields are required.";
            return View();
        }
        if (emp.Password != oldPassword)
        {
            ViewBag.Error = "Current password is incorrect.";
            return View();
        }
        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "New passwords do not match.";
            return View();
        }
        emp.Password = newPassword;
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Password changed.";
        return RedirectToAction("Dashboard");
    }

    // Customer Management

    public IActionResult Customers()
    {
        var r = RequireEmployee(); if (r != null) return r;
        var customers = _db.Customers.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToList();
        return View(customers);
    }

    [HttpGet]
    public IActionResult CreateCustomer()
    {
        var r = RequireEmployee(); if (r != null) return r;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateCustomer(string firstName, string lastName, string email,
        string password, string phoneNumber, string street, string zipCode)
    {
        var r = RequireEmployee(); if (r != null) return r;
        SetCustomerCreateViewBag(firstName, lastName, email, phoneNumber, street, zipCode);

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(street) ||
            string.IsNullOrWhiteSpace(zipCode))
        {
            ViewBag.Error = "All fields are required.";
            return View();
        }
        if (!AccountController.IsValidEmailAddress(email))
        {
            ViewBag.Error = "Please enter a valid email address.";
            return View();
        }

        if (_db.Customers.Any(c => c.Email == email.Trim()))
        {
            ViewBag.Error = "A customer with that email already exists.";
            return View();
        }

        string? formattedPhone = AccountController.FormatPhoneNumber(phoneNumber);
        if (formattedPhone == null)
        {
            ViewBag.Error = "Phone number must contain exactly 10 digits.";
            return View();
        }

        var (city, state) = AccountController.LookupZip(zipCode.Trim());
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

        _email.TrySend(customer.Email,
            $"{_email.SubjectPrefix} Welcome to BevosTunes!",
            $"Hi {customer.FirstName},\n\nAn account has been created for you on BevosTunes.\n\nEmail: {customer.Email}\n\nLog in at any time to explore and purchase music:\n{loginUrl}\n\n- The BevosTunes Team");

        TempData["SuccessMessage"] = $"Customer {customer.FirstName} {customer.LastName} created.";
        return RedirectToAction("Customers");
    }

    [HttpGet]
    public IActionResult EditCustomer(int id)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var customer = _db.Customers.Find(id);
        if (customer == null) return NotFound();
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditCustomer(int customerId, string firstName, string lastName,
        string phoneNumber, string street, string zipCode)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var customer = _db.Customers.Find(customerId);
        if (customer == null) return NotFound();

        customer.FirstName = firstName?.Trim() ?? "";
        customer.LastName = lastName?.Trim() ?? "";
        customer.PhoneNumber = phoneNumber ?? "";
        customer.Street = street?.Trim() ?? "";
        customer.ZipCode = zipCode?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(street) ||
            string.IsNullOrWhiteSpace(zipCode))
        {
            ViewBag.Error = "All fields are required.";
            return View(customer);
        }

        string? formattedPhone = AccountController.FormatPhoneNumber(phoneNumber);
        if (formattedPhone == null)
        {
            ViewBag.Error = "Phone number must contain exactly 10 digits.";
            return View(customer);
        }

        var (city, state) = AccountController.LookupZip(zipCode.Trim());
        if (city == null)
        {
            ViewBag.Error = "Could not find city/state for that ZIP code.";
            return View(customer);
        }

        customer.FirstName   = firstName.Trim();
        customer.LastName    = lastName.Trim();
        customer.PhoneNumber = formattedPhone;
        customer.Street      = street.Trim();
        customer.City        = city;
        customer.State       = state!;
        customer.ZipCode     = zipCode.Trim();
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Customer updated.";
        return RedirectToAction("Customers");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeCustomerPassword(int customerId, string newPassword)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var customer = _db.Customers.Find(customerId);
        if (customer == null) return NotFound();

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ViewBag.Error = "New password is required.";
            return View("EditCustomer", customer);
        }

        customer.Password = newPassword;
        _db.SaveChanges();
        TempData["SuccessMessage"] = "Customer password changed.";
        return RedirectToAction("EditCustomer", new { id = customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleCustomer(int customerId)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var customer = _db.Customers.Find(customerId);
        if (customer == null) return NotFound();
        customer.IsActive = !customer.IsActive;
        _db.SaveChanges();
        TempData["SuccessMessage"] = customer.IsActive
            ? "Customer account re-enabled."
            : "Customer account disabled.";
        return RedirectToAction("Customers");
    }

    // Review Approval

    public IActionResult PendingReviews()
    {
        var r = RequireEmployee(); if (r != null) return r;

        ViewBag.SongReviews = _db.SongReviews
            .Include(r2 => r2.Customer).Include(r2 => r2.Song)
            .Where(r2 => r2.Status == ReviewStatus.Pending)
            .ToList();
        ViewBag.AlbumReviews = _db.AlbumReviews
            .Include(r2 => r2.Customer).Include(r2 => r2.Album)
            .Where(r2 => r2.Status == ReviewStatus.Pending)
            .ToList();
        ViewBag.ArtistReviews = _db.ArtistReviews
            .Include(r2 => r2.Customer).Include(r2 => r2.Artist)
            .Where(r2 => r2.Status == ReviewStatus.Pending)
            .ToList();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApproveSongReview(int reviewId)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.SongReviews.Find(reviewId);
        if (review != null)
        {
            review.Status              = ReviewStatus.Approved;
            review.ApprovingEmployeeID = EmployeeId!.Value;
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review approved.";
        return RedirectToAction("PendingReviews");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RejectSongReview(int reviewId)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.SongReviews.Find(reviewId);
        if (review != null)
        {
            review.Status              = ReviewStatus.Rejected;
            review.ApprovingEmployeeID = EmployeeId!.Value;
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review rejected.";
        return RedirectToAction("PendingReviews");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditSongReviewText(int reviewId, string? reviewText)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.SongReviews.Find(reviewId);
        if (review != null)
        {
            var text = reviewText?.Trim();
            review.ReviewText = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)];
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review text updated.";
        return RedirectToAction("PendingReviews");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApproveAlbumReview(int reviewId)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.AlbumReviews.Find(reviewId);
        if (review != null)
        {
            review.Status              = ReviewStatus.Approved;
            review.ApprovingEmployeeID = EmployeeId!.Value;
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review approved.";
        return RedirectToAction("PendingReviews");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RejectAlbumReview(int reviewId)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.AlbumReviews.Find(reviewId);
        if (review != null)
        {
            review.Status              = ReviewStatus.Rejected;
            review.ApprovingEmployeeID = EmployeeId!.Value;
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review rejected.";
        return RedirectToAction("PendingReviews");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApproveArtistReview(int reviewId)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.ArtistReviews.Find(reviewId);
        if (review != null)
        {
            review.Status              = ReviewStatus.Approved;
            review.ApprovingEmployeeID = EmployeeId!.Value;
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review approved.";
        return RedirectToAction("PendingReviews");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RejectArtistReview(int reviewId)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.ArtistReviews.Find(reviewId);
        if (review != null)
        {
            review.Status              = ReviewStatus.Rejected;
            review.ApprovingEmployeeID = EmployeeId!.Value;
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review rejected.";
        return RedirectToAction("PendingReviews");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditAlbumReviewText(int reviewId, string? reviewText)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.AlbumReviews.Find(reviewId);
        if (review != null)
        {
            var text = reviewText?.Trim();
            review.ReviewText = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)];
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review text updated.";
        return RedirectToAction("PendingReviews");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditArtistReviewText(int reviewId, string? reviewText)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var review = _db.ArtistReviews.Find(reviewId);
        if (review != null)
        {
            var text = reviewText?.Trim();
            review.ReviewText = string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 100)];
            _db.SaveChanges();
        }
        TempData["SuccessMessage"] = "Review text updated.";
        return RedirectToAction("PendingReviews");
    }

    // Purchase on Behalf of Customer

    public IActionResult SelectCustomer()
    {
        var r = RequireEmployee(); if (r != null) return r;
        var customers = _db.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToList();
        return View(customers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImpersonateCustomer(int customerId)
    {
        var r = RequireEmployee(); if (r != null) return r;
        var customer = _db.Customers.Find(customerId);
        if (customer == null) return NotFound();

        // Store proxy customer in session; keep employee session intact
        HttpContext.Session.SetInt32("ProxyCustomerID", customerId);
        HttpContext.Session.SetInt32("CustomerID", customerId);
        HttpContext.Session.SetString("UserName", $"{customer.FirstName} {customer.LastName} (via Employee)");

        TempData["SuccessMessage"] = $"Now acting as customer: {customer.FirstName} {customer.LastName}. Complete checkout to place order on their behalf.";
        return RedirectToAction("Index", "Cart");
    }

    public IActionResult StopImpersonating()
    {
        var r = RequireEmployee(); if (r != null) return r;
        int? empId = EmployeeId;
        HttpContext.Session.Remove("CustomerID");
        HttpContext.Session.Remove("ProxyCustomerID");
        HttpContext.Session.Remove("UserName");

        // Restore employee name
        var emp = _db.Employees.Find(empId);
        if (emp != null)
            HttpContext.Session.SetString("UserName", $"{emp.FirstName} {emp.LastName}");

        TempData["SuccessMessage"] = "Returned to employee mode.";
        return RedirectToAction("Dashboard");
    }

    private void SetCustomerCreateViewBag(string? firstName, string? lastName, string? email,
        string? phoneNumber, string? street, string? zipCode)
    {
        ViewBag.FirstName = firstName;
        ViewBag.LastName = lastName;
        ViewBag.Email = email;
        ViewBag.PhoneNumber = phoneNumber;
        ViewBag.Street = street;
        ViewBag.ZipCode = zipCode;
    }
}
