using CardShop.Core.Entities;
using CardShop.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Web.Pages;

public record CardTile(int CardId, string ProductId, string Name, string Game, string SetName,
    string Condition, bool IsFoil, int Qty, decimal YourPrice, decimal? Market);

public class CardsModel(ShopDbContext db) : PageModel
{
    const int PageSize = 48;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Game { get; set; }
    [BindProperty(SupportsGet = true)] public string? SetName { get; set; }
    [BindProperty(SupportsGet = true)] public string? Condition { get; set; }
    [BindProperty(SupportsGet = true)] public string? Foil { get; set; }
    [BindProperty(SupportsGet = true)] public bool InStock { get; set; }
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "name";
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public List<CardTile> Tiles { get; set; } = [];
    public List<string> Games { get; set; } = [];
    public List<string> Sets { get; set; } = [];
    public List<string> Conditions { get; set; } = [];
    public int Total { get; set; }
    public int UnitsInView { get; set; }
    public decimal ValueInView { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    sealed class Row
    {
        public InventoryItem Item { get; set; } = null!;
        public Card Card { get; set; } = null!;
        public decimal? Market { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var game = Game; var set = SetName; var cond = Condition; var foil = Foil; var inStock = InStock;

        IQueryable<Row> q =
            from i in db.Inventory
            join c in db.Cards on i.CardId equals c.Id
            select new Row
            {
                Item = i,
                Card = c,
                Market = db.PriceSnapshots.Where(s => s.CardId == c.Id)
                    .OrderByDescending(s => s.DataDate)
                    .Select(s => (decimal?)s.Market)
                    .FirstOrDefault()
            };

        if (!string.IsNullOrWhiteSpace(game)) q = q.Where(r => r.Card.Game == game);
        if (!string.IsNullOrWhiteSpace(set)) q = q.Where(r => r.Card.SetName == set);
        if (!string.IsNullOrWhiteSpace(cond)) q = q.Where(r => r.Card.Condition == cond);
        if (foil == "yes") q = q.Where(r => r.Card.IsFoil);
        else if (foil == "no") q = q.Where(r => !r.Card.IsFoil);
        if (inStock) q = q.Where(r => r.Item.Quantity > 0);

        foreach (var term in (Q ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = term;
            var pattern = "%" + t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(r => EF.Functions.ILike(r.Card.Name, pattern)
                          || EF.Functions.ILike(r.Card.SetName, pattern)
                          || r.Card.TcgPlayerId == t);
        }

        Total = await q.CountAsync(ct);
        PageNo = Math.Clamp(PageNo, 1, TotalPages);

        // totals for the summary line (two small columns per row)
        var totals = await q.Select(r => new { r.Item.Quantity, r.Market }).ToListAsync(ct);
        UnitsInView = totals.Sum(x => x.Quantity);
        ValueInView = totals.Sum(x => x.Quantity * (x.Market ?? 0m));

        var sorted = Sort switch
        {
            "price" => q.OrderByDescending(r => r.Market ?? 0m).ThenBy(r => r.Card.Name),
            "value" => q.OrderByDescending(r => r.Item.Quantity * (r.Market ?? 0m)).ThenBy(r => r.Card.Name),
            "qty"   => q.OrderByDescending(r => r.Item.Quantity).ThenBy(r => r.Card.Name),
            "set"   => q.OrderBy(r => r.Card.SetName).ThenBy(r => r.Card.Name),
            _       => q.OrderBy(r => r.Card.Name).ThenBy(r => r.Card.SetName)
        };

        var rows = await sorted.Skip((PageNo - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        Tiles = rows.Select(r => new CardTile(r.Card.Id, r.Card.TcgPlayerId, r.Card.Name, r.Card.Game,
            r.Card.SetName, r.Card.Condition, r.Card.IsFoil, r.Item.Quantity, r.Item.YourPrice, r.Market)).ToList();

        // dropdown options
        Games = await db.Cards.Select(c => c.Game).Distinct().OrderBy(x => x).ToListAsync(ct);
        Sets = await db.Cards.Where(c => string.IsNullOrEmpty(game) || c.Game == game)
            .Select(c => c.SetName).Distinct().OrderBy(x => x).ToListAsync(ct);
        Conditions = await db.Cards.Select(c => c.Condition).Distinct().OrderBy(x => x).ToListAsync(ct);
    }

    public static string? ImageUrl(string productId) =>
        long.TryParse(productId, out _) ? $"https://tcgplayer-cdn.tcgplayer.com/product/{productId}_200w.jpg" : null;

    public string PageUrl(int p)
    {
        var qs = new Dictionary<string, string?>
        {
            ["q"] = Q, ["game"] = Game, ["setName"] = SetName, ["condition"] = Condition,
            ["foil"] = Foil, ["inStock"] = InStock ? "true" : null, ["sort"] = Sort, ["p"] = p.ToString()
        };
        return QueryHelpers.AddQueryString("/Cards",
            qs.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value));
    }
}
