namespace CardShop.Core.Entities;

public class PriceSnapshot
{
    public long Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public DateOnly DataDate { get; set; }   // which daily TCGCSV update this came from
    public string SubType { get; set; } = "";
    public decimal Market { get; set; }
    public decimal? Low { get; set; }
    public decimal? Mid { get; set; }
    public decimal? High { get; set; }
    public DateTime CapturedAt { get; set; }
}