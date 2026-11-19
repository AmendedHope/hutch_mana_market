using System.Net.Http.Json;
using CardShop.Core.Abstractions;
using Microsoft.Extensions.Configuration;

namespace CardShop.Infrastructure;
public class DiscordNotifier(HttpClient http, IConfiguration config) : INotifier
{
    public async Task SendDigestAsync(IReadOnlyList<PriceChange> changes, CancellationToken ct)
    {
        var lines = changes
            .OrderByDescending(c => Math.Abs(c.Percent))
            .Take(15) // Discord messages cap at 2000 characters
            .Select(c => $"{(c.Percent > 0 ? "📈" : "📉")} **{c.Card.Name}** ({c.Card.SetName}) " +
                         $"${c.OldPrice:F2} → ${c.NewPrice:F2} ({c.Percent:+0.#;-0.#}%)");

        var content = $"**{changes.Count} price changes**\n" + string.Join("\n", lines);
        var res = await http.PostAsJsonAsync(config["Discord:WebhookUrl"], new { content }, ct);
        res.EnsureSuccessStatusCode();
    }
}