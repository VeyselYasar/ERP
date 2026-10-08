using ERP.Api.Data;
using ERP.Api.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AdminRepository>();
builder.Services.AddScoped<StockRepository>();
builder.Services.AddScoped<PackagingRecipeRepository>();
builder.Services.AddScoped<StockTransferRepository>();
builder.Services.AddScoped<StockMovementRepository>();
builder.Services.AddScoped<ProductionRepository>();
builder.Services.AddScoped<StockReportRepository>();
builder.Services.AddScoped<CompanyProfileRepository>();
builder.Services.AddScoped<CustomerRepository>();
builder.Services.AddScoped<SalesRepository>();
builder.Services.AddScoped<IPasswordHasher<UserTable>, PasswordHasher<UserTable>>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
