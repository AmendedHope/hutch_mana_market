using CardShop.Infrastructure;

namespace CardShop.Web;

public class PricePollingWorker(IServiceScopeFactory scopes, ILogger<PricePollingWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), ct); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<PriceSyncService>();
                var result = await sync.SyncAsync(allowFullScan: false, ct);
                log.LogInformation("Price sync: {Saved} saved, {Changes} changes, {Notes} notes",
                    result.PricesSaved, result.ChangesFound, result.Notes.Count);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { log.LogError(ex, "Price sync failed"); }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }
}