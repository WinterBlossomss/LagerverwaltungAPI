using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.Api.DTOs
{
    // [ApiController] checks these attributes automatically and answers 400 with the
    // list of problems before the controller method even runs.

    public record RegisterRequest(
        [Required, StringLength(20, MinimumLength = 3)] string Username,
        [Required, StringLength(100, MinimumLength = 8)] string Password);

    public record LoginRequest(
        [Required] string Username,
        [Required] string Password);

    public record LoginResponse(string Token, DateTime ExpiresAtUtc, int UserId, string Username, string Role);
}
