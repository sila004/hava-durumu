using Intern.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Intern.API.Controllers
{
    public class BaseController : ControllerBase
    {
        public IActionResult ActionResultInstance<T>(Response<T> response) where T : class
        {
            if (response.Error?.Code == "WeatherProviderRateLimited" &&
                response.Error.Params?.TryGetValue("retryAfterSeconds", out var retryAfter) == true &&
                long.TryParse(retryAfter, out var seconds) && seconds > 0)
            {
                Response.Headers["Retry-After"] = retryAfter;
                Response.Headers["Cache-Control"] = "no-store";
            }

            if (response.StatusCode == (int)HttpStatusCode.NoContent)
                return NoContent();

            if (response.StatusCode == (int)HttpStatusCode.NotFound)
                return NotFound(response);

            if (response.StatusCode == (int)HttpStatusCode.BadRequest)
                return BadRequest(response);

            return new ObjectResult(response)
            {
                StatusCode = response.StatusCode
            };
        }
    }

}
