using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ArcadeManager.Controllers;

/// <summary>
/// Controller for the overlays page
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OverlaysController" /> class.
/// </remarks>
/// <param name="logger">The logger.</param>
public class OverlaysController(ILogger<OverlaysController> logger) : BaseController(logger)
{
    public IActionResult Check() => View();

    public IActionResult ConvertMameToRa() => View();

    public IActionResult ConvertRaToMame() => View();

    public IActionResult Generate() => View();

    public IActionResult Index() => View();

    public IActionResult Install() => View();
}