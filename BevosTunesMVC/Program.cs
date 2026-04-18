using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.DAL;
using BevosTunesMVC.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<EmailSender>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing SQL connection string. Copy BevosTunesMVC/appsettings.Development.json.example " +
        "to BevosTunesMVC/appsettings.Development.json and set ConnectionStrings:DefaultConnection, " +
        "or set environment variable ConnectionStrings__DefaultConnection.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// Seed the database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Genres.Any())
    {
        DbSeeder.SeedGenres(db);
        DbSeeder.SeedArtists(db);
        DbSeeder.SeedArtistGenres(db);
        DbSeeder.SeedAlbums(db);
        DbSeeder.SeedAlbumArtists(db);
        DbSeeder.SeedAlbumGenres(db);
        DbSeeder.SeedSongs(db);
        DbSeeder.SeedSongGenres(db);
        DbSeeder.SeedAlbumSongs(db);
        DbSeeder.SeedCustomers(db);
        DbSeeder.SeedEmployees(db);
        DbSeeder.SeedCreditCards(db);
        DbSeeder.SeedOrders(db);        // also seeds OrderItems (combined in the generated seeder)
        DbSeeder.SeedCartItems(db);     // seeds CartItems from InCart orders in the Excel data
        DbSeeder.SeedSongReviews(db);   // also seeds AlbumReviews (combined in the generated seeder)
        DbSeeder.SeedFeaturedItems(db);
        DbSeeder.SeedDiscounts(db);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
