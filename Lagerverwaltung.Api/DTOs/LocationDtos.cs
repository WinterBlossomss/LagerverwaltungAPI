using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.Api.DTOs
{
    /// <summary>RaumTyp: "Hauptlager", "Umbuchungslager" or "Labor".</summary>
    public record CreateLocationRequest(
        [Required] string RaumTyp,
        [Required] int? BenutzerId,
        int? RaumNum);

    public record UpdateLocationRequest(int? RaumNum);

    public record AssignTeacherRequest([Required] int? BenutzerId);
}
