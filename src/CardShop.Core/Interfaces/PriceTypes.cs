using CardShop.Core.Entities;

namespace CardShop.Core.Abstractions;

public record MarketPrice(long ProductId, string SubType, decimal? Market, decimal? Low, decimal? Mid, decimal? High);
public record PriceChange(Card Card, decimal OldPrice, decimal NewPrice, decimal Percent);
public record CatalogCategory(int CategoryId, string Name, string DisplayName);
public record CatalogGroup(int GroupId, string Name);

public interface IPriceProvider
{
    Task<IReadOnlyList<CatalogCategory>> GetCategoriesAsync(CancellationToken ct);
    Task<IReadOnlyList<CatalogGroup>> GetGroupsAsync(int categoryId, CancellationToken ct);
    Task<HashSet<long>> GetProductIdsAsync(int categoryId, int groupId, CancellationToken ct);
    Task<IReadOnlyList<MarketPrice>> GetGroupPricesAsync(int categoryId, int groupId, CancellationToken ct);
}

public interface INotifier
{
    Task SendDigestAsync(IReadOnlyList<PriceChange> changes, CancellationToken ct);
}