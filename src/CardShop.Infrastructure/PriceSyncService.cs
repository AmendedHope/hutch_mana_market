using System.Globalization;
using CardShop.Core.Abstractions;
using CardShop.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CardShop.Infrastructure;

public record SyncResult(int PricesSaved, int ChangesFound, List<string> Notes);

public class PriceSyncService(ShopDbContext db, IPriceProvider provider, INotifier notifier, IConfiguration config)
{
    // TCGCSV refreshes around 20:00 UTC. A new "data day" starts at 22:00 UTC to leave a buffer.
    public static DateOnly CurrentDataDate() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-22));

    decimal Setting(string key, decimal fallback) =>
        decimal.TryParse(config[key], NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public async Task<SyncResult> SyncAsync(bool allowFullScan, CancellationToken ct = default)
    {
        var notes = new List<string>();
        var dataDate = CurrentDataDate();

        var allCards = await db.Cards.ToListAsync(ct);
        var doneIds = (await db.PriceSnapshots.Where(s => s.DataDate == dataDate)
            .Select(s => s.CardId).ToListAsync(ct)).ToHashSet();
        var todo = allCards.Where(c => !doneIds.Contains(c.Id)).ToList();

        if (todo.Count == 0)
        {
            notes.Add($"Already up to date for {dataDate}.");
            return new SyncResult(0, 0, notes);
        }

        await ResolveGroupsAsync(todo, allowFullScan, notes, ct);

        var pctLimit = Setting("Alerts:Percent", 10m);
        var minDollars = Setting("Alerts:MinDollars", 0.50m);
        var changes = new List<PriceChange>();
        var saved = 0;

        var byGroup = todo.Where(c => c.CategoryId is not null && c.GroupId is not null)
                          .GroupBy(c => (Cat: c.CategoryId!.Value, Grp: c.GroupId!.Value));

        foreach (var group in byGroup)
        {
            IReadOnlyList<MarketPrice> prices;
            try { prices = await provider.GetGroupPricesAsync(group.Key.Cat, group.Key.Grp, ct); }
            catch (Exception ex) { notes.Add($"Price fetch failed for group {group.Key.Grp}: {ex.Message}"); continue; }

            var byProduct = prices.ToLookup(p => p.ProductId);

            foreach (var card in group)
            {
                if (!long.TryParse(card.TcgPlayerId, out var productId)) continue;

                var row = Pick(byProduct[productId].ToList(), card.IsFoil);
                if (row?.Market is not { } market)
                {
                    notes.Add($"No price found for {card.Name} ({card.SetName}, {(card.IsFoil ? "foil" : "non-foil")})");
                    continue;
                }

                var prev = await db.PriceSnapshots
                    .Where(s => s.CardId == card.Id)
                    .OrderByDescending(s => s.DataDate)
                    .FirstOrDefaultAsync(ct);

                db.PriceSnapshots.Add(new PriceSnapshot
                {
                    CardId = card.Id,
                    DataDate = dataDate,
                    SubType = row.SubType,
                    Market = market,
                    Low = row.Low,
                    Mid = row.Mid,
                    High = row.High,
                    CapturedAt = DateTime.UtcNow
                });
                saved++;

                if (prev is not null && prev.Market > 0)
                {
                    var diff = market - prev.Market;
                    var pct = diff / prev.Market * 100;
                    if (Math.Abs(pct) >= pctLimit && Math.Abs(diff) >= minDollars)
                        changes.Add(new PriceChange(card, prev.Market, market, pct));
                }
            }

            await Task.Delay(250, ct); // be polite to a free community service
        }

        await db.SaveChangesAsync(ct);

        if (changes.Count > 0 && !string.IsNullOrWhiteSpace(config["Discord:WebhookUrl"]))
        {
            try { await notifier.SendDigestAsync(changes, ct); }
            catch (Exception ex) { notes.Add($"Discord notification failed: {ex.Message}"); }
        }

        return new SyncResult(saved, changes.Count, notes);
    }

    static MarketPrice? Pick(List<MarketPrice> rows, bool foil)
    {
        if (rows.Count == 0) return null;
        static bool IsFoilType(MarketPrice r) => r.SubType.Contains("foil", StringComparison.OrdinalIgnoreCase);

        if (foil) return rows.FirstOrDefault(IsFoilType);
        return rows.FirstOrDefault(r => r.SubType.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            ?? rows.FirstOrDefault(r => !IsFoilType(r))
            ?? rows[0];
    }

    async Task ResolveGroupsAsync(List<Card> cards, bool allowFullScan, List<string> notes, CancellationToken ct)
    {
        var unresolved = cards.Where(c => c.GroupId is null).ToList();
        if (unresolved.Count == 0) return;

        var categories = await provider.GetCategoriesAsync(ct);

        foreach (var byGame in unresolved.GroupBy(c => c.Game))
        {
            var cat = categories.FirstOrDefault(c => Same(c.Name, byGame.Key) || Same(c.DisplayName, byGame.Key));
            if (cat is null)
            {
                notes.Add($"Couldn't match product line '{byGame.Key}' to a TCGplayer category.");
                continue;
            }

            var remaining = byGame.ToList();
            foreach (var c in remaining) c.CategoryId = cat.CategoryId;

            var groups = await provider.GetGroupsAsync(cat.CategoryId, ct);
            var wanted = remaining.Select(c => c.SetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ordered = groups.OrderByDescending(g => wanted.Contains(g.Name)).ToList();
            if (!allowFullScan) ordered = ordered.Where(g => wanted.Contains(g.Name)).ToList();

            foreach (var g in ordered)
            {
                if (remaining.Count == 0) break;
                var ids = await provider.GetProductIdsAsync(cat.CategoryId, g.GroupId, ct);
                foreach (var c in remaining.Where(c => long.TryParse(c.TcgPlayerId, out var id) && ids.Contains(id)))
                    c.GroupId = g.GroupId;
                remaining.RemoveAll(c => c.GroupId is not null);
                await Task.Delay(250, ct);
            }

            foreach (var c in remaining)
                notes.Add($"Couldn't find product {c.TcgPlayerId} ({c.Name}) in any {cat.Name} set.");
        }

        await db.SaveChangesAsync(ct);
    }

    static bool Same(string? a, string b) =>
        string.Equals(a?.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}