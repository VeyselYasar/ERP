using System.Security.Claims;
using ERP.Api.Data;
using ERP.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Controllers;

public sealed class AccountController(
    UserRepository users,
    AdminRepository admins,
    CompanyProfileRepository companyProfiles,
    IPasswordHasher<UserTable> passwordHasher) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", "Home")
        : View(new LoginViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Mail.Trim(), cancellationToken);
        if (user is null || !user.AktifPasif ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "E-posta veya parola hatalı ya da hesap pasif.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, $"{user.Ad} {user.SoyAd}"),
            new(ClaimTypes.Email, user.Mail)
        };
        var isAdmin = await admins.IsAdminEmailAsync(user.Mail, cancellationToken);
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", "Home")
        : View(new RegisterViewModel());

    [Authorize]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new UserTable
        {
            Ad = model.Ad.Trim(),
            SoyAd = model.SoyAd.Trim(),
            Mail = model.Mail.Trim(),
            AktifPasif = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, model.Password);

        try
        {
            await users.CreateAsync(user, cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            ModelState.AddModelError(nameof(model.Mail), "Bu e-posta adresi zaten kayıtlı.");
            return View(model);
        }

        TempData["Success"] = "Kaydınız oluşturuldu. Şimdi giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var ticket = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return View(new ChangePasswordViewModel
        {
            RememberMe = ticket.Properties?.IsPersistent == true
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> CompanyProfile(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        return View(await companyProfiles.GetAsync(cancellationToken));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> EditCompanyProfile(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        return View(await companyProfiles.GetAsync(cancellationToken));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRememberMe(bool rememberMe)
    {
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            User,
            new AuthenticationProperties { IsPersistent = rememberMe });

        TempData["Success"] = rememberMe
            ? "Beni hatırla bu oturum için açıldı."
            : "Beni hatırla kapatıldı.";
        return RedirectToAction(nameof(Settings));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var userId)) return Challenge();

        var user = await users.FindByEmailAsync(User.FindFirstValue(ClaimTypes.Email) ?? string.Empty, cancellationToken);
        if (user is null || user.UserId != userId)
        {
            ModelState.AddModelError(string.Empty, "Kullanıcı hesabı bulunamadı.");
            return View(model);
        }

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Mevcut şifre doğru değil.");
            return View(model);
        }

        user.PasswordHash = passwordHasher.HashPassword(user, model.NewPassword);
        if (!await users.UpdatePasswordAsync(user.UserId, user.PasswordHash, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Şifre güncellenemedi. Hesap durumunu kontrol edin.");
            return View(model);
        }

        TempData["Success"] = "Şifreniz başarıyla değiştirildi.";
        return RedirectToAction(nameof(Settings));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCompanyProfile(CompanyProfileViewModel profile, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (!ModelState.IsValid)
        {
            profile.Exists = (await companyProfiles.GetAsync(cancellationToken)).Exists;
            return View("EditCompanyProfile", profile);
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
        await companyProfiles.SaveAsync(profile, userId, cancellationToken);
        TempData["Success"] = "Şirket ve fabrika profili kaydedildi.";
        return RedirectToAction(nameof(CompanyProfile));
    }
}
