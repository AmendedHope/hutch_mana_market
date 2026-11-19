namespace CardShop.Core.Entities;

public class InventoryItem
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public int Quantity { get; set; }
    public decimal CostBasis { get; set; }
    public decimal YourPrice { get; set; }
}