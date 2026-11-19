namespace CardShop.Core.Entities;

public class AlertRule
{
    public int Id { get; set; }
    public int? CardId { get; set; }
    public decimal PercentThreshold { get; set; } = 10;
    public decimal? MinDollarChange { get; set; }
}