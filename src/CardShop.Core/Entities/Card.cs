namespace CardShop.Core.Entities;

public class Card
{
    public int Id { get; set; }
    public string TcgPlayerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Game { get; set; } = "";
    public string SetName { get; set; } = "";
    public string Condition { get; set; } = "NM";
    public bool IsFoil { get; set; }
    public int? CategoryId { get; set; }   // filled in by the first sync
    public int? GroupId { get; set; }      // filled in by the first sync
}