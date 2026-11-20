using CardShop.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardShop.Web.Pages;

public class ImportModel(InventoryImporter importer) : PageModel
{
    [BindProperty] public IFormFile? Upload { get; set; }
    public ImportResult? Result { get; set; }

    public async Task OnPostAsync(CancellationToken ct)
    {
        if (Upload is null || Upload.Length == 0) return;
        await using var stream = Upload.OpenReadStream();
        Result = await importer.ImportAsync(stream, Upload.FileName, ct);
    }
}