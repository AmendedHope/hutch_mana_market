using System.Security.Claims;
using CardShop.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CardShop.Web.Pages;

[AllowAnonymous]
public class LoginModel(IConfiguration config, ILogger<LoginModel> log) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty(SupportsGet = true)] public bool TooMany { get; set; }

    public string? Error { get; set; }
    public bool Configured => OwnerAccount.IsConfigured(config);

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(SafeReturnUrl());
        if (TooMany) Error = "Too many sign-in attempts. Wait a few minutes, then try again.";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Configured) return Page();

        if (!OwnerAccount.Verify(config, Username, Password))
        {
            log.LogWarning("Failed sign-in from {Ip}", HttpContext.Connection.RemoteIpAddress);
            Error = "That username and password don't match.";
            Password = "";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, config["Auth:Username"]!.Trim()),
            new(OwnerAccount.StampClaim, OwnerAccount.Stamp(config))
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null
            });

        log.LogInformation("Owner signed in from {Ip}", HttpContext.Connection.RemoteIpAddress);
        return LocalRedirect(SafeReturnUrl());
    }

    // Only ever send people back to a page on this site, never to an outside link.
    string SafeReturnUrl() => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
}
