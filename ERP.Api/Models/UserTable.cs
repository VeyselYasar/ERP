using System.ComponentModel.DataAnnotations;

namespace ERP.Api.Models;

public sealed class UserTable
{
    public int UserId { get; set; }

    // The database [Password] column contains only this encoded hash.
    public string PasswordHash { get; set; } = string.Empty;

    public bool AktifPasif { get; set; } = true;

    [Required, StringLength(100)]
    public string Ad { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string SoyAd { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    public string Mail { get; set; } = string.Empty;
}
