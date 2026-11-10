using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Intern.API.WeatherMail;
using Intern.Data;
using Intern.Entities.Concretes;
using Intern.Entities.DTOs.Weather;
using Intern.Service.Services.Abstracts;
using Intern.Service.Services.Concretes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intern.API.Controllers;

[ApiController]
[Route("api/weather-account")]
[Authorize(AuthenticationSchemes = WeatherAccountController.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class WeatherLibraryController(AppDbContext db, IWeatherService weather) : BaseController
{
    private static readonly string[] Sections = ["home", "map", "air-quality"];

    // İstek gövdesinden hesap kimliği alınmaz: bütün kayıtlar oturumun sahibine aittir.
    private async Task<Guid?> AccountId(CancellationToken ct) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
        await db.WeatherMailAccounts.AnyAsync(x => x.Id == id, ct) ? id : null;

    [HttpGet("library")]
    public async Task<IActionResult> Library(CancellationToken ct)
    {
        var id = await AccountId(ct);
        if (id is null) return Unauthorized();
        var email = await db.WeatherMailAccounts.Where(x => x.Id == id).Select(x => x.Email).SingleAsync(ct);
        var favorites = await db.WeatherFavorites.Where(x => x.AccountId == id)
            .OrderBy(x => x.CreatedUtc).ThenBy(x => x.City).Select(x => x.City).ToArrayAsync(ct);
        var preferences = await db.WeatherPagePreferences.Where(x => x.AccountId == id)
            .ToDictionaryAsync(x => x.Section, x => x.City, ct);
        return Ok(new { email, favorites, preferences });
    }

    [HttpPut("favorites"), WeatherClient]
    public async Task<IActionResult> AddFavorite(CityRequest request, CancellationToken ct)
    {
        var id = await AccountId(ct);
        if (id is null) return Unauthorized();
        if (!WeatherService.CityNames.Contains(request.City)) return BadRequest(new { message = "Listeden bir şehir seç." });
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WeatherFavorites" ("AccountId", "City", "CreatedUtc") VALUES ({id.Value}, {request.City}, {now})
            ON CONFLICT ("AccountId", "City") DO NOTHING
            """, ct);
        return Ok(new { city = request.City });
    }

    [HttpDelete("favorites/{city}"), WeatherClient]
    public async Task<IActionResult> RemoveFavorite(string city, CancellationToken ct)
    {
        var id = await AccountId(ct);
        if (id is null) return Unauthorized();
        await db.WeatherFavorites.Where(x => x.AccountId == id && x.City == city).ExecuteDeleteAsync(ct);
        return Ok(new { city });
    }

    [HttpPut("preferences/{section}"), WeatherClient]
    public async Task<IActionResult> Preference(string section, CityRequest request, CancellationToken ct)
    {
        var id = await AccountId(ct);
        if (id is null) return Unauthorized();
        if (!Sections.Contains(section) || !WeatherService.CityNames.Contains(request.City))
            return BadRequest(new { message = "Geçerli bir sayfa ve şehir seç." });
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WeatherPagePreferences" ("AccountId", "Section", "City", "UpdatedUtc")
            VALUES ({id.Value}, {section}, {request.City}, {now})
            ON CONFLICT ("AccountId", "Section") DO UPDATE SET "City" = EXCLUDED."City", "UpdatedUtc" = EXCLUDED."UpdatedUtc"
            """, ct);
        return Ok(new { section, city = request.City });
    }

    [HttpGet("comparisons")]
    public async Task<IActionResult> Comparisons(CancellationToken ct)
    {
        var id = await AccountId(ct);
        if (id is null) return Unauthorized();
        var items = await db.WeatherComparisons.AsNoTracking().Where(x => x.AccountId == id)
            .OrderByDescending(x => x.ComparedUtc).ThenByDescending(x => x.Id).Take(50).ToListAsync(ct);
        return Ok(items.Select(View));
    }

    [HttpPost("comparisons"), WeatherClient]
    public async Task<IActionResult> Compare(ComparisonRequest request, CancellationToken ct)
    {
        var id = await AccountId(ct);
        if (id is null) return Unauthorized();
        if (request.FirstCity == request.SecondCity || !WeatherService.CityNames.Contains(request.FirstCity) ||
            !WeatherService.CityNames.Contains(request.SecondCity))
            return BadRequest(new { message = "Listeden iki farklı şehir seç." });
        // Değerler istemciden kabul edilmez; gösterilecek ve kaydedilecek sonuç aynı kaynaktan gelir.
        var first = await weather.GetWeatherAsync(request.FirstCity, ct);
        if (!first.IsSuccess) return ActionResultInstance(first);
        var second = await weather.GetWeatherAsync(request.SecondCity, ct);
        if (!second.IsSuccess) return ActionResultInstance(second);
        var a = first.Data;
        var b = second.Data;
        var row = new WeatherComparison
        {
            AccountId = id.Value, FirstCity = request.FirstCity, SecondCity = request.SecondCity,
            FirstTemperature = a.Temperature, FirstHumidity = a.Humidity, FirstApparent = a.Apparent,
            FirstWind = a.Wind, FirstCode = a.Code,
            SecondTemperature = b.Temperature, SecondHumidity = b.Humidity, SecondApparent = b.Apparent,
            SecondWind = b.Wind, SecondCode = b.Code, ComparedUtc = DateTimeOffset.UtcNow
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockHistory(id.Value, ct);
        db.WeatherComparisons.Add(row);
        await db.SaveChangesAsync(ct);
        var olderIds = db.WeatherComparisons.Where(x => x.AccountId == id)
            .OrderByDescending(x => x.ComparedUtc).ThenByDescending(x => x.Id).Skip(50).Select(x => x.Id);
        await db.WeatherComparisons.Where(x => olderIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(View(row));
    }

    [HttpDelete("comparisons"), WeatherClient]
    public async Task<IActionResult> ClearComparisons(CancellationToken ct)
    {
        var id = await AccountId(ct);
        if (id is null) return Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockHistory(id.Value, ct);
        await db.WeatherComparisons.Where(x => x.AccountId == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { message = "Karşılaştırma geçmişin temizlendi." });
    }

    private Task<int> LockHistory(Guid id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"comparisons:" + id}))", ct);

    private static object View(WeatherComparison row) => new
    {
        row.Id, row.FirstCity, row.SecondCity, row.ComparedUtc,
        firstWeather = new WeatherDto { Temperature = row.FirstTemperature, Humidity = row.FirstHumidity,
            Apparent = row.FirstApparent, Wind = row.FirstWind, Code = row.FirstCode },
        secondWeather = new WeatherDto { Temperature = row.SecondTemperature, Humidity = row.SecondHumidity,
            Apparent = row.SecondApparent, Wind = row.SecondWind, Code = row.SecondCode }
    };
}

public sealed record CityRequest([Required, MaxLength(80)] string City);
public sealed record ComparisonRequest([Required, MaxLength(80)] string FirstCity, [Required, MaxLength(80)] string SecondCity);
