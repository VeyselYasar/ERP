using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ERP.Api.Data;

namespace ERP.Api.Controllers;

public sealed class HomeController(AdminRepository repository) : Controller
{
    [Authorize]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Challenge();

        var tables = await repository.GetVisibleTablesAsync(
            userId,
            User.IsInRole("Admin"),
            cancellationToken);

        if (tables.Count == 0)
            return View();

        return RedirectToAction("Open", "Tables", new { tableName = tables[0] });
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
