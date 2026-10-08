using System.Security.Claims;
using ERP.Api.Data;
using ERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController(AdminRepository adminRepository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Permissions(int? userId, CancellationToken cancellationToken)
    {
        var model = await adminRepository.GetPermissionsPageAsync(userId, cancellationToken);
        if (model.Users.Count == 0)
            TempData["Info"] = "Yetki vermek için önce en az bir aktif kullanıcı kaydedin.";

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(
        AdminPermissionsViewModel model,
        CancellationToken cancellationToken)
    {
        var page = await adminRepository.GetPermissionsPageAsync(model.SelectedUserId, cancellationToken);
        if (page.SelectedUserId != model.SelectedUserId || page.Users.Count == 0)
            ModelState.AddModelError(string.Empty, "Seçilen aktif kullanıcı bulunamadı.");

        var allowedNames = page.Permissions.Select(permission => permission.TableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var submittedNames = model.Permissions.Select(permission => permission.TableName).ToList();
        if (submittedNames.Count != allowedNames.Count ||
            submittedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != submittedNames.Count ||
            !allowedNames.SetEquals(submittedNames))
        {
            ModelState.AddModelError(string.Empty, "İzin listesi geçerli tablo listesiyle eşleşmiyor.");
        }

        if (!ModelState.IsValid)
        {
            page.Permissions = model.Permissions;
            return View(page);
        }

        await adminRepository.SavePermissionsAsync(model.SelectedUserId, model.Permissions, cancellationToken);
        TempData["Success"] = "Kullanıcı yetkileri kaydedildi.";
        return RedirectToAction(nameof(Permissions), new { userId = model.SelectedUserId });
    }
}
