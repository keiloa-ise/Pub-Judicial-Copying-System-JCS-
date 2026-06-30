using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Infrastructure.Persistence;

namespace ResourceIQ.Jcs.Api.Bootstrap;

/// <summary>
/// One-time, OPT-IN production bootstrap. Runs only when JCS_BOOTSTRAP=true (per 
/// migrations are never applied unconditionally from startup). When enabled it:
///   1) applies pending EF migrations,
///   2) seeds reference data (decision-type templates + paragraphs — NOT demo users),
///   3) creates the first Administrator if none exists, using JCS_ADMIN_USERNAME /
///      JCS_ADMIN_PASSWORD (fails fast if the password is missing).
/// After the first successful deploy, set JCS_BOOTSTRAP=false and redeploy.
/// </summary>
public static class ProductionBootstrap
{
    public static async Task RunAsync(WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("JCS_BOOTSTRAP")) return;

        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<JcsDbContext>();
        var logger = sp.GetRequiredService<ILogger<JcsDbContext>>();

        logger.LogInformation("JCS_BOOTSTRAP=true: applying migrations and reference data.");
        await db.Database.MigrateAsync();
        await DbSeeder.SeedReferenceDataAsync(db);
        await EnsureAdminAsync(db, sp.GetRequiredService<IPasswordHasher>(), app.Configuration, logger);
    }

    private static async Task EnsureAdminAsync(
        JcsDbContext db, IPasswordHasher hasher, IConfiguration cfg, ILogger logger)
    {
        if (await db.Users.AnyAsync(u => u.Role == Role.Administrator)) return;

        var username = cfg["JCS_ADMIN_USERNAME"] ?? "admin";
        var password = cfg["JCS_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "JCS_ADMIN_PASSWORD must be set when JCS_BOOTSTRAP=true and no Administrator exists.");

        db.Users.Add(new User
        {
            Username = username.Trim(),
            DisplayName = "مدير النظام",
            Role = Role.Administrator,
            IsActive = true,
            PasswordHash = hasher.Hash(password),
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Created initial Administrator '{Username}'.", username);
    }
}
