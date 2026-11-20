using System.Globalization;
using System.Text;
using CardShop.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Web.Pages;

public enum PriceStatus { Ok, Under, Over, NoMarket }

public record AnalyticsRow(
    int ItemId, string ProductId, string Name, string Game, string SetName, string Condition, bool IsFoil,
    int Qty, decimal YourPrice, decimal? UnitCost, decimal? Market, decimal? Low,
    PriceStatus Status, decimal? DiffPct, decimal? Suggested)
{
    public decimal ChangePerUnit => Suggested is { } s ? s - YourPrice : 0;
    public decimal Impact => ChangePerUnit * Qty;
    public decimal? CostTotal => UnitCost * Qty;
    public decimal? ValueTotal => Market * Qty;
    public decimal? Gain => ValueTotal - CostTotal;
}

public record GameSummary(string Game, int Cards, int Units, decimal Cost, decimal Value, decimal Gain, decimal? Roi);

public class AnalyticsModel(ShopDbContext db) : PageModel
{
    const int MaxRepriceRows = 300;

    // ----- filters -----
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Game { get; set; }
    [BindProperty(SupportsGet = true)] public string? SetName { get; set; }
    [BindProperty(SupportsGet = true)] public string? Condition { get; set; }
    [BindProperty(SupportsGet = true)] public string? Foil { get; set; }
    [BindProperty(SupportsGet = true)] public string Stock { get; set; } = "in";      // in | all

    // ----- repricing settings -----
    [BindProperty(SupportsGet = true)] public decimal Target { get; set; } = 100;      // % of market to price at
    [BindProperty(SupportsGet = true)] public decimal Threshold { get; set; } = 15;    // % away from market that counts as mispriced
    [BindProperty(SupportsGet = true)] public string Round { get; set; } = "cents";    // cents | nickel | 99
    [BindProperty(SupportsGet = true)] public decimal MinPrice { get; set; } = 0.25m;
    [BindProperty(SupportsGet = true)] public string Floor { get; set; } = "cost";     // cost | none
    [BindProperty(SupportsGet = true)] public decimal MinMargin { get; set; } = 0;     // % above cost when Floor = cost
    [BindProperty(SupportsGet = true)] public string Show { get; set; } = "both";      // both | under | over

    // ----- profit settings -----
    [BindProperty(SupportsGet = true)] public string CostMode { get; set; } = "unit";  // unit | row
    [BindProperty(SupportsGet = true)] public decimal FeePct { get; set; } = 0;

    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "reprice";    // reprice | profit

    // ----- results -----
    public List<string> Games { get; set; } = [];
    public List<string> Sets { get; set; } = [];
    public List<string> Conditions { get; set; } = [];

    public int CardCount { get; set; }
    public int PricedCount { get; set; }
    public decimal MarketValue { get; set; }
    public decimal ListValue { get; set; }
    public int UnderCount { get; set; }
    public int OverCount { get; set; }
    public decimal LeftOnTable { get; set; }
    public List<AnalyticsRow> Reprice { get; set; } = [];
    public int RepriceTotal { get; set; }

    public int WithCostCount { get; set; }
    public int MissingCostCount { get; set; }
    public decimal CostTotal { get; set; }
    public decimal CostedValue { get; set; }
    public decimal Gain { get; set; }
    public decimal NetIfSold { get; set; }
    public decimal? Roi { get; set; }
    public List<GameSummary> ByGame { get; set; } = [];
    public List<AnalyticsRow> Winners { get; set; } = [];
    public List<AnalyticsRow> Losers { get; set; } = [];

