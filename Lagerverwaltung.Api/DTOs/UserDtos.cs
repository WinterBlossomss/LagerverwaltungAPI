using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.Api.DTOs
{
    /// <summary>
    /// PUT /api/users/me. Leave a field null to keep it.
    /// CurrentPassword is required for every change, so a stolen token alone can't take over the account.
    /// </summary>
    public record UpdateOwnProfileRequest(
        [Required] string CurrentPassword,
        [StringLength(20, MinimumLength = 3)] string? NewUsername,
        [StringLength(100, MinimumLength = 8)] string? NewPassword);

    public record ChangeRoleRequest([Required] string Role);
}
