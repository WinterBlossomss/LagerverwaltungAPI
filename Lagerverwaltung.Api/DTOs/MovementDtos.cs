using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.Api.DTOs
{
    /// <summary>
    /// Used for POST /api/transfers (Hauptlager -> Labor) and POST /api/returns (Labor -> Umbuchungslager).
    /// The client only says WHICH lab, article and amount. Source, target and movement type
    /// are fixed by the stored procedure, so nobody can book into a wrong room.
    /// </summary>
    public record MovementRequest(
        [Required] int? LaborId,
        [Required] int? ArtikelId,
        [Required, Range(1, 100000)] int? Menge);

    public record CreatedResponse(int Id);
}
