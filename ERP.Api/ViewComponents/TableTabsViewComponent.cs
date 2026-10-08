using System.Security.Claims;
using ERP.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.ViewComponents;

public sealed class TableTabsViewComponent(AdminRepository repository) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true ||
            !int.TryParse(HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return View(Array.Empty<string>());
        }

        var tables = await repository.GetVisibleTablesAsync(
            userId,
            HttpContext.User.IsInRole("Admin"),
            HttpContext.RequestAborted);
        return View(tables);
    }
}
