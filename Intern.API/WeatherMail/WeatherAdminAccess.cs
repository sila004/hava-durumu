using System.Security.Claims;
using Intern.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intern.API.WeatherMail;

public sealed class WeatherAdminSettings
{
    public string[] Emails { get; set; } = [];
    public bool Contains(string email) => Emails.Any(value =>
        string.Equals(value.Trim(), email, StringComparison.OrdinalIgnoreCase));
}

public sealed class WeatherAdminRequirement : IAuthorizationRequirement
{
    public const string Policy = "WeatherAdmin";
}

// Yetki her istekte mevcut hesaptan ve sunucu yapılandırmasından okunur.
// İstemciden gelen rol/adres bilgisi veya eski çerezdeki rol kabul edilmez.
public sealed class WeatherAdminHandler(AppDbContext db, IOptions<WeatherAdminSettings> settings)
    : AuthorizationHandler<WeatherAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, WeatherAdminRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return;
        var email = await db.WeatherMailAccounts.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.Email).SingleOrDefaultAsync();
        if (email is not null && settings.Value.Contains(email)) context.Succeed(requirement);
    }
}