    [TempData] public string? Flash { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Clamp();
        var game = Game; var set = SetName; var cond = Condition;

        var q = from i in db.Inventory
                join c in db.Cards on i.CardId equals c.Id
                select new
                {
                    i.Id, i.Quantity, i.YourPrice, i.CostBasis,
                    c.TcgPlayerId, c.Name, c.Game, c.SetName, c.Condition, c.IsFoil,
                    Snap = db.PriceSnapshots.Where(s => s.CardId == c.Id)
                        .OrderByDescending(s => s.DataDate)
                        .Select(s => new { s.Market, s.Low })
                        .FirstOrDefault()
                };

        if (!string.IsNullOrWhiteSpace(game)) q = q.Where(r => r.Game == game);
        if (!string.IsNullOrWhiteSpace(set)) q = q.Where(r => r.SetName == set);
        if (!string.IsNullOrWhiteSpace(cond)) q = q.Where(r => r.Condition == cond);
        if (Foil == "yes") q = q.Where(r => r.IsFoil);
        else if (Foil == "no") q = q.Where(r => !r.IsFoil);
        if (Stock != "all") q = q.Where(r => r.Quantity > 0);
        foreach (var term in (Q ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = term;
            var pattern = "%" + t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(r => EF.Functions.ILike(r.Name, pattern) || EF.Functions.ILike(r.SetName, pattern) || r.TcgPlayerId == t);
        }

        var raw = await q.ToListAsync(ct);

        var rows = raw.Select(r =>
        {
            decimal? unitCost = r.CostBasis <= 0 ? null
                : CostMode == "row" && r.Quantity > 0 ? r.CostBasis / r.Quantity : r.CostBasis;
            decimal? market = r.Snap is { Market: > 0 } s ? s.Market : null;

            var status = PriceStatus.NoMarket;
            decimal? diff = null, suggested = null;
            if (market is { } m)
            {
                diff = r.YourPrice > 0 ? (r.YourPrice - m) / m * 100 : -100;
                status = r.YourPrice < m * (1 - Threshold / 100) ? PriceStatus.Under
                       : r.YourPrice > m * (1 + Threshold / 100) ? PriceStatus.Over
                       : PriceStatus.Ok;
                suggested = SuggestPrice(m, unitCost);
            }
            return new AnalyticsRow(r.Id, r.TcgPlayerId, r.Name, r.Game, r.SetName, r.Condition, r.IsFoil,
                r.Quantity, r.YourPrice, unitCost, market, r.Snap?.Low, status, diff, suggested);
        }).ToList();

        // ----- headline numbers -----
        CardCount = rows.Count;
        PricedCount = rows.Count(r => r.Market is not null);
        MarketValue = rows.Sum(r => r.ValueTotal ?? 0);
        ListValue = rows.Sum(r => r.YourPrice * r.Qty);
        UnderCount = rows.Count(r => r.Status == PriceStatus.Under);
        OverCount = rows.Count(r => r.Status == PriceStatus.Over);
        LeftOnTable = rows.Where(r => r.Status == PriceStatus.Under).Sum(r => Math.Max(0, r.Impact));

        // ----- repricing list -----
        var flagged = rows.Where(r => Show switch
        {
            "under" => r.Status == PriceStatus.Under,
            "over" => r.Status == PriceStatus.Over,
            _ => r.Status is PriceStatus.Under or PriceStatus.Over
        }).Where(r => r.Suggested is not null && r.Suggested != r.YourPrice)
          .OrderByDescending(r => Math.Abs(r.Impact)).ThenBy(r => r.Name).ToList();
        RepriceTotal = flagged.Count;
        Reprice = flagged.Take(MaxRepriceRows).ToList();

        // ----- profit & margin (only cards with both a cost and a market price) -----
        var costed = rows.Where(r => r.UnitCost is not null && r.Market is not null && r.Qty > 0).ToList();
        WithCostCount = costed.Count;
        MissingCostCount = rows.Count(r => r.UnitCost is null && r.Qty > 0);
        CostTotal = costed.Sum(r => r.CostTotal!.Value);
        CostedValue = costed.Sum(r => r.ValueTotal!.Value);
        Gain = CostedValue - CostTotal;
        NetIfSold = CostedValue * (1 - FeePct / 100) - CostTotal;
        Roi = CostTotal > 0 ? NetIfSold / CostTotal * 100 : null;

        ByGame = costed.GroupBy(r => string.IsNullOrWhiteSpace(r.Game) ? "(no game)" : r.Game)
            .Select(g =>
            {
                var cost = g.Sum(r => r.CostTotal!.Value);
                var value = g.Sum(r => r.ValueTotal!.Value);
                var net = value * (1 - FeePct / 100) - cost;
                return new GameSummary(g.Key, g.Count(), g.Sum(r => r.Qty), cost, value, net, cost > 0 ? net / cost * 100 : null);
            })
            .OrderByDescending(g => g.Value).ToList();

        Winners = costed.Where(r => r.Gain > 0).OrderByDescending(r => r.Gain).Take(10).ToList();
        Losers = costed.Where(r => r.Gain < 0).OrderBy(r => r.Gain).Take(10).ToList();

        // ----- dropdowns -----
        Games = await db.Cards.Select(c => c.Game).Distinct().OrderBy(x => x).ToListAsync(ct);
        Sets = await db.Cards.Where(c => string.IsNullOrEmpty(game) || c.Game == game)
            .Select(c => c.SetName).Distinct().OrderBy(x => x).ToListAsync(ct);
        Conditions = await db.Cards.Select(c => c.Condition).Distinct().OrderBy(x => x).ToListAsync(ct);
    }

    // Save the edited prices of the ticked rows to your inventory.
    public async Task<IActionResult> OnPostApplyAsync(List<int> selected, Dictionary<int, string> price, string? returnQuery, CancellationToken ct)
    {
        var updates = ParseSelection(selected, price);
        if (updates.Count == 0)
        {
            Flash = "Nothing was selected, so no prices changed.";
            return Redirect(BackUrl(returnQuery));
        }

        var ids = updates.Keys.ToList();
        var items = await db.Inventory.Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        foreach (var item in items) item.YourPrice = updates[item.Id];
        await db.SaveChangesAsync(ct);

        Flash = $"Updated {items.Count:N0} price{(items.Count == 1 ? "" : "s")} in your inventory. " +
                "Remember to update them on TCGplayer too (Download CSV).";
        return Redirect(BackUrl(returnQuery));
    }

    // Download the ticked rows, with the edited prices, as a CSV in TCGplayer's pricing-file layout.
    public async Task<IActionResult> OnPostExportAsync(List<int> selected, Dictionary<int, string> price, string? returnQuery, CancellationToken ct)
    {
        var updates = ParseSelection(selected, price);
        if (updates.Count == 0)
        {
            Flash = "Tick at least one card to export.";
            return Redirect(BackUrl(returnQuery));
        }

        var ids = updates.Keys.ToList();
        var rows = await (from i in db.Inventory
                          join c in db.Cards on i.CardId equals c.Id
                          where ids.Contains(i.Id)
                          orderby c.Name
                          select new
                          {
                              i.Id, i.Quantity, c.TcgPlayerId, c.Game, c.SetName, c.Name, c.Condition,
                              Market = db.PriceSnapshots.Where(s => s.CardId == c.Id).OrderByDescending(s => s.DataDate)
                                  .Select(s => (decimal?)s.Market).FirstOrDefault()
                          }).ToListAsync(ct);

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("TCGplayer Id,Product Line,Set Name,Product Name,Condition,Total Quantity,TCG Market Price,TCG Marketplace Price");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(r.TcgPlayerId), Csv(r.Game), Csv(r.SetName), Csv(r.Name), Csv(r.Condition),
                r.Quantity.ToString(inv), r.Market?.ToString("0.00", inv) ?? "", updates[r.Id].ToString("0.00", inv)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"price-update-{DateTime.Now:yyyy-MM-dd}.csv");
    }

