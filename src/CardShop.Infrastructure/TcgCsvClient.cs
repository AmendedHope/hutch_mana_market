using System.Net.Http.Json;
using CardShop.Core.Abstractions;

namespace CardShop.Infrastructure;

public class TcgCsvClient(HttpClient http) : IPriceProvider
{
    async Task<List<T>> GetAsync<T>(string path, CancellationToken ct)
    {
        var env = await http.GetFromJsonAsync<TcgCsvEnvelope<T>>(path, ct);
        return env?.Results ?? [];
    }

    public async Task<IReadOnlyList<CatalogCategory>> GetCategoriesAsync(CancellationToken ct) =>
        (await GetAsync<TcgCsvCategory>("categories", ct))
            .Select(c => new CatalogCategory(c.CategoryId, c.Name ?? "", c.DisplayName ?? "")).ToList();

    public async Task<IReadOnlyList<CatalogGroup>> GetGroupsAsync(int categoryId, CancellationToken ct) =>
        (await GetAsync<TcgCsvGroup>($"{categoryId}/groups", ct))
            .Select(g => new CatalogGroup(g.GroupId, g.Name ?? "")).ToList();

    public async Task<HashSet<long>> GetProductIdsAsync(int categoryId, int groupId, CancellationToken ct) =>
        (await GetAsync<TcgCsvProduct>($"{categoryId}/{groupId}/products", ct))
            .Select(p => p.ProductId).ToHashSet();

    public async Task<IReadOnlyList<MarketPrice>> GetGroupPricesAsync(int categoryId, int groupId, CancellationToken ct) =>
        (await GetAsync<TcgCsvPrice>($"{categoryId}/{groupId}/prices", ct))
            .Select(p => new MarketPrice(p.ProductId, p.SubTypeName ?? "Normal",
                p.MarketPrice, p.LowPrice, p.MidPrice, p.HighPrice)).ToList();
}

internal class TcgCsvEnvelope<T> { public List<T> Results { get; set; } = []; }
internal class TcgCsvCategory { public int CategoryId { get; set; } public string? Name { get; set; } public string? DisplayName { get; set; } }
internal class TcgCsvGroup { public int GroupId { get; set; } public string? Name { get; set; } }
internal class TcgCsvProduct { public long ProductId { get; set; } }
internal class TcgCsvPrice
{
    public long ProductId { get; set; }
    public decimal? LowPrice { get; set; }
    public decimal? MidPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? MarketPrice { get; set; }
    public string? SubTypeName { get; set; }
}