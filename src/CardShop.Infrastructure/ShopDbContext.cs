using CardShop.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Infrastructure;

public class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Card>().HasIndex(c => new { c.TcgPlayerId, c.Condition, c.IsFoil }).IsUnique();

        // one snapshot per card per daily update; also speeds up history queries
        b.Entity<PriceSnapshot>().HasIndex(s => new { s.CardId, s.DataDate }).IsUnique();
        b.Entity<PriceSnapshot>().Property(s => s.Market).HasPrecision(10, 2);
        b.Entity<PriceSnapshot>().Property(s => s.Low).HasPrecision(10, 2);
        b.Entity<PriceSnapshot>().Property(s => s.Mid).HasPrecision(10, 2);
        b.Entity<PriceSnapshot>().Property(s => s.High).HasPrecision(10, 2);

        b.Entity<InventoryItem>().Property(i => i.YourPrice).HasPrecision(10, 2);
        b.Entity<InventoryItem>().Property(i => i.CostBasis).HasPrecision(10, 2);
    }
}