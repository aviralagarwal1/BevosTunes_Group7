using Microsoft.EntityFrameworkCore;
using BevosTunesMVC.Models;

namespace BevosTunesMVC.DAL;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Genre> Genres { get; set; }
    public DbSet<Artist> Artists { get; set; }
    public DbSet<ArtistGenre> ArtistGenres { get; set; }
    public DbSet<Song> Songs { get; set; }
    public DbSet<SongGenre> SongGenres { get; set; }
    public DbSet<Album> Albums { get; set; }
    public DbSet<AlbumGenre> AlbumGenres { get; set; }
    public DbSet<AlbumArtist> AlbumArtists { get; set; }
    public DbSet<AlbumSong> AlbumSongs { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<CreditCard> CreditCards { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<SongReview> SongReviews { get; set; }
    public DbSet<AlbumReview> AlbumReviews { get; set; }
    public DbSet<ArtistReview> ArtistReviews { get; set; }
    public DbSet<Discount> Discounts { get; set; }
    public DbSet<FeaturedItem> FeaturedItems { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
}
