using Autofac;
using Autofac.Extensions.DependencyInjection;
using DotNetEnv;
using Intern.API.Infrastructure;
using Intern.API.Middleware;
using Intern.API.Modules;
using Intern.Data;
using Intern.Service.Services.Concretes;
using Intern.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Serilog;
using Serilog.Events;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Intern.API.Controllers;
using Intern.API.WeatherMail;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

// .env dosyasındaki ortam değişkenlerini yükler.
Env.NoClobber().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration["JWT:Secret"] = Environment.GetEnvironmentVariable("JWT_SECRET") ?? builder.Configuration["JWT:Secret"];

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] IP:[{ClientIp}] User:[{UserName}] {Message}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Information,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] IP:[{ClientIp}] User:[{UserName}] {Message}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// CORS: izinli adresler appsettings "Cors:AllowedOrigins" bölümünden okunur.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
{
    if (!builder.Environment.IsDevelopment() &&
        !builder.Configuration.GetValue("Features:EnableSampleControllers", false))
        manager.FeatureProviders.Add(new SampleControllersFeatureProvider());
});

// Yalnız Render gibi kendi güvenilir ağ geçidi arkasındaki dağıtımda açılır.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    if (!builder.Configuration.GetValue("ReverseProxy:Enabled", false)) return;
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2; // Vercel yönlendirmesi + hosting ağ geçidi.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddHttpContextAccessor();
// Dış API yanıtlarını kısa süre önbelleğe almak için hazır.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<WeatherRequestCache>();

// Swagger (sağ üstte Bearer token girişi ile)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Intern API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// JWT kimlik doğrulama. Sır yoksa uygulama açılmaz (.env içindeki JWT_SECRET).
var jwtSecret = builder.Configuration["JWT:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException("JWT Secret not found. Intern.API/.env içine JWT_SECRET yazın.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        NameClaimType = ClaimTypes.NameIdentifier
    };

    // Token "accessToken" çerezindeyse onu kullan; yoksa Authorization başlığına düşer.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Cookies.TryGetValue("accessToken", out var token) && !string.IsNullOrEmpty(token))
                context.Token = token;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization(options => options.AddPolicy(WeatherAdminRequirement.Policy, policy =>
    policy.AddAuthenticationSchemes(WeatherAccountController.Scheme).RequireAuthenticatedUser()
        .AddRequirements(new WeatherAdminRequirement())));
builder.Services.Configure<WeatherAdminSettings>(builder.Configuration.GetSection("WeatherAdmin"));
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, WeatherAdminHandler>();

builder.Services.AddAuthentication().AddCookie(WeatherAccountController.Scheme, options =>
{
    options.Cookie.Name = "weatherSession";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("Mail"));
builder.Services.AddScoped<WeatherMailSender>();
builder.Services.AddHostedService<DailyWeatherMailBackgroundService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Çok fazla deneme yapıldı. Bir süre sonra tekrar dene." }, ct);
    options.AddPolicy("email-code", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
    options.AddPolicy("email-verify", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.AddPolicy("email-test", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 3, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});

// Dış API ayarları (appsettings "ExchangeRate" bölümü)
builder.Services.Configure<ExchangeRateSettings>(builder.Configuration.GetSection(ExchangeRateSettings.SectionName));

// Yeni bir dış API (ör. başka bir servis) eklerken bu bloğu kopyalayın.
builder.Services.AddHttpClient(ExchangeRateService.HttpClientName, (sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<ExchangeRateSettings>>().Value;
    // Dış zaman aşımı: yeniden denemelere pay bırakılır.
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds * 4);
})
.AddResilienceHandler("exchange-rate-pipeline", (pipeline, context) =>
{
    var settings = context.ServiceProvider.GetRequiredService<IOptions<ExchangeRateSettings>>().Value;
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 2,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromMilliseconds(500)
    });
    pipeline.AddTimeout(TimeSpan.FromSeconds(settings.TimeoutSeconds));
});

// Hava durumu servisi Autofac tarafından kaydedilir; burada yalnız ayar ve HTTP istemcisi tanımlanır.
builder.Services.Configure<WeatherSettings>(builder.Configuration.GetSection(WeatherSettings.SectionName));
builder.Services.AddHttpClient(WeatherService.HttpClientName, (sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<WeatherSettings>>().Value;
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli
});

// Veritabanı
var connectionString = RuntimeConnectionString.Resolve(builder.Configuration);
// Npgsql: DateTime için eski zaman damgası davranışı açık kalır (AddDbContext'ten önce ayarlanmalı).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly("Intern.Data");
    });
});

// Konteyner yeniden başlasa da hesap oturumlarının anahtarları PostgreSQL'de kalır.
if (!builder.Environment.IsDevelopment())
    builder.Services.AddDataProtection().SetApplicationName("Meteoroloji")
        .PersistKeysToDbContext<AppDbContext>();

// Autofac bağımlılık enjeksiyonu
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
    containerBuilder.RegisterModule(new ServiceModule()));

var app = builder.Build();

// Açılışta migration + tohum veri (kapatmak için Database:MigrateOnStartup=false).
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await StartupDatabaseInitializer.RunAsync(scope.ServiceProvider, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Veritabanı migration veya tohum veri sırasında hata oluştu.");
        if (!app.Environment.IsDevelopment()) throw;
    }
}

app.UseForwardedHeaders();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowSpecificOrigin");
app.UseRateLimiter();

app.UseAuthentication();

// Log bağlamı: istek yolu, istemci IP'si, kullanıcı adı.
// UseAuthentication'dan SONRA durur; önce dursa kullanıcı adı hep "anonymous" yazılır.
app.Use(async (context, next) =>
{
    var userName = context.User.Identity?.IsAuthenticated == true
        ? context.User.Identity.Name
        : "anonymous";
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    using (Serilog.Context.LogContext.PushProperty("RequestPath", context.Request.Path.Value))
    using (Serilog.Context.LogContext.PushProperty("ClientIp", ip))
    using (Serilog.Context.LogContext.PushProperty("UserName", userName))
    {
        await next();
    }
});

app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("Healthy")).AllowAnonymous();
app.MapGet("/health/ready", async Task<IResult> (AppDbContext db, CancellationToken ct) =>
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeout.CancelAfter(TimeSpan.FromSeconds(3));
    try
    {
        return await db.Database.CanConnectAsync(timeout.Token)
            ? Results.Ok("Ready")
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();
app.MapControllers();

app.Run();
