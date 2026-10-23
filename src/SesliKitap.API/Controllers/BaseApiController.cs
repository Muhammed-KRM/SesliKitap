using Microsoft.AspNetCore.Mvc;

namespace SesliKitap.API.Controllers;

/// <summary>
/// Tüm SesliKitap API denetleyicilerinin (controller) ortak taban sınıfı.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Mevcut HTTP isteğinin uçtan uca izleme kimliği (<see cref="HttpContext.TraceIdentifier"/>).
    /// </summary>
    protected string TraceId => HttpContext.TraceIdentifier;
}
