using System.Security.Claims;
using ERP.Api.Data;
using ERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ERP.Api.Controllers;

[Authorize]
public sealed class TablesController(AdminRepository repository, StockRepository stockRepository, PackagingRecipeRepository recipeRepository, StockTransferRepository transferRepository, StockMovementRepository movementRepository, ProductionRepository productionRepository, StockReportRepository reportRepository, CustomerRepository customerRepository, SalesRepository salesRepository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Open(string tableName, DateTime? fromDate, DateTime? toDate, int? customerId, string? productName, string? shipmentType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName) ||
            !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return NotFound();
        }

        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, tableName, cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Okuma && !access.Yazma && !access.Silme && !access.Onizleme)
            return Forbid();

        if (string.Equals(tableName, "STOK", StringComparison.OrdinalIgnoreCase))
        {
            var model = new StockPageViewModel
            {
                CanRead = access.Okuma || access.IsAdmin,
                CanWrite = access.Yazma || access.IsAdmin,
                ProductType = "Mamul",
                Unit = "Adet"
            };
            if (model.CanRead)
                model.Items = await stockRepository.GetItemsAsync(cancellationToken);
            model.AvailableRecipes = await stockRepository.GetRecipesForStockAsync(model.ProductType, model.Unit, cancellationToken);
            return View("Stock", model);
        }

        if (string.Equals(tableName, "AMBALAJ REÇETESİ", StringComparison.OrdinalIgnoreCase))
        {
            var model = new PackagingRecipePageViewModel
            {
                CanRead = access.Okuma || access.IsAdmin,
                CanWrite = access.Yazma || access.IsAdmin
            };
            if (model.CanRead) model.Recipes = await recipeRepository.GetRecipesAsync(cancellationToken);
            return View("PackagingRecipes", model);
        }

        if (string.Equals(tableName, "DEPO TRANSFERİ", StringComparison.OrdinalIgnoreCase))
        {
            var model = new TransferPageViewModel
            {
                CanRead = access.Okuma || access.IsAdmin,
                CanWrite = access.Yazma || access.IsAdmin
            };
            if (model.CanRead) model.Transfers = await transferRepository.GetHistoryAsync(cancellationToken);
            if (model.CanWrite) model.Sources = await transferRepository.GetSourcesAsync(cancellationToken);
            return View("WarehouseTransfers", model);
        }

        if (string.Equals(tableName, "STOK HAREKETLERİ", StringComparison.OrdinalIgnoreCase))
        {
            var model = new StockMovementPageViewModel
            {
                CanRead = access.Okuma || access.IsAdmin,
                CanWrite = access.Yazma || access.IsAdmin
            };
            if (model.CanRead) model.Movements = await movementRepository.GetHistoryAsync(cancellationToken);
            if (model.CanWrite) model.Sources = await transferRepository.GetSourcesAsync(cancellationToken);
            return View("StockMovements", model);
        }

        if (string.Equals(tableName, "ÜRETİM KAYDI", StringComparison.OrdinalIgnoreCase))
        {
            var model = new ProductionPageViewModel
            {
                CanRead = access.Okuma || access.IsAdmin,
                CanWrite = access.Yazma || access.IsAdmin,
                Recipes = (await recipeRepository.GetRecipesAsync(cancellationToken))
                    .Where(recipe => recipe.ProductType == "Mamul").ToList()
            };
            if (model.CanRead) model.Records = await productionRepository.GetHistoryAsync(cancellationToken);
            return View("Production", model);
        }

        if (string.Equals(tableName, "STOK RAPORU", StringComparison.OrdinalIgnoreCase))
        {
            if (!(access.Okuma || access.IsAdmin)) return Forbid();
            var start = fromDate?.Date ?? DateTime.Today.AddDays(-30);
            var end = toDate?.Date ?? DateTime.Today;
            if (end < start) end = start;
            return View("StockReport", await reportRepository.GetReportAsync(
                DateTime.SpecifyKind(start, DateTimeKind.Local).ToUniversalTime(),
                DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Local).ToUniversalTime(),
                start, end, cancellationToken));
        }

        if (string.Equals(tableName, "MÜŞTERİLER", StringComparison.OrdinalIgnoreCase))
        {
            var model = await customerRepository.GetPageAsync(customerId, access.Okuma || access.IsAdmin,
                access.Yazma || access.IsAdmin, cancellationToken);
            return View("Customers", model);
        }

        if (string.Equals(tableName, "SATIŞ", StringComparison.OrdinalIgnoreCase))
        {
            var model = await salesRepository.GetPageAsync(access.Okuma || access.IsAdmin,
                access.Yazma || access.IsAdmin, cancellationToken);
            return View("Sales", model);
        }

        if (string.Equals(tableName, "SATIŞ RAPORU", StringComparison.OrdinalIgnoreCase))
        {
            if (!(access.Okuma || access.IsAdmin)) return Forbid();
            var toDateExclusive = toDate?.Date.AddDays(1);
            var model = await salesRepository.GetReportAsync(fromDate?.Date, toDateExclusive, customerId,
                productName, shipmentType, cancellationToken);
            return View("SalesReport", model);
        }

        return View("Open", access);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStockItem(StockPageViewModel model, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "STOK", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();

        if (string.IsNullOrWhiteSpace(model.ProductName) || model.ProductName.Length > 150)
            ModelState.AddModelError(nameof(model.ProductName), "Ürün adı zorunludur (en fazla 150 karakter).");
        if (string.IsNullOrWhiteSpace(model.ProductType) || model.ProductType.Length > 100)
            ModelState.AddModelError(nameof(model.ProductType), "Ürün cinsi zorunludur.");
        if (!new[] { "Hammadde", "Mamul" }.Contains(model.ProductType))
            ModelState.AddModelError(nameof(model.ProductType), "Hammadde veya Mamul seçin.");
        if (model.Quantity <= 0) ModelState.AddModelError(nameof(model.Quantity), "Miktar sıfırdan büyük olmalıdır.");
        if (model.MinimumQuantity < 0) ModelState.AddModelError(nameof(model.MinimumQuantity), "Düşük stok eşiği sıfır veya daha büyük olmalıdır.");
        if (!new[] { "Adet", "Kg", "Metre", "Litre" }.Contains(model.Unit))
            ModelState.AddModelError(nameof(model.Unit), "Geçerli bir miktar birimi seçin.");
        if (!new[] { "Depo 1", "Depo 2", "Depo 3" }.Contains(model.Warehouse))
            ModelState.AddModelError(nameof(model.Warehouse), "Geçerli bir depo seçin.");
        var compatibleRecipes = await stockRepository.GetRecipesForStockAsync(model.ProductType, model.Unit, cancellationToken);
        if (model.PackagingRecipeId.HasValue && !compatibleRecipes.Any(recipe => recipe.Id == model.PackagingRecipeId.Value))
            ModelState.AddModelError(nameof(model.PackagingRecipeId), "Seçilen ambalaj reçetesi ürün cinsi ve birimiyle uyumlu değil.");

        if (ModelState.IsValid)
        {
            await stockRepository.AddItemAsync(model, userId, cancellationToken);
            return RedirectToAction(nameof(Open), new { tableName = "STOK" });
        }

        model.TableName = "STOK";
        model.CanRead = access.Okuma || isAdmin;
        model.CanWrite = true;
        if (model.CanRead) model.Items = await stockRepository.GetItemsAsync(cancellationToken);
        model.AvailableRecipes = compatibleRecipes;
        return View("Stock", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMinimumQuantity(int stockItemId, decimal minimumQuantity, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "STOK", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();
        if (stockItemId < 1 || minimumQuantity < 0) return BadRequest();
        await stockRepository.UpdateMinimumQuantityAsync(stockItemId, minimumQuantity, cancellationToken);
        return RedirectToAction(nameof(Open), new { tableName = "STOK" });
    }

    [HttpGet]
    public async Task<IActionResult> PackagingOptions(string productType, string unit, CancellationToken cancellationToken)
    {
        if (!new[] { "Hammadde", "Mamul" }.Contains(productType) ||
            !new[] { "Adet", "Kg", "Metre", "Litre" }.Contains(unit)) return BadRequest();
        return Json(await stockRepository.GetRecipesForStockAsync(productType, unit, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePackagingRecipe(PackagingRecipePageViewModel model, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "AMBALAJ REÇETESİ", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();

        if (string.IsNullOrWhiteSpace(model.RecipeName) || model.RecipeName.Length > 100)
            ModelState.AddModelError(nameof(model.RecipeName), "Reçete adı zorunludur (en fazla 100 karakter).");
        if (!new[] { "Hammadde", "Mamul" }.Contains(model.ProductType))
            ModelState.AddModelError(nameof(model.ProductType), "Hammadde veya Mamul seçin.");
        if (!new[] { "Adet", "Kg", "Metre", "Litre" }.Contains(model.Unit))
            ModelState.AddModelError(nameof(model.Unit), "Geçerli bir stok birimi seçin.");
        if (model.QuantityPerPackage <= 0) ModelState.AddModelError(nameof(model.QuantityPerPackage), "Ambalaj içi miktar sıfırdan büyük olmalıdır.");
        if (model.PackagesPerPallet <= 0) ModelState.AddModelError(nameof(model.PackagesPerPallet), "Paletteki ambalaj sayısı sıfırdan büyük olmalıdır.");

        if (ModelState.IsValid)
        {
            await recipeRepository.AddRecipeAsync(model, cancellationToken);
            return RedirectToAction(nameof(Open), new { tableName = "AMBALAJ REÇETESİ" });
        }

        model.TableName = "AMBALAJ REÇETESİ";
        model.CanWrite = true;
        model.CanRead = access.Okuma || isAdmin;
        if (model.CanRead) model.Recipes = await recipeRepository.GetRecipesAsync(cancellationToken);
        return View("PackagingRecipes", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTransfer(TransferPageViewModel model, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "DEPO TRANSFERİ", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();

        if (!model.StockItemId.HasValue) ModelState.AddModelError(nameof(model.StockItemId), "Transfer edilecek stok kaydını seçin.");
        if (!new[] { "Depo 1", "Depo 2", "Depo 3" }.Contains(model.ToWarehouse))
            ModelState.AddModelError(nameof(model.ToWarehouse), "Hedef depo seçin.");
        if (model.Quantity < 0.001m) ModelState.AddModelError(nameof(model.Quantity), "Transfer miktarı en az 0,001 olmalıdır.");

        if (ModelState.IsValid)
        {
            model.Message = await transferRepository.TransferAsync(model.StockItemId!.Value, model.ToWarehouse, model.Quantity, userId, cancellationToken);
            if (model.Message is null) return RedirectToAction(nameof(Open), new { tableName = "DEPO TRANSFERİ" });
        }

        model.CanWrite = true;
        model.CanRead = access.Okuma || isAdmin;
        model.Sources = await transferRepository.GetSourcesAsync(cancellationToken);
        if (model.CanRead) model.Transfers = await transferRepository.GetHistoryAsync(cancellationToken);
        return View("WarehouseTransfers", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordStockMovement(StockMovementPageViewModel model, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "STOK HAREKETLERİ", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();

        if (!model.StockItemId.HasValue) ModelState.AddModelError(nameof(model.StockItemId), "Stok kaydı seçin.");
        if (!new[] { "Giriş", "Çıkış" }.Contains(model.MovementType))
            ModelState.AddModelError(nameof(model.MovementType), "Giriş veya Çıkış seçin.");
        if (model.Quantity < 0.001m) ModelState.AddModelError(nameof(model.Quantity), "Miktar en az 0,001 olmalıdır.");
        if (!new[] { "Satın alma", "Üretim", "Fire", "Sayım düzeltmesi", "Satış / sevk", "İade", "Diğer" }.Contains(model.Reason))
            ModelState.AddModelError(nameof(model.Reason), "Listeden geçerli bir neden seçin.");
        if (model.Note?.Length > 300) ModelState.AddModelError(nameof(model.Note), "Açıklama en fazla 300 karakter olabilir.");

        if (ModelState.IsValid)
        {
            model.Message = await movementRepository.RecordAsync(model.StockItemId!.Value, model.MovementType,
                model.Quantity, model.Reason, model.Note, userId, cancellationToken);
            if (model.Message is null) return RedirectToAction(nameof(Open), new { tableName = "STOK HAREKETLERİ" });
        }

        model.CanWrite = true;
        model.CanRead = access.Okuma || isAdmin;
        model.Sources = await transferRepository.GetSourcesAsync(cancellationToken);
        if (model.CanRead) model.Movements = await movementRepository.GetHistoryAsync(cancellationToken);
        return View("StockMovements", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordProduction(ProductionPageViewModel model, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "ÜRETİM KAYDI", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();

        var recipes = (await recipeRepository.GetRecipesAsync(cancellationToken))
            .Where(recipe => recipe.ProductType == "Mamul").ToList();
        var recipe = recipes.FirstOrDefault(item => item.Id == model.PackagingRecipeId);
        if (string.IsNullOrWhiteSpace(model.ProductName) || model.ProductName.Length > 150)
            ModelState.AddModelError(nameof(model.ProductName), "Mamul adı zorunludur (en fazla 150 karakter).");
        if (recipe is null) ModelState.AddModelError(nameof(model.PackagingRecipeId), "Önce Ambalaj Reçetesi modülünde bir Mamul reçetesi oluşturun.");
        if (model.Quantity < 0.001m) ModelState.AddModelError(nameof(model.Quantity), "Üretilen miktar en az 0,001 olmalıdır.");
        if (!new[] { "Depo 1", "Depo 2", "Depo 3" }.Contains(model.Warehouse))
            ModelState.AddModelError(nameof(model.Warehouse), "Geçerli bir depo seçin.");
        if (model.Note?.Length > 300) ModelState.AddModelError(nameof(model.Note), "Açıklama en fazla 300 karakter olabilir.");

        if (ModelState.IsValid && recipe is not null)
        {
            await productionRepository.RecordAsync(model, recipe, userId, cancellationToken);
            return RedirectToAction(nameof(Open), new { tableName = "ÜRETİM KAYDI" });
        }

        model.CanWrite = true;
        model.CanRead = access.Okuma || isAdmin;
        model.Recipes = recipes;
        if (model.CanRead) model.Records = await productionRepository.GetHistoryAsync(cancellationToken);
        return View("Production", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCustomer(CustomerPageViewModel page, CancellationToken cancellationToken)
    {
        var customer = page.Form;
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "MÜŞTERİLER", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();

        if (!new[] { "Şirket", "Şahıs" }.Contains(customer.CustomerType))
            ModelState.AddModelError("Form.CustomerType", "Şirket veya Şahıs seçin.");

        if (ModelState.IsValid)
        {
            try
            {
                await customerRepository.SaveAsync(customer, userId, cancellationToken);
                TempData["Success"] = "Müşteri kartı kaydedildi.";
                return RedirectToAction(nameof(Open), new { tableName = "MÜŞTERİLER" });
            }
            catch (SqlException exception) when (exception.Number is 2601 or 2627)
            {
                ModelState.AddModelError("Form.CustomerCode", "Bu müşteri kodu zaten kullanılıyor.");
            }
        }

        var model = await customerRepository.GetPageAsync(null, access.Okuma || isAdmin, true, cancellationToken);
        model.Form = customer;
        return View("Customers", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCustomerStatus(int customerId, bool isActive, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "MÜŞTERİLER", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();

        if (await customerRepository.SetActiveAsync(customerId, isActive, userId, cancellationToken))
            TempData["Success"] = isActive ? "Müşteri aktif duruma alındı." : "Müşteri pasif duruma alındı.";
        else
            TempData["Success"] = "Müşteri kaydı bulunamadı.";

        return RedirectToAction(nameof(Open), new { tableName = "MÜŞTERİLER" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordSale(SalePageViewModel model, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        var access = await repository.GetTableAccessAsync(userId, isAdmin, "SATIŞ", cancellationToken);
        if (access is null) return NotFound();
        if (!isAdmin && !access.Yazma) return Forbid();

        if (!model.CustomerId.HasValue || model.CustomerId < 1)
            ModelState.AddModelError(nameof(model.CustomerId), "Aktif müşteri seçin.");
        if (!model.StockItemId.HasValue || model.StockItemId < 1)
            ModelState.AddModelError(nameof(model.StockItemId), "Satılacak ürün ve depoyu seçin.");
        if (model.Quantity <= 0)
            ModelState.AddModelError(nameof(model.Quantity), "Satış miktarı sıfırdan büyük olmalı.");
        if (model.Quantity > 999999999999999.999m)
            ModelState.AddModelError(nameof(model.Quantity), "Satış miktarı sistemin desteklediği sınırı aşıyor.");
        if (model.UnitPrice <= 0 || model.UnitPrice > 99999999999999.9999m)
            ModelState.AddModelError(nameof(model.UnitPrice), "Birim fiyat sıfırdan büyük ve geçerli sınırlar içinde olmalı.");
        if (model.Quantity > 0 && model.UnitPrice > 0 && model.Quantity <= 999999999999999.999m &&
            model.UnitPrice <= 99999999999999.9999m &&
            model.Quantity > 9999999999999999.99m / model.UnitPrice)
            ModelState.AddModelError(nameof(model.Quantity), "Satış toplamı sistemin tutar sınırını aşıyor.");
        if (model.Quantity > 0 && model.UnitPrice > 0 && model.Quantity <= 9999999999999999.99m / model.UnitPrice &&
            decimal.Round(model.Quantity * model.UnitPrice, 2, MidpointRounding.AwayFromZero) <= 0)
            ModelState.AddModelError(nameof(model.UnitPrice), "Satış toplamı kuruşa yuvarlandığında sıfırdan büyük olmalı.");
        if (!new[] { "Karayolu (Tır/Kamyon)", "Demiryolu (Tren)", "Denizyolu", "Havayolu", "Müşteri teslim alacak", "Diğer" }.Contains(model.ShipmentType))
            ModelState.AddModelError(nameof(model.ShipmentType), "Listeden bir sevkiyat türü seçin.");
        if (model.SaleDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(model.SaleDate), "Satış tarihi ileri bir tarih olamaz.");
        if (model.SaleDate == default)
            ModelState.AddModelError(nameof(model.SaleDate), "Satış tarihi girin.");
        if (model.Note?.Length > 300)
            ModelState.AddModelError(nameof(model.Note), "Açıklama en fazla 300 karakter olabilir.");

        if (ModelState.IsValid)
        {
            model.Message = await salesRepository.RecordAsync(model, userId, cancellationToken);
            if (model.Message is null)
            {
                TempData["Success"] = "Satış kaydedildi ve stoktan düşüldü.";
                return RedirectToAction(nameof(Open), new { tableName = "SATIŞ" });
            }
        }

        var page = await salesRepository.GetPageAsync(access.Okuma || isAdmin, true, cancellationToken);
        page.CustomerId = model.CustomerId;
        page.StockItemId = model.StockItemId;
        page.Quantity = model.Quantity;
        page.UnitPrice = model.UnitPrice;
        page.ShipmentType = model.ShipmentType;
        page.SaleDate = model.SaleDate;
        page.Note = model.Note;
        page.Message = model.Message;
        return View("Sales", page);
    }
}
