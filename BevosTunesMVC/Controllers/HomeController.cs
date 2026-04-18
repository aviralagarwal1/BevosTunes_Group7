using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.Models;
using BevosTunesMVC.DAL;

namespace BevosTunesMVC.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    public IActionResult Index()
    {
        bool isCustomer = HttpContext.Session.GetInt32("CustomerID").HasValue;

        if (isCustomer)
        {
            // Logged-in customers: show featured items
            var featured = _db.FeaturedItems
                .Include(f => f.Song).ThenInclude(s => s!.Artist)
                .Include(f => f.Song).ThenInclude(s => s!.Discounts.Where(d => d.IsActive))
                .Include(f => f.Album).ThenInclude(a => a!.AlbumArtists).ThenInclude(aa => aa.Artist)
                .Include(f => f.Album).ThenInclude(a => a!.Discounts.Where(d => d.IsActive))
                .Include(f => f.Artist)
                .Where(f => f.IsActive)
                .ToList();

            ViewBag.FeaturedItems = featured;
            ViewBag.IsCustomer    = true;
        }
        else
        {
            // Anonymous: show full song list
            var songs = _db.Songs
                .Include(s => s.Artist)
                .Include(s => s.Discounts.Where(d => d.IsActive))
                .Where(s => s.IsActive)
                .OrderBy(s => s.Title)
                .ToList();

            ViewBag.Songs      = songs;
            ViewBag.IsCustomer = false;
        }

        return View();
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
