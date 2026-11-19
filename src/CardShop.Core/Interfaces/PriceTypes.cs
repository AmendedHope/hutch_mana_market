using CardShop.Core.Entities;

namespace CardShop.Core.Abstractions;

public record MarketPrice(string TcgPlayerId, decimal Market, decimal? Low, DateTime SourceUpdatedAt);
public record PriceChange(Card Card, decimal OldPrice, decimal NewPrice, decimal Percent);

public interface IPriceProvider
{
    Task<IReadOnlyList<MarketPrice>> GetPricesAsync(IEnumerable<string> ids, CancellationToken ct);
}

public interface INotifier
{
    Task SendDigestAsync(IReadOnlyList<PriceChange> changes, CancellationToken ct);
}