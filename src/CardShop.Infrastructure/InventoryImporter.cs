using System.Globalization;
using CardShop.Core.Entities;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Infrastructure;

public record ImportResult(int CardsAdded, int InventoryUpdated, List<string> Errors);

public class InventoryImporter(ShopDbContext db)
{
    // Add your export's header names to these lists (matching ignores case)
    static readonly string[] IdCols    = ["TCGplayer Id", "TcgPlayerId", "Product ID"];
    static readonly string[] NameCols  = ["Product Name", "Name"];
    static readonly string[] GameCols  = ["Product Line", "Game"];
    static readonly string[] SetCols   = ["Set Name", "Set"];
    static readonly string[] CondCols  = ["Condition"];
    static readonly string[] QtyCols   = ["Total Quantity", "Quantity", "Qty"];
    static readonly string[] PriceCols = ["TCG Marketplace Price", "Price", "Your Price"];
    static readonly string[] CostCols  = ["Cost", "Cost Basis"];

    public async Task<ImportResult> ImportAsync(Stream file, string fileName, CancellationToken ct = default)
    {
        var errors = new List<string>();
        int added = 0, updated = 0;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        List<string> headers;
        List<Dictionary<string, string>> rows;

        try
        {
            (headers, rows) = ext switch
            {
                ".xlsx" => ReadXlsx(file),
                ".csv" or ".txt" => ReadCsv(file),
                _ => throw new InvalidDataException($"Unsupported file type '{ext}'. Upload a .csv or .xlsx file.")
            };
        }
        catch (Exception ex)
        {
            return new ImportResult(0, 0, [$"Could not read file: {ex.Message}"]);
        }

        var cards = await db.Cards.ToDictionaryAsync(c => (c.TcgPlayerId, c.Condition, c.IsFoil), ct);
        var items = new Dictionary<Card, InventoryItem>();
        foreach (var i in await db.Inventory.Include(i => i.Card).ToListAsync(ct))
            items[i.Card!] = i;

        static string? Get(Dictionary<string, string> r, string[] names)
        {
            foreach (var n in names)
                if (r.TryGetValue(n, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            return null;
        }

        var rowNum = 1; // header is row 1
        foreach (var r in rows)
        {
            rowNum++;
            if (r.Values.All(string.IsNullOrWhiteSpace)) continue; // blank line

            var id = Get(r, IdCols);
            if (id is null) { errors.Add($"Row {rowNum}: no product ID, skipped"); continue; }

            var condition = Get(r, CondCols) ?? "Near Mint";
            var isFoil = condition.Contains("foil", StringComparison.OrdinalIgnoreCase);
            var key = (id, condition, isFoil);

            if (!cards.TryGetValue(key, out var card))
            {
                card = new Card
                {
                    TcgPlayerId = id,
                    Name = Get(r, NameCols) ?? "(unknown)",
                    Game = Get(r, GameCols) ?? "",
                    SetName = Get(r, SetCols) ?? "",
                    Condition = condition,
                    IsFoil = isFoil
                };
                db.Cards.Add(card);
                cards[key] = card;
                added++;
            }

            if (!items.TryGetValue(card, out var item))
            {
                item = new InventoryItem { Card = card };
                db.Inventory.Add(item);
                items[card] = item;
            }
            else updated++;

            item.Quantity  = int.TryParse(Get(r, QtyCols), NumberStyles.Any, CultureInfo.InvariantCulture, out var q) ? q : 0;
            item.YourPrice = decimal.TryParse(Get(r, PriceCols)?.TrimStart('$'), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0;
            item.CostBasis = decimal.TryParse(Get(r, CostCols)?.TrimStart('$'), NumberStyles.Any, CultureInfo.InvariantCulture, out var c) ? c : 0;
        }

        await db.SaveChangesAsync(ct);

        if (added + updated == 0)
            errors.Insert(0, $"Nothing imported. Headers found: {string.Join(" | ", headers)}");

        return new ImportResult(added, updated, errors);
    }

    static (List<string>, List<Dictionary<string, string>>) ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,
            MissingFieldFound = null,
            BadDataFound = null
        });

        var headers = new List<string>();
        var rows = new List<Dictionary<string, string>>();
        if (!csv.Read()) return (headers, rows);

        csv.ReadHeader();
        headers = (csv.HeaderRecord ?? []).Select(h => h.Trim()).ToList();

        while (csv.Read())
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
                d[headers[i]] = csv.GetField(i) ?? "";
            rows.Add(d);
        }
        return (headers, rows);
    }

    static (List<string>, List<Dictionary<string, string>>) ReadXlsx(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.First();

        var headers = new List<string>();
        var rows = new List<Dictionary<string, string>>();

        var used = ws.RangeUsed();
        if (used is null) return (headers, rows);

        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        for (var c = firstCol; c <= lastCol; c++)
            headers.Add(ws.Cell(firstRow, c).GetString().Trim());

        for (var r = firstRow + 1; r <= lastRow; r++)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = firstCol; c <= lastCol; c++)
                d[headers[c - firstCol]] = ws.Cell(r, c).GetString();
            rows.Add(d);
        }
        return (headers, rows);
    }
}