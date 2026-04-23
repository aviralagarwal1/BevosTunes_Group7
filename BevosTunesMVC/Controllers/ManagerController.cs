using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Models;
using System.ComponentModel.DataAnnotations;

namespace BevosTunesMVC.Controllers;

public class ManagerController : Controller
{
    private readonly AppDbContext _db;
    public ManagerController(AppDbContext db) => _db = db;

    private int? EmployeeId => HttpContext.Session.GetInt32("EmployeeID");
    private bool IsManager  => HttpContext.Session.GetString("IsManager") == "true";

    private IActionResult RequireManager()
    {
        if (!EmployeeId.HasValue || !IsManager)
        {
            TempData["ErrorMessage"] = "Manager access required.";
            return RedirectToAction("Dashboard", "Employee");
        }
        return null!;
    }

    // =======================================================================
    // EMPLOYEE MANAGEMENT
    // =======================================================================

    public IActionResult Employees()
    {
        var r = RequireManager(); if (r != null) return r;
        var emps = _db.Employees.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToList();
        return View(emps);
    }

    [HttpGet]
    public IActionResult HireEmployee()
    {
        var r = RequireManager(); if (r != null) return r;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult HireEmployee(string firstName, string lastName, string email,
        string password, string phoneNumber, string street, string city,
        string state, string zipCode, bool isManager)
    {
        var r = RequireManager(); if (r != null) return r;
        SetEmployeeFormViewBag(firstName, lastName, email, phoneNumber, street, city, state, zipCode, isManager);

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(street) ||
            string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(zipCode))
        {
            ViewBag.Error = "All fields are required.";
            return View();
        }
        if (!new EmailAddressAttribute().IsValid(email.Trim()))
        {
            ViewBag.Error = "Please enter a valid email address.";
            return View();
        }

        if (_db.Employees.Any(e => e.Email == email.Trim()))
        {
            ViewBag.Error = "An employee with that email already exists.";
            return View();
        }

        var emp = new Employee
        {
            FirstName   = firstName.Trim(),
            LastName    = lastName.Trim(),
            Email       = email.Trim(),
            Password    = password,
            PhoneNumber = phoneNumber.Trim(),
            Street      = street.Trim(),
            City        = city.Trim(),
            State       = state.Trim(),
            ZipCode     = zipCode.Trim(),
            IsActive    = true,
            IsManager   = isManager
        };
        _db.Employees.Add(emp);
        _db.SaveChanges();

        TempData["SuccessMessage"] = $"Employee {emp.FirstName} {emp.LastName} hired.";
        return RedirectToAction("Employees");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult FireEmployee(int employeeId)
    {
        var r = RequireManager(); if (r != null) return r;
        var emp = _db.Employees.Find(employeeId);
        if (emp != null)
        {
            emp.IsActive = false;
            _db.SaveChanges();
            TempData["SuccessMessage"] = $"{emp.FirstName} {emp.LastName} has been fired.";
        }
        return RedirectToAction("Employees");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RehireEmployee(int employeeId)
    {
        var r = RequireManager(); if (r != null) return r;
        var emp = _db.Employees.Find(employeeId);
        if (emp != null)
        {
            emp.IsActive = true;
            _db.SaveChanges();
            TempData["SuccessMessage"] = $"{emp.FirstName} {emp.LastName} has been rehired.";
        }
        return RedirectToAction("Employees");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PromoteEmployee(int employeeId)
    {
        var r = RequireManager(); if (r != null) return r;
        var emp = _db.Employees.Find(employeeId);
        if (emp != null)
        {
            emp.IsManager = true;
            _db.SaveChanges();
            TempData["SuccessMessage"] = $"{emp.FirstName} {emp.LastName} promoted to manager.";
        }
        return RedirectToAction("Employees");
    }

    [HttpGet]
    public IActionResult EditEmployee(int id)
    {
        var r = RequireManager(); if (r != null) return r;
        var emp = _db.Employees.Find(id);
        if (emp == null) return NotFound();
        return View(emp);
    }

    [HttpGet]
    public IActionResult ConfirmFireEmployee(int employeeId)
    {
        var r = RequireManager(); if (r != null) return r;
        var emp = _db.Employees.Find(employeeId);
        if (emp == null) return NotFound();

        var model = new ActionConfirmationViewModel
        {
            Title = "Fire Employee",
            Heading = "Fire Employee",
            Message = "This will mark the employee inactive. Their record stays in the system and they can be rehired later.",
            ConfirmAction = nameof(FireEmployee),
            ConfirmButtonText = "Fire Employee",
            ConfirmButtonClass = "btn-danger",
            CancelUrl = Url.Action(nameof(Employees)) ?? "/Manager/Employees",
            HiddenFields = new Dictionary<string, string>
            {
                ["employeeId"] = emp.EmployeeID.ToString()
            },
            Details = new List<string>
            {
                $"{emp.FirstName} {emp.LastName}",
                emp.Email
            }
        };

        return View("~/Views/Shared/ActionConfirmation.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditEmployee(int employeeId, string firstName, string lastName,
        string phoneNumber, string street, string city, string state, string zipCode)
    {
        var r = RequireManager(); if (r != null) return r;
        var emp = _db.Employees.Find(employeeId);
        if (emp == null) return NotFound();

        emp.FirstName = firstName?.Trim() ?? "";
        emp.LastName = lastName?.Trim() ?? "";
        emp.PhoneNumber = phoneNumber ?? "";
        emp.Street = street?.Trim() ?? "";
        emp.City = city?.Trim() ?? "";
        emp.State = state?.Trim() ?? "";
        emp.ZipCode = zipCode?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(street) ||
            string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(zipCode))
        {
            ViewBag.Error = "All fields are required.";
            return View(emp);
        }

        emp.FirstName   = firstName.Trim();
        emp.LastName    = lastName.Trim();
        emp.PhoneNumber = phoneNumber.Trim();
        emp.Street      = street.Trim();
        emp.City        = city.Trim();
        emp.State       = state.Trim();
        emp.ZipCode     = zipCode.Trim();
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Employee updated.";
        return RedirectToAction("Employees");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetEmployeePassword(int employeeId, string newPassword)
    {
        var r = RequireManager(); if (r != null) return r;
        var emp = _db.Employees.Find(employeeId);
        if (emp == null) return NotFound();
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ViewBag.Error = "New password is required.";
            return View("EditEmployee", emp);
        }
        emp.Password = newPassword;
        _db.SaveChanges();
        TempData["SuccessMessage"] = "Password updated.";
        return RedirectToAction("EditEmployee", new { id = employeeId });
    }

    // =======================================================================
    // SONG MANAGEMENT
    // =======================================================================

    public IActionResult Songs()
    {
        var r = RequireManager(); if (r != null) return r;
        var songs = _db.Songs.Include(s => s.Artist).OrderBy(s => s.Title).ToList();
        return View(songs);
    }

    [HttpGet]
    public IActionResult AddSong()
    {
        var r = RequireManager(); if (r != null) return r;
        PrepareSongEditorView(isActive: true);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddSong(string? title, string? price, int? artistId, int[]? genreIds, bool isActive)
    {
        var r = RequireManager(); if (r != null) return r;
        genreIds ??= Array.Empty<int>();
        string trimmedTitle = title?.Trim() ?? "";
        int selectedArtistId = artistId ?? 0;
        decimal parsedPrice = 0m;

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            ViewBag.Error = "Song title is required.";
            PrepareSongEditorView(artistId, genreIds, title, price, isActive);
            return View();
        }
        if (string.IsNullOrWhiteSpace(price))
        {
            ViewBag.Error = "Price is required.";
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View();
        }
        if (!decimal.TryParse(price, out parsedPrice) || parsedPrice < 0.01m)
        {
            ViewBag.Error = "Price must be at least $0.01.";
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View();
        }
        if (!artistId.HasValue || !_db.Artists.Any(a => a.ArtistID == artistId.Value))
        {
            ViewBag.Error = "An artist selection is required.";
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View();
        }
        if (genreIds.Length == 0)
        {
            ViewBag.Error = "At least one genre is required.";
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View();
        }

        // Title unique per artist
        if (_db.Songs.Any(s => s.Title == trimmedTitle && s.ArtistID == selectedArtistId))
        {
            ViewBag.Error   = "This artist already has a song with that title.";
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View();
        }

        var song = new Song
        {
            Title    = trimmedTitle,
            Price    = parsedPrice,
            ArtistID = selectedArtistId,
            IsActive = isActive
        };
        _db.Songs.Add(song);
        _db.SaveChanges();

        foreach (var gid in genreIds)
            _db.SongGenres.Add(new SongGenre { SongID = song.SongID, GenreID = gid });
        _db.SaveChanges();

        TempData["SuccessMessage"] = $"Song \"{song.Title}\" added.";
        return RedirectToAction("Songs");
    }

    [HttpGet]
    public IActionResult EditSong(int id)
    {
        var r = RequireManager(); if (r != null) return r;
        var song = _db.Songs.Include(s => s.SongGenres).FirstOrDefault(s => s.SongID == id);
        if (song == null) return NotFound();
        PrepareSongEditorView(song.ArtistID, song.SongGenres.Select(sg => sg.GenreID).ToArray(),
            song.Title, song.Price.ToString("0.00"), song.IsActive);
        return View(song);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditSong(int songId, string? title, string? price,
        int? artistId, int[]? genreIds, bool isActive)
    {
        var r = RequireManager(); if (r != null) return r;
        var song = _db.Songs.Include(s => s.SongGenres).FirstOrDefault(s => s.SongID == songId);
        if (song == null) return NotFound();
        genreIds ??= Array.Empty<int>();
        string trimmedTitle = title?.Trim() ?? "";
        int selectedArtistId = artistId ?? 0;
        decimal parsedPrice = 0m;

        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            ViewBag.Error = "Song title is required.";
            song.Title = trimmedTitle;
            song.ArtistID = selectedArtistId;
            song.IsActive = isActive;
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View(song);
        }
        if (string.IsNullOrWhiteSpace(price))
        {
            ViewBag.Error = "Price is required.";
            song.Title = trimmedTitle;
            song.ArtistID = selectedArtistId;
            song.IsActive = isActive;
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View(song);
        }
        if (!decimal.TryParse(price, out parsedPrice) || parsedPrice < 0.01m)
        {
            ViewBag.Error = "Price must be at least $0.01.";
            song.Title = trimmedTitle;
            song.ArtistID = selectedArtistId;
            song.IsActive = isActive;
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View(song);
        }
        if (!artistId.HasValue || !_db.Artists.Any(a => a.ArtistID == artistId.Value))
        {
            ViewBag.Error = "An artist selection is required.";
            song.Title = trimmedTitle;
            song.Price = parsedPrice;
            song.ArtistID = selectedArtistId;
            song.IsActive = isActive;
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View(song);
        }
        if (genreIds.Length == 0)
        {
            ViewBag.Error = "At least one genre is required.";
            song.Title = trimmedTitle;
            song.Price = parsedPrice;
            song.ArtistID = selectedArtistId;
            song.IsActive = isActive;
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View(song);
        }

        if (_db.Songs.Any(s => s.Title == trimmedTitle && s.ArtistID == selectedArtistId && s.SongID != songId))
        {
            ViewBag.Error = "This artist already has a song with that title.";
            song.Title = trimmedTitle;
            song.Price = parsedPrice;
            song.ArtistID = selectedArtistId;
            song.IsActive = isActive;
            PrepareSongEditorView(artistId, genreIds, trimmedTitle, price, isActive);
            return View(song);
        }

        song.Title    = trimmedTitle;
        song.Price    = parsedPrice;
        song.ArtistID = selectedArtistId;
        song.IsActive = isActive;

        // Replace genres
        _db.SongGenres.RemoveRange(song.SongGenres);
        foreach (var gid in genreIds)
            _db.SongGenres.Add(new SongGenre { SongID = songId, GenreID = gid });
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Song updated.";
        return RedirectToAction("Songs");
    }

    // =======================================================================
    // ALBUM MANAGEMENT
    // =======================================================================

    public IActionResult Albums()
    {
        var r = RequireManager(); if (r != null) return r;
        var albums = _db.Albums
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .OrderBy(a => a.Title).ToList();
        return View(albums);
    }

    [HttpGet]
    public IActionResult AddAlbum()
    {
        var r = RequireManager(); if (r != null) return r;
        PrepareAlbumEditorView(isActive: true);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddAlbum(string? title, string? price, string? albumCoverURL,
        int[]? artistIds, int[]? genreIds, int[]? songIds, bool isActive, string submitAction = "save")
    {
        var r = RequireManager(); if (r != null) return r;

        artistIds ??= Array.Empty<int>();
        genreIds ??= Array.Empty<int>();
        songIds ??= Array.Empty<int>();

        var availableSongs = GetAvailableSongsForArtists(artistIds);
        var availableSongIds = availableSongs.Select(s => s.SongID).ToHashSet();
        songIds = songIds.Where(availableSongIds.Contains).Distinct().ToArray();

        if (submitAction == "refreshSongs")
        {
            PrepareAlbumEditorView(artistIds, genreIds, songIds, title, price, albumCoverURL, isActive);
            return View();
        }

        string? error = null;
        decimal parsedPrice = 0m;
        if (string.IsNullOrWhiteSpace(title))
            error = "Album title is required.";
        else if (!decimal.TryParse(price, out parsedPrice) || parsedPrice < 0.01m)
            error = "Price must be at least $0.01.";
        else if (string.IsNullOrWhiteSpace(albumCoverURL))
            error = "Album cover URL is required.";
        else if (!Uri.TryCreate(albumCoverURL.Trim(), UriKind.Absolute, out _))
            error = "Album cover URL must be a valid URL.";
        else if (artistIds.Length == 0)
            error = "At least one artist is required.";
        else if (genreIds.Length == 0)
            error = "At least one genre is required.";
        else if (songIds.Length == 0)
            error = "At least one song is required.";

        if (error != null)
        {
            ViewBag.Error   = error;
            PrepareAlbumEditorView(artistIds, genreIds, songIds, title, price, albumCoverURL, isActive);
            return View();
        }

        var album = new Album
        {
            Title         = title!.Trim(),
            Price         = parsedPrice,
            AlbumCoverURL = albumCoverURL!.Trim(),
            IsActive     = isActive
        };
        _db.Albums.Add(album);
        _db.SaveChanges();

        foreach (var aid in artistIds)
            _db.AlbumArtists.Add(new AlbumArtist { AlbumID = album.AlbumID, ArtistID = aid });
        foreach (var gid in genreIds)
            _db.AlbumGenres.Add(new AlbumGenre { AlbumID = album.AlbumID, GenreID = gid });
        foreach (var sid in songIds)
            _db.AlbumSongs.Add(new AlbumSong { AlbumID = album.AlbumID, SongID = sid });
        _db.SaveChanges();

        TempData["SuccessMessage"] = $"Album \"{album.Title}\" added.";
        return RedirectToAction("Albums");
    }

    [HttpGet]
    public IActionResult EditAlbum(int id)
    {
        var r = RequireManager(); if (r != null) return r;
        var album = _db.Albums
            .Include(a => a.AlbumArtists)
            .Include(a => a.AlbumGenres)
            .Include(a => a.AlbumSongs)
            .FirstOrDefault(a => a.AlbumID == id);
        if (album == null) return NotFound();

        PrepareAlbumEditorView(
            album.AlbumArtists.Select(aa => aa.ArtistID).ToArray(),
            album.AlbumGenres.Select(ag => ag.GenreID).ToArray(),
            album.AlbumSongs.Select(als => als.SongID).ToArray(),
            album.Title,
            album.Price.ToString("0.00"),
            album.AlbumCoverURL,
            album.IsActive);

        return View(album);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditAlbum(int albumId, string? title, string? price,
        string? albumCoverURL, int[]? artistIds, int[]? genreIds, int[]? songIds, bool isActive,
        string submitAction = "save")
    {
        var r = RequireManager(); if (r != null) return r;
        var album = _db.Albums
            .Include(a => a.AlbumArtists)
            .Include(a => a.AlbumGenres)
            .Include(a => a.AlbumSongs)
            .FirstOrDefault(a => a.AlbumID == albumId);
        if (album == null) return NotFound();

        artistIds ??= Array.Empty<int>();
        genreIds ??= Array.Empty<int>();
        songIds ??= Array.Empty<int>();

        var availableSongs = GetAvailableSongsForArtists(artistIds);
        var availableSongIds = availableSongs.Select(s => s.SongID).ToHashSet();
        songIds = songIds.Where(availableSongIds.Contains).Distinct().ToArray();

        if (submitAction == "refreshSongs")
        {
            PrepareAlbumEditorView(artistIds, genreIds, songIds, title, price, albumCoverURL, isActive);
            return View(album);
        }

        string? error = null;
        decimal parsedPrice = 0m;
        if (string.IsNullOrWhiteSpace(title))
            error = "Album title is required.";
        else if (!decimal.TryParse(price, out parsedPrice) || parsedPrice < 0.01m)
            error = "Price must be at least $0.01.";
        else if (string.IsNullOrWhiteSpace(albumCoverURL))
            error = "Album cover URL is required.";
        else if (!Uri.TryCreate(albumCoverURL.Trim(), UriKind.Absolute, out _))
            error = "Album cover URL must be a valid URL.";
        else if (artistIds.Length == 0)
            error = "At least one artist is required.";
        else if (genreIds.Length == 0)
            error = "At least one genre is required.";
        else if (songIds.Length == 0)
            error = "At least one song is required.";

        if (error != null)
        {
            ViewBag.Error = error;
            PrepareAlbumEditorView(artistIds, genreIds, songIds, title, price, albumCoverURL, isActive);
            return View(album);
        }

        album.Title         = title!.Trim();
        album.Price         = parsedPrice;
        album.AlbumCoverURL = albumCoverURL!.Trim();
        album.IsActive      = isActive;

        _db.AlbumArtists.RemoveRange(album.AlbumArtists);
        _db.AlbumGenres.RemoveRange(album.AlbumGenres);
        _db.AlbumSongs.RemoveRange(album.AlbumSongs);
        _db.SaveChanges();

        foreach (var aid in artistIds)
            _db.AlbumArtists.Add(new AlbumArtist { AlbumID = albumId, ArtistID = aid });
        foreach (var gid in genreIds)
            _db.AlbumGenres.Add(new AlbumGenre { AlbumID = albumId, GenreID = gid });
        foreach (var sid in songIds)
            _db.AlbumSongs.Add(new AlbumSong { AlbumID = albumId, SongID = sid });
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Album updated.";
        return RedirectToAction("Albums");
    }

    // =======================================================================
    // ARTIST MANAGEMENT
    // =======================================================================

    public IActionResult Artists()
    {
        var r = RequireManager(); if (r != null) return r;
        var artists = _db.Artists.OrderBy(a => a.Name).ToList();
        return View(artists);
    }

    [HttpGet]
    public IActionResult AddArtist()
    {
        var r = RequireManager(); if (r != null) return r;
        PrepareArtistEditorView();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddArtist(string? name, int[]? genreIds)
    {
        var r = RequireManager(); if (r != null) return r;
        genreIds ??= Array.Empty<int>();
        string trimmedName = name?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ViewBag.Error = "Artist name is required.";
            PrepareArtistEditorView(genreIds, name);
            return View();
        }
        if (genreIds.Length == 0)
        {
            ViewBag.Error = "At least one genre is required.";
            PrepareArtistEditorView(genreIds, trimmedName);
            return View();
        }

        var artist = new Artist { Name = trimmedName };
        _db.Artists.Add(artist);
        _db.SaveChanges();

        foreach (var gid in genreIds)
            _db.ArtistGenres.Add(new ArtistGenre { ArtistID = artist.ArtistID, GenreID = gid });
        _db.SaveChanges();

        TempData["SuccessMessage"] = $"Artist \"{artist.Name}\" added.";
        return RedirectToAction("Artists");
    }

    [HttpGet]
    public IActionResult EditArtist(int id)
    {
        var r = RequireManager(); if (r != null) return r;
        var artist = _db.Artists.Include(a => a.ArtistGenres).FirstOrDefault(a => a.ArtistID == id);
        if (artist == null) return NotFound();
        PrepareArtistEditorView(artist.ArtistGenres.Select(ag => ag.GenreID).ToArray(), artist.Name);
        return View(artist);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditArtist(int artistId, string? name, int[]? genreIds)
    {
        var r = RequireManager(); if (r != null) return r;
        var artist = _db.Artists.Include(a => a.ArtistGenres).FirstOrDefault(a => a.ArtistID == artistId);
        if (artist == null) return NotFound();
        genreIds ??= Array.Empty<int>();
        string trimmedName = name?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ViewBag.Error = "Artist name is required.";
            artist.Name = trimmedName;
            PrepareArtistEditorView(genreIds, trimmedName);
            return View(artist);
        }
        if (genreIds.Length == 0)
        {
            ViewBag.Error = "At least one genre is required.";
            artist.Name = trimmedName;
            PrepareArtistEditorView(genreIds, trimmedName);
            return View(artist);
        }

        artist.Name = trimmedName;
        _db.ArtistGenres.RemoveRange(artist.ArtistGenres);
        foreach (var gid in genreIds)
            _db.ArtistGenres.Add(new ArtistGenre { ArtistID = artistId, GenreID = gid });
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Artist updated.";
        return RedirectToAction("Artists");
    }

    // =======================================================================
    // GENRE MANAGEMENT
    // =======================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddGenre(string name, string? returnAction, string? returnController, int? returnId)
    {
        var r = RequireManager(); if (r != null) return r;

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "Genre name is required.";
            return returnId.HasValue
                ? RedirectToAction(returnAction ?? "Songs", returnController ?? "Manager", new { id = returnId.Value })
                : RedirectToAction(returnAction ?? "Songs", returnController ?? "Manager");
        }

        if (!_db.Genres.Any(g => g.Name == name.Trim()))
        {
            _db.Genres.Add(new Genre { Name = name.Trim() });
            _db.SaveChanges();
            TempData["SuccessMessage"] = $"Genre \"{name.Trim()}\" added.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Genre \"{name.Trim()}\" already exists.";
        }

        return returnId.HasValue
            ? RedirectToAction(returnAction ?? "Songs", returnController ?? "Manager", new { id = returnId.Value })
            : RedirectToAction(returnAction ?? "Songs", returnController ?? "Manager");
    }

    // =======================================================================
    // PROMOTIONS
    // =======================================================================

    public IActionResult Promotions()
    {
        var r = RequireManager(); if (r != null) return r;

        ViewBag.FeaturedItems = _db.FeaturedItems
            .Include(f => f.Song).Include(f => f.Album).Include(f => f.Artist)
            .OrderByDescending(f => f.IsActive)
            .ThenByDescending(f => f.FeaturedItemID)
            .ToList();
        ViewBag.Discounts = _db.Discounts
            .Include(d => d.Song).Include(d => d.Album)
            .OrderByDescending(d => d.DiscountID).ToList();
        ViewBag.Songs   = _db.Songs.Where(s => s.IsActive).OrderBy(s => s.Title).ToList();
        ViewBag.Albums  = _db.Albums.Where(a => a.IsActive).OrderBy(a => a.Title).ToList();
        ViewBag.Artists = _db.Artists.OrderBy(a => a.Name).ToList();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetFeatured(int? songId, int? albumId, int? artistId)
    {
        var r = RequireManager(); if (r != null) return r;

        int selected = (songId.HasValue ? 1 : 0)
                     + (albumId.HasValue ? 1 : 0)
                     + (artistId.HasValue ? 1 : 0);
        if (selected != 1)
        {
            TempData["ErrorMessage"] = "Please select exactly one of Song, Album, or Artist to feature.";
            return RedirectToAction("Promotions");
        }

        var existing = FindFeaturedItem(songId, albumId, artistId);
        if (existing?.IsActive == true)
        {
            TempData["ErrorMessage"] = "That item is already featured.";
            return RedirectToAction("Promotions");
        }
        if (existing != null)
        {
            existing.IsActive = true;
            _db.SaveChanges();
            TempData["SuccessMessage"] = "Featured item re-enabled.";
            return RedirectToAction("Promotions");
        }

        var newFeatured = new FeaturedItem
        {
            SongID   = songId,
            AlbumID  = albumId,
            ArtistID = artistId,
            IsActive = true
        };
        _db.FeaturedItems.Add(newFeatured);
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Featured item added.";
        return RedirectToAction("Promotions");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleFeatured(int featuredItemId)
    {
        var r = RequireManager(); if (r != null) return r;
        var featured = _db.FeaturedItems.Find(featuredItemId);
        if (featured != null)
        {
            featured.IsActive = !featured.IsActive;
            _db.SaveChanges();
            TempData["SuccessMessage"] = featured.IsActive ? "Featured item enabled." : "Featured item disabled.";
        }
        return RedirectToAction("Promotions");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddDiscount(int? songId, int? albumId, decimal discountAmount)
    {
        var r = RequireManager(); if (r != null) return r;

        int selected = (songId.HasValue ? 1 : 0) + (albumId.HasValue ? 1 : 0);
        if (selected != 1)
        {
            TempData["ErrorMessage"] = "Please select exactly one song or album for the discount.";
            return RedirectToAction("Promotions");
        }
        if (discountAmount < 0.01m)
        {
            TempData["ErrorMessage"] = "Discount amount must be at least $0.01.";
            return RedirectToAction("Promotions");
        }
        var price = GetDiscountedItemPrice(songId, albumId);
        if (!price.HasValue)
        {
            TempData["ErrorMessage"] = "Selected item not found.";
            return RedirectToAction("Promotions");
        }
        if (discountAmount >= price.Value)
        {
            TempData["ErrorMessage"] = "Discount amount must be less than the item's price.";
            return RedirectToAction("Promotions");
        }

        // Only one active discount per item: supersede any existing active discount.
        foreach (var ex in _db.Discounts.Where(d => d.IsActive && d.SongID == songId && d.AlbumID == albumId))
            ex.IsActive = false;

        _db.Discounts.Add(new Discount
        {
            SongID         = songId,
            AlbumID        = albumId,
            DiscountAmount = discountAmount,
            IsActive       = true
        });
        _db.SaveChanges();

        TempData["SuccessMessage"] = "Discount added.";
        return RedirectToAction("Promotions");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditDiscount(int discountId, decimal discountAmount)
    {
        var r = RequireManager(); if (r != null) return r;
        if (discountAmount < 0.01m)
        {
            TempData["ErrorMessage"] = "Discount amount must be at least $0.01.";
            return RedirectToAction("Promotions");
        }
        var discount = _db.Discounts.Find(discountId);
        if (discount != null)
        {
            var price = GetDiscountedItemPrice(discount.SongID, discount.AlbumID);
            if (!price.HasValue)
            {
                TempData["ErrorMessage"] = "Discounted item not found.";
                return RedirectToAction("Promotions");
            }
            if (discountAmount >= price.Value)
            {
                TempData["ErrorMessage"] = "Discount amount must be less than the item's price.";
                return RedirectToAction("Promotions");
            }
            discount.DiscountAmount = discountAmount;
            _db.SaveChanges();
            TempData["SuccessMessage"] = "Discount updated.";
        }
        return RedirectToAction("Promotions");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleDiscount(int discountId)
    {
        var r = RequireManager(); if (r != null) return r;
        var discount = _db.Discounts.Find(discountId);
        if (discount != null)
        {
            discount.IsActive = !discount.IsActive;
            if (discount.IsActive)
            {
                // Only one active discount per item: supersede any other active discount.
                foreach (var other in _db.Discounts.Where(d =>
                             d.DiscountID != discount.DiscountID
                          && d.IsActive
                          && d.SongID == discount.SongID
                          && d.AlbumID == discount.AlbumID))
                    other.IsActive = false;
            }
            _db.SaveChanges();
            TempData["SuccessMessage"] = discount.IsActive ? "Discount enabled." : "Discount disabled.";
        }
        return RedirectToAction("Promotions");
    }

    // =======================================================================
    // REPORTS
    // =======================================================================

    public IActionResult ReportSongsSold()
    {
        var r = RequireManager(); if (r != null) return r;

        var soldStats = _db.OrderItems
            .Where(oi => oi.SongID != null
                      && oi.Order.Status == OrderStatus.Ordered
                      && !oi.Order.IsRefunded)
            .AsEnumerable()
            .GroupBy(oi => oi.SongID!.Value)
            .ToDictionary(g => g.Key, g => new { Count = g.Count(), Revenue = g.Sum(oi => oi.Price) });

        var data = _db.Songs
            .Include(s => s.Artist)
            .AsEnumerable()
            .Select(song =>
            {
                soldStats.TryGetValue(song.SongID, out var stats);
                return new
                {
                    Song = song,
                    Count = stats?.Count ?? 0,
                    Revenue = stats?.Revenue ?? 0m
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.Song.Title)
            .ToList();

        ViewBag.Data = data;
        return View();
    }

    public IActionResult ReportAlbumsSold()
    {
        var r = RequireManager(); if (r != null) return r;

        var soldStats = _db.OrderItems
            .Where(oi => oi.AlbumID != null
                      && oi.Order.Status == OrderStatus.Ordered
                      && !oi.Order.IsRefunded)
            .AsEnumerable()
            .GroupBy(oi => oi.AlbumID!.Value)
            .ToDictionary(g => g.Key, g => new { Count = g.Count(), Revenue = g.Sum(oi => oi.Price) });

        var data = _db.Albums
            .Include(a => a.AlbumArtists).ThenInclude(aa => aa.Artist)
            .AsEnumerable()
            .Select(album =>
            {
                soldStats.TryGetValue(album.AlbumID, out var stats);
                return new
                {
                    Album = album,
                    Count = stats?.Count ?? 0,
                    Revenue = stats?.Revenue ?? 0m
                };
            })
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.Album.Title)
            .ToList();

        ViewBag.Data = data;
        return View();
    }

    public IActionResult ReportTopBands()
    {
        var r = RequireManager(); if (r != null) return r;

        // Collect all sold order items
        var songItems = _db.OrderItems
            .Include(oi => oi.Song).ThenInclude(s => s!.Artist)
            .Include(oi => oi.Song).ThenInclude(s => s!.SongGenres).ThenInclude(sg => sg.Genre)
            .Where(oi => oi.SongID != null && oi.Order.Status == OrderStatus.Ordered && !oi.Order.IsRefunded)
            .ToList();

        var albumItems = _db.OrderItems
            .Include(oi => oi.Album).ThenInclude(a => a!.AlbumArtists).ThenInclude(aa => aa.Artist)
            .Include(oi => oi.Album).ThenInclude(a => a!.AlbumGenres).ThenInclude(ag => ag.Genre)
            .Where(oi => oi.AlbumID != null && oi.Order.Status == OrderStatus.Ordered && !oi.Order.IsRefunded)
            .ToList();

        var genres = _db.Genres.ToList();

        var results = genres.Select(genre =>
        {
            // Songs in this genre
            var genreSongItems = songItems
                .Where(oi => oi.Song!.SongGenres.Any(sg => sg.GenreID == genre.GenreID))
                .ToList();
            // Albums in this genre
            var genreAlbumItems = albumItems
                .Where(oi => oi.Album!.AlbumGenres.Any(ag => ag.GenreID == genre.GenreID))
                .ToList();

            if (!genreSongItems.Any() && !genreAlbumItems.Any()) return null;

            // Group by artist
            var artistStats = new Dictionary<int, (string Name, int SongCount, decimal SongRev, int AlbumCount, decimal AlbumRev)>();

            foreach (var oi in genreSongItems)
            {
                int aid = oi.Song!.ArtistID;
                string aname = oi.Song.Artist?.Name ?? "";
                if (!artistStats.ContainsKey(aid)) artistStats[aid] = (aname, 0, 0, 0, 0);
                var (n, sc, sr, ac, ar) = artistStats[aid];
                artistStats[aid] = (n, sc + 1, sr + oi.Price, ac, ar);
            }
            foreach (var oi in genreAlbumItems)
            {
                foreach (var aa in oi.Album!.AlbumArtists)
                {
                    int aid = aa.ArtistID;
                    string aname = aa.Artist?.Name ?? "";
                    if (!artistStats.ContainsKey(aid)) artistStats[aid] = (aname, 0, 0, 0, 0);
                    var (n, sc, sr, ac, ar) = artistStats[aid];
                    artistStats[aid] = (n, sc, sr, ac + 1, ar + oi.Price);
                }
            }

            var top = artistStats
                .OrderByDescending(kv => kv.Value.SongRev + kv.Value.AlbumRev)
                .First();

            var albumBreakdowns = genreAlbumItems
                .Where(oi => oi.Album!.AlbumArtists.Any(aa => aa.ArtistID == top.Key))
                .GroupBy(oi => oi.Album!)
                .Select(g => new
                {
                    AlbumTitle = g.Key.Title,
                    Count = g.Count(),
                    Revenue = g.Sum(oi => oi.Price)
                })
                .OrderByDescending(x => x.Revenue)
                .ThenBy(x => x.AlbumTitle)
                .ToList();

            return new
            {
                Genre        = genre.Name,
                ArtistName   = top.Value.Name,
                SongCount    = top.Value.SongCount,
                SongRevenue  = top.Value.SongRev,
                AlbumCount   = top.Value.AlbumCount,
                AlbumRevenue = top.Value.AlbumRev,
                AlbumBreakdowns = albumBreakdowns,
                TotalRevenue = top.Value.SongRev + top.Value.AlbumRev
            };
        })
        .Where(x => x != null)
        .OrderByDescending(x => x!.TotalRevenue)
        .ToList();

        ViewBag.Data = results;
        return View();
    }

    private void PrepareAlbumEditorView(int[]? selectedArtists = null, int[]? selectedGenres = null,
        int[]? selectedSongs = null, string? title = null, string? price = null,
        string? albumCoverURL = null, bool? isActive = null)
    {
        var artistIds = selectedArtists?.Distinct().ToArray() ?? Array.Empty<int>();

        ViewBag.Artists = _db.Artists.OrderBy(a => a.Name).ToList();
        ViewBag.Genres = _db.Genres.OrderBy(g => g.Name).ToList();
        ViewBag.SelectedArtists = artistIds;
        ViewBag.SelectedGenres = selectedGenres?.Distinct().ToArray() ?? Array.Empty<int>();
        ViewBag.SelectedSongs = selectedSongs?.Distinct().ToArray() ?? Array.Empty<int>();
        ViewBag.AvailableSongs = GetAvailableSongsForArtists(artistIds);
        ViewBag.TitleValue = title;
        ViewBag.PriceValue = price;
        ViewBag.AlbumCoverURLValue = albumCoverURL;
        ViewBag.IsActiveValue = isActive;
    }

    private void PrepareSongEditorView(int? selectedArtistId = null, int[]? selectedGenres = null,
        string? title = null, string? price = null, bool? isActive = null)
    {
        ViewBag.Artists = _db.Artists.OrderBy(a => a.Name).ToList();
        ViewBag.Genres = _db.Genres.OrderBy(g => g.Name).ToList();
        ViewBag.SelectedArtistId = selectedArtistId;
        ViewBag.SelectedGenres = selectedGenres?.Distinct().ToArray() ?? Array.Empty<int>();
        ViewBag.TitleValue = title;
        ViewBag.PriceValue = price;
        ViewBag.IsActiveValue = isActive;
    }

    private void PrepareArtistEditorView(int[]? selectedGenres = null, string? name = null)
    {
        ViewBag.Genres = _db.Genres.OrderBy(g => g.Name).ToList();
        ViewBag.SelectedGenres = selectedGenres?.Distinct().ToArray() ?? Array.Empty<int>();
        ViewBag.NameValue = name;
    }

    private void SetEmployeeFormViewBag(string? firstName, string? lastName, string? email,
        string? phoneNumber, string? street, string? city, string? state,
        string? zipCode, bool isManager)
    {
        ViewBag.FirstName = firstName;
        ViewBag.LastName = lastName;
        ViewBag.Email = email;
        ViewBag.PhoneNumber = phoneNumber;
        ViewBag.Street = street;
        ViewBag.City = city;
        ViewBag.State = state;
        ViewBag.ZipCode = zipCode;
        ViewBag.IsManagerValue = isManager;
    }

    private List<Song> GetAvailableSongsForArtists(IEnumerable<int> artistIds)
    {
        var ids = artistIds.Distinct().ToList();
        if (ids.Count == 0) return new List<Song>();

        return _db.Songs
            .Where(s => ids.Contains(s.ArtistID) && s.IsActive)
            .OrderBy(s => s.Title)
            .ToList();
    }

    private decimal? GetDiscountedItemPrice(int? songId, int? albumId)
    {
        if (songId.HasValue)
            return _db.Songs.Where(s => s.SongID == songId.Value).Select(s => (decimal?)s.Price).FirstOrDefault();
        if (albumId.HasValue)
            return _db.Albums.Where(a => a.AlbumID == albumId.Value).Select(a => (decimal?)a.Price).FirstOrDefault();
        return null;
    }

    private FeaturedItem? FindFeaturedItem(int? songId, int? albumId, int? artistId)
    {
        if (songId.HasValue)
            return _db.FeaturedItems.FirstOrDefault(f => f.SongID == songId.Value);
        if (albumId.HasValue)
            return _db.FeaturedItems.FirstOrDefault(f => f.AlbumID == albumId.Value);
        if (artistId.HasValue)
            return _db.FeaturedItems.FirstOrDefault(f => f.ArtistID == artistId.Value);
        return null;
    }
}
