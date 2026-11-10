using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Intern.API.WeatherMail;

// JSON + özel başlık CORS preflight gerektirir; diğer sitelerin çerezle form göndermesini engeller.
public sealed class WeatherClientAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Headers["X-Weather-Client"] != "web")
            context.Result = new ObjectResult(new { message = "Geçersiz istemci isteği." }) { StatusCode = 403 };
    }
}
