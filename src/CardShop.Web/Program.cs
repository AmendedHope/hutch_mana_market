using System.Threading.RateLimiting;
using CardShop.Core.Abstractions;
using CardShop.Infrastructure;
using CardShop.Web;
using CardShop.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

// dotnet run --project src/CardShop.Web -- set-password
if (args.Length > 0 && args[0] == "set-password")
{
    Environment.ExitCode = OwnerAccount.RunSetup();
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShopDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<InventoryImporter>();

builder.Services.AddHttpClient<IPriceProvider, TcgCsvClient>(c =>
{
    c.BaseAddress = new Uri("https://tcgcsv.com/tcgplayer/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("HutchManaMarket/1.0 (shop price tracker)");
    c.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<INotifier, DiscordNotifier>();
builder.Services.AddScoped<PriceSyncService>();
builder.Services.AddHostedService<PricePollingWorker>();

// ----- Sign-in -----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.LogoutPath = "/Logout";
        o.AccessDeniedPath = "/Login";
        o.Cookie.Name = "mana.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;

        // If the password was changed (or the account removed), old cookies stop working.
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var config = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var stamp = ctx.Principal?.FindFirst(OwnerAccount.StampClaim)?.Value;
            if (!OwnerAccount.IsConfigured(config) || stamp != OwnerAccount.Stamp(config))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Every page requires sign-in unless it opts out with [AllowAnonymous] (only Login and Error do).
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// At most 5 sign-in attempts per 5 minutes from one address.
builder.Services.AddRateLimiter(o =>
{
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.Equals("/Login", StringComparison.OrdinalIgnoreCase)
            ? RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("open"));
    o.OnRejected = (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
        ctx.HttpContext.Response.Headers.Location = "/Login?tooMany=true";
        return ValueTask.CompletedTask;
    };
});

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();   // css/js/images load without sign-in so the login page is styled
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
