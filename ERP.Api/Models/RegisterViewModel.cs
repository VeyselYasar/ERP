using System.ComponentModel.DataAnnotations;

namespace ERP.Api.Models;

public sealed class RegisterViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Ad")]
    public string Ad { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Soyad")]
    public string SoyAd { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    [Display(Name = "E-posta")]
    public string Mail { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8), DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password)), DataType(DataType.Password)]
    [Display(Name = "Parola tekrar")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
