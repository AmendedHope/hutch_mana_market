using System.Net;
using System.Threading.RateLimiting;
using CardShop.Core.Abstractions;
using CardShop.Infrastructure;
using CardShop.Web;
using CardShop.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// dotnet run --project src/CardShop.Web -- set-password
// dotnet run --project src/CardShop.Web -- set-password --print   (prints settings for a hosting server)
if (args.Length > 0 && args[0] == "set-password")
{
    Environment.ExitCode = OwnerAccount.RunSetup(printOnly: args.Contains("--print"));
    return;
}

var builder = WebApplication.CreateBuilder(args);
var isDev = builder.Environment.IsDevelopment();

// ----- Hosting -----
// Behind a reverse proxy (nginx, IIS, a load balancer), read the visitor's real address and
// https scheme from the proxy's headers. Only proxies on this machine are trusted by default;
// list others in Hosting:TrustedProxies (comma-separated IPs) so visitors can't fake their address.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var ip in (builder.Configuration["Hosting:TrustedProxies"] ?? "")
                 .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        if (IPAddress.TryParse(ip, out var addr)) o.KnownProxies.Add(addr);
});

// Keys that encrypt sign-in cookies. Keep them in a folder that survives restarts and redeploys
// (DataProtection:KeysPath), or everyone is signed out every time the app restarts.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("HutchisMuchManaShop");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// On the server, cookies are only ever sent over https.
var cookieSecurity = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = cookieSecurity);

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
        o.Cookie.SecurePolicy = cookieSecurity;
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

if (!OwnerAccount.IsConfigured(app.Configuration))
    app.Logger.LogWarning("No owner account is configured, so nobody can sign in. Set Auth__Username and Auth__PasswordHash " +
                          "(run: dotnet run --project src/CardShop.Web -- set-password --print).");

// On the server, create/upgrade the database tables at startup (Database__MigrateOnStartup=true).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.MigrateAsync();
}

app.UseForwardedHeaders();

if (!isDev)
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Basic browser protections: no framing (clickjacking), no MIME sniffing, forms only post back here.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h.ContentSecurityPolicy = "frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();   // css/js/images load without sign-in so the login page is styled
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
