using Intern.Data;
using Intern.Data.SeedData;
using Microsoft.EntityFrameworkCore;

namespace Intern.API.Infrastructure;

/// <summary>Açılışta migration uygular ve tohum veriyi yükler.</summary>
internal static class StartupDatabaseInitializer
{
    // Hata yutulmaz; çağıran loglar.
    public static async Task RunAsync(IServiceProvider services, ILogger logger)
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        await AbilitySeedData.Initialize(services, context);
        logger.LogInformation("Veritabanı migration ve tohum veri işlemleri tamamlandı.");
    }
}
