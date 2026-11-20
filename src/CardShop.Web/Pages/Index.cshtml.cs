using CardShop.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Web.Pages;

public record TopCard(string ProductId, string Name, string SetName, int Qty, decimal Market, decimal Value);

public class IndexModel(ShopDbContext db) : PageModel
{
    public string Greeting { get; set; } = "Welcome back";
    public string DisplayName => User.Identity?.Name ?? "friend";

    public int Listings { get; set; }
    public int Units { get; set; }
    public decimal ListValue { get; set; }
    public decimal MarketValue { get; set; }
    public int Priced { get; set; }
    public int Underpriced { get; set; }
    public int Overpriced { get; set; }
    public DateOnly? LastPriceDate { get; set; }
    public List<TopCard> Top { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var hour = DateTime.Now.Hour;
        Greeting = hour < 5 ? "Burning the midnight mana" : hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";

        var rows = await (
            from i in db.Inventory
            join c in db.Cards on i.CardId equals c.Id
            select new
            {
                c.TcgPlayerId, c.Name, c.SetName, i.Quantity, i.YourPrice,
                Market = db.PriceSnapshots.Where(s => s.CardId == c.Id)
                    .OrderByDescending(s => s.DataDate)
                    .Select(s => (decimal?)s.Market)
                    .FirstOrDefault()
            }).ToListAsync(ct);

        Listings = rows.Count;
        Units = rows.Sum(r => r.Quantity);
        ListValue = rows.Sum(r => r.Quantity * r.YourPrice);
        MarketValue = rows.Sum(r => r.Quantity * (r.Market ?? 0m));

        var priced = rows.Where(r => r.Market is > 0).ToList();
        Priced = priced.Count;
        // 15% either side of market counts as worth a look
        Underpriced = priced.Count(r => r.YourPrice > 0 && r.YourPrice < r.Market!.Value * 0.85m);
        Overpriced = priced.Count(r => r.YourPrice > r.Market!.Value * 1.15m);

        Top = priced.Where(r => r.Quantity > 0)
            .Select(r => new TopCard(r.TcgPlayerId, r.Name, r.SetName, r.Quantity, r.Market!.Value, r.Quantity * r.Market!.Value))
            .OrderByDescending(t => t.Value).Take(6).ToList();

        LastPriceDate = await db.PriceSnapshots.MaxAsync(s => (DateOnly?)s.DataDate, ct);
    }
}