    // ---------- helpers ----------

    decimal SuggestPrice(decimal market, decimal? unitCost)
    {
        var floor = MinPrice;
        if (Floor == "cost" && unitCost is { } uc) floor = Math.Max(floor, uc * (1 + MinMargin / 100));

        var p = RoundPrice(market * Target / 100, up: false);
        if (p < floor) p = RoundPrice(floor, up: true);
        return p;
    }

    decimal RoundPrice(decimal x, bool up)
    {
        if (x <= 0) return 0;
        switch (Round)
        {
            case "nickel":
                return (up ? Math.Ceiling(x * 20) : Math.Round(x * 20, MidpointRounding.AwayFromZero)) / 20;
            case "99":
                if (x < 1) goto case "nickel";
                var whole = Math.Floor(x);
                var candidate = whole + 0.99m;                       // e.g. 4.99
                if (up) return candidate >= x ? candidate : candidate + 1;
                return x - whole >= 0.5m || whole < 1 ? candidate : whole - 0.01m;   // nearest .99
            default:
                return up ? Math.Ceiling(x * 100) / 100 : Math.Round(x, 2, MidpointRounding.AwayFromZero);
        }
    }

    static Dictionary<int, decimal> ParseSelection(List<int> selected, Dictionary<int, string> price)
    {
        var result = new Dictionary<int, decimal>();
        foreach (var id in selected.Distinct())
        {
            if (!price.TryGetValue(id, out var text)) continue;
            if (!decimal.TryParse(text?.Trim().TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out var p)) continue;
            if (p < 0 || p > 100_000) continue;
            result[id] = Math.Round(p, 2, MidpointRounding.AwayFromZero);
        }
        return result;
    }

    static string Csv(string? s)
    {
        s ??= "";
        // neutralise spreadsheet formulas, then quote if needed
        if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
        return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    static string BackUrl(string? returnQuery) =>
        "/Analytics" + (!string.IsNullOrEmpty(returnQuery) && returnQuery.StartsWith('?') && !returnQuery.Contains("//") ? returnQuery : "");

    void Clamp()
    {
        Target = Math.Clamp(Target, 50, 200);
        Threshold = Math.Clamp(Threshold, 1, 90);
        MinPrice = Math.Clamp(MinPrice, 0, 10_000);
        MinMargin = Math.Clamp(MinMargin, 0, 500);
        FeePct = Math.Clamp(FeePct, 0, 50);
        if (Round is not ("cents" or "nickel" or "99")) Round = "cents";
        if (Floor is not ("cost" or "none")) Floor = "cost";
        if (Show is not ("both" or "under" or "over")) Show = "both";
        if (CostMode is not ("unit" or "row")) CostMode = "unit";
        if (Stock is not ("in" or "all")) Stock = "in";
        if (Tab is not ("reprice" or "profit")) Tab = "reprice";
    }
}
