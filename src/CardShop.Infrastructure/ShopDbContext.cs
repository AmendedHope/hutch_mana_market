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
        b.Entity<Card>().HasIndex(c => c.TcgPlayerId).IsUnique();

        // This index makes "latest price per card" and history queries fast
        b.Entity<PriceSnapshot>().HasIndex(s => new { s.CardId, s.CapturedAt });

        b.Entity<PriceSnapshot>().Property(s => s.Market).HasPrecision(10, 2);
        b.Entity<InventoryItem>().Property(i => i.YourPrice).HasPrecision(10, 2);
        b.Entity<InventoryItem>().Property(i => i.CostBasis).HasPrecision(10, 2);
    }
}