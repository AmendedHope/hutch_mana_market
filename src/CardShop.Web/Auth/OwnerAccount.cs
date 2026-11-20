using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace CardShop.Web.Auth;

/// <summary>
/// The single owner sign-in. The username and a salted PBKDF2 hash of the password live in
/// configuration (user secrets in development) under Auth:Username and Auth:PasswordHash.
/// The plain password is never stored.
/// </summary>
public static class OwnerAccount
{
    public const string StampClaim = "pwd_stamp";
    const int MinPasswordLength = 10;
    static readonly PasswordHasher<string> Hasher = new();

    public static bool IsConfigured(IConfiguration c) =>
        !string.IsNullOrWhiteSpace(c["Auth:Username"]) && !string.IsNullOrWhiteSpace(c["Auth:PasswordHash"]);

    public static bool Verify(IConfiguration c, string? username, string? password)
    {
        var user = (c["Auth:Username"] ?? "").Trim();
        var hash = c["Auth:PasswordHash"] ?? "";
        if (user.Length == 0 || hash.Length == 0) return false;

        // Always run the hash check, even for a wrong username, so both cases take the same time.
        var nameOk = string.Equals(user, (username ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        PasswordVerificationResult result;
        try { result = Hasher.VerifyHashedPassword(user, hash, password ?? ""); }
        catch (FormatException) { return false; }
        return nameOk && result != PasswordVerificationResult.Failed;
    }

    /// <summary>Short fingerprint of the current hash. Changing the password changes it,
    /// which signs out every existing session.</summary>
    public static string Stamp(IConfiguration c) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(c["Auth:PasswordHash"] ?? "")))[..16];

    /// <summary>Console flow for: dotnet run --project src/CardShop.Web -- set-password [--print]
    /// Without --print it saves to this computer's user secrets (local development).
    /// With --print it only prints the two settings to add on a hosting server.</summary>
    public static int RunSetup(bool printOnly)
    {
        Console.WriteLine();
        Console.WriteLine("Set up the owner sign-in for HutchisMuch Mana Shop");
        Console.WriteLine("---------------------------------------------------");
        if (Console.IsInputRedirected)
            Console.WriteLine("Note: this terminal can't hide typing, so your password will be visible as you type.");

        Console.Write("Username: ");
        var username = (Console.ReadLine() ?? "").Trim();
        if (username.Length == 0) { Console.Error.WriteLine("Username can't be empty."); return 1; }

        var pw = ReadSecret($"Password (at least {MinPasswordLength} characters): ");
        if (pw.Length < MinPasswordLength) { Console.Error.WriteLine($"Password must be at least {MinPasswordLength} characters."); return 1; }
        if (ReadSecret("Type it again: ") != pw) { Console.Error.WriteLine("The passwords didn't match. Nothing was changed."); return 1; }

        var hash = Hasher.HashPassword(username, pw);

        if (printOnly)
        {
            Console.WriteLine();
            Console.WriteLine("Add these two settings (environment variables) on your hosting server:");
            Console.WriteLine();
            Console.WriteLine($"  Auth__Username={username}");
            Console.WriteLine($"  Auth__PasswordHash={hash}");
            Console.WriteLine();
            Console.WriteLine("The hash is not your password, but keep it private anyway. Nothing was saved on this computer.");
            return 0;
        }

        var id = typeof(OwnerAccount).Assembly.GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId;
        if (id is null) { Console.Error.WriteLine("This project has no UserSecretsId."); return 1; }
        var path = PathHelper.GetSecretsPathFromSecretsId(id);

        JsonObject root = new();
        if (File.Exists(path))
        {
            var text = File.ReadAllText(path); // strips a BOM if present
            if (!string.IsNullOrWhiteSpace(text)) root = JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }
        root.Remove("Auth"); // drop any nested section so the flat keys below win
        root["Auth:Username"] = username;
        root["Auth:PasswordHash"] = hash;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"Saved. Sign in as \"{username}\". Any open sessions have been signed out.");
        return 0;
    }

    static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";

        var sb = new StringBuilder();
        try
        {
            while (true)
            {
                var k = Console.ReadKey(intercept: true);
                if (k.Key == ConsoleKey.Enter) break;
                if (k.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
                    continue;
                }
                if (!char.IsControl(k.KeyChar)) { sb.Append(k.KeyChar); Console.Write('*'); }
            }
        }
        catch (InvalidOperationException)
        {
            return Console.ReadLine() ?? "";
        }
        Console.WriteLine();
        return sb.ToString();
    }
}
