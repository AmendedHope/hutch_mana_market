namespace CardShop.Core.Entities;

public class PriceSnapshot
{
    public long Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public decimal Market { get; set; }
    public decimal? Low { get; set; }
    public DateTime CapturedAt { get; set; }
}