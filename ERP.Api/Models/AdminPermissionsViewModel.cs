using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ERP.Api.Models;

public sealed class AdminPermissionsViewModel
{
    [Range(1, int.MaxValue)]
    public int SelectedUserId { get; set; }

    public List<SelectListItem> Users { get; set; } = [];
    public List<TablePermissionViewModel> Permissions { get; set; } = [];
}

public sealed class TablePermissionViewModel
{
    [Required, StringLength(128)]
    public string TableName { get; set; } = string.Empty;

    public bool Okuma { get; set; }
    public bool Yazma { get; set; }
    public bool Silme { get; set; }
    public bool Onizleme { get; set; }
}

public sealed class TableAccessViewModel
{
    public string TableName { get; set; } = string.Empty;
    public bool Okuma { get; set; }
    public bool Yazma { get; set; }
    public bool Silme { get; set; }
    public bool Onizleme { get; set; }
    public bool IsAdmin { get; set; }
}

public sealed class StockPageViewModel
{
    public string TableName { get; set; } = "STOK";
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public int? PackagingRecipeId { get; set; }
    public decimal Quantity { get; set; }
    public decimal MinimumQuantity { get; set; }
    public string Unit { get; set; } = "Adet";
    public string Warehouse { get; set; } = "Depo 1";
    public List<StockItemViewModel> Items { get; set; } = [];
    public List<PackagingRecipeItemViewModel> AvailableRecipes { get; set; } = [];
}

public sealed class StockItemViewModel
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal MinimumQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string? PackagingRecipeName { get; set; }
}

public sealed class PackagingRecipePageViewModel
{
    public string TableName { get; set; } = "AMBALAJ REÇETESİ";
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string ProductType { get; set; } = "Mamul";
    public string Unit { get; set; } = "Adet";
    public decimal QuantityPerPackage { get; set; }
    public int PackagesPerPallet { get; set; }
    public List<PackagingRecipeItemViewModel> Recipes { get; set; } = [];
}

public sealed class PackagingRecipeItemViewModel
{
    public int Id { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityPerPackage { get; set; }
    public int PackagesPerPallet { get; set; }
    public decimal QuantityPerPallet { get; set; }
}

public sealed class TransferPageViewModel
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public int? StockItemId { get; set; }
    public string ToWarehouse { get; set; } = "Depo 2";
    public decimal Quantity { get; set; }
    public string? Message { get; set; }
    public List<TransferSourceOption> Sources { get; set; } = [];
    public List<TransferHistoryItem> Transfers { get; set; } = [];
}

public sealed class TransferSourceOption
{
    public int StockItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string? RecipeName { get; set; }
}

public sealed class TransferHistoryItem
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string FromWarehouse { get; set; } = string.Empty;
    public string ToWarehouse { get; set; } = string.Empty;
    public string TransferredBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class StockMovementPageViewModel
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public int? StockItemId { get; set; }
    public string MovementType { get; set; } = "Giriş";
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = "Satın alma";
    public string? Note { get; set; }
    public string? Message { get; set; }
    public List<TransferSourceOption> Sources { get; set; } = [];
    public List<StockMovementItemViewModel> Movements { get; set; } = [];
}

public sealed class StockMovementItemViewModel
{
    public DateTime CreatedAt { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
}

public sealed class ProductionPageViewModel
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? PackagingRecipeId { get; set; }
    public decimal Quantity { get; set; }
    public string Warehouse { get; set; } = "Depo 1";
    public DateTime ProducedAt { get; set; } = DateTime.Now;
    public string? Note { get; set; }
    public string? Message { get; set; }
    public List<PackagingRecipeItemViewModel> Recipes { get; set; } = [];
    public List<ProductionRecordItemViewModel> Records { get; set; } = [];
}

public sealed class ProductionRecordItemViewModel
{
    public DateTime ProducedAt { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string ProducedBy { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class StockReportPageViewModel
{
    public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime ToDate { get; set; } = DateTime.Today;
    public List<StockReportSummaryItem> Summary { get; set; } = [];
    public List<StockLowItem> LowStock { get; set; } = [];
    public List<StockMovementItemViewModel> Movements { get; set; } = [];
}

public sealed class StockReportSummaryItem
{
    public string Warehouse { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public int ProductCount { get; set; }
}

public sealed class StockLowItem
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal MinimumQuantity { get; set; }
}

public sealed class CustomerPageViewModel
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public CustomerCardViewModel Form { get; set; } = new();
    public List<CustomerCardViewModel> Customers { get; set; } = [];
}

public sealed class CustomerCardViewModel
{
    public int CustomerId { get; set; }

    [Required, StringLength(30)]
    [Display(Name = "Müşteri kodu")]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "Müşteri türü")]
    public string CustomerType { get; set; } = "Şirket";

    [Required, StringLength(200)]
    [Display(Name = "Unvan / ad soyad")]
    public string Title { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Vergi dairesi")]
    public string? TaxOffice { get; set; }

    [StringLength(30)]
    [Display(Name = "Vergi numarası / T.C. kimlik no")]
    public string? TaxNumber { get; set; }

    [StringLength(150)]
    [Display(Name = "İlgili kişi")]
    public string? ContactName { get; set; }

    [StringLength(30)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(320)]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [StringLength(500)]
    [Display(Name = "Fatura adresi")]
    public string? BillingAddress { get; set; }

    [StringLength(500)]
    [Display(Name = "Teslimat adresi")]
    public string? DeliveryAddress { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public sealed class SalePageViewModel
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public int? CustomerId { get; set; }
    public int? StockItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string ShipmentType { get; set; } = "Karayolu (Tır/Kamyon)";
    public DateTime SaleDate { get; set; } = DateTime.Today;
    public string? Note { get; set; }
    public string? Message { get; set; }
    public List<SaleCustomerOption> Customers { get; set; } = [];
    public List<SaleStockOption> StockItems { get; set; } = [];
    public List<SaleHistoryItem> Sales { get; set; } = [];
}

public sealed class SaleCustomerOption
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public sealed class SaleStockOption
{
    public int StockItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
}

public sealed class SaleHistoryItem
{
    public int SaleId { get; set; }
    public DateTime SaleDate { get; set; }
    public string CustomerTitle { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string ShipmentType { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class SalesReportPageViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? CustomerId { get; set; }
    public string? ProductName { get; set; }
    public string? ShipmentType { get; set; }
    public List<SaleCustomerOption> Customers { get; set; } = [];
    public List<SaleHistoryItem> Sales { get; set; } = [];
    public decimal TotalAmount { get; set; }
}
