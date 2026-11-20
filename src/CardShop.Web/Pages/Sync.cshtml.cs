using CardShop.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardShop.Web.Pages;

public class SyncModel(PriceSyncService sync) : PageModel
{
    public SyncResult? Result { get; set; }
    public DateOnly DataDate => PriceSyncService.CurrentDataDate();

    // CancellationToken.None so closing the tab doesn't throw away a long catalog scan
    public async Task OnPostAsync() => Result = await sync.SyncAsync(allowFullScan: true, CancellationToken.None);
}