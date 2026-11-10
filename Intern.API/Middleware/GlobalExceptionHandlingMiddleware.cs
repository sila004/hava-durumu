using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Intern.API.Middleware
{
    /// <summary>
    /// Yakalanmamış tüm istisnaları merkezi olarak yakalar. Tam detay loglanır;
    /// istemciye genel bir ProblemDetails döner (Development'ta detay, değilse genel mesaj + traceId).
    /// </summary>
    public class GlobalExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlingMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var traceId = context.TraceIdentifier;

                // Tam detay yalnızca sunucu loguna.
                _logger.LogError(ex,
                    "Beklenmeyen bir hata oluştu. TraceId: {TraceId} Method: {Method} Path: {Path}",
                    traceId, context.Request.Method, context.Request.Path);

                // Yanıt başladıysa gövde değiştirilemez.
                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Beklenmeyen bir hata oluştu.",
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    Instance = context.Request.Path,
                    Detail = _environment.IsDevelopment()
                        ? ex.ToString()
                        : "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin."
                };
                problem.Extensions["traceId"] = traceId;

                await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
            }
        }
    }
}
