using System.ComponentModel.DataAnnotations;

namespace ERP.Api.Models;

public sealed class ChangePasswordViewModel
{
    public bool RememberMe { get; set; }

    [Required, DataType(DataType.Password), Display(Name = "Mevcut şifre")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), DataType(DataType.Password), Display(Name = "Yeni şifre")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Yeni şifreler eşleşmiyor."), Display(Name = "Yeni şifreyi tekrar girin")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class CompanyProfileViewModel
{
    public bool Exists { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Şirket unvanı")]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Vergi dairesi")]
    public string? TaxOffice { get; set; }

    [StringLength(30)]
    [Display(Name = "Vergi numarası")]
    public string? TaxNumber { get; set; }

    [StringLength(30)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(320)]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [StringLength(500)]
    [Display(Name = "Şirket adresi")]
    public string? CompanyAddress { get; set; }

    [StringLength(200)]
    [Display(Name = "Fabrika / tesis adı")]
    public string? FactoryName { get; set; }

    [StringLength(500)]
    [Display(Name = "Fabrika / tesis adresi")]
    public string? FactoryAddress { get; set; }
}
