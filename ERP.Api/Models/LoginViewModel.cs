using System.ComponentModel.DataAnnotations;

namespace ERP.Api.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress, StringLength(320)]
    [Display(Name = "E-posta")]
    public string Mail { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
