using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Models
{
    public partial class Raum
    {
        [NotMapped]
        public string Anzeigename =>
            RaumTypNavigation?.Name == "Labor"
                ? (RaumNum != null ? $"Raum {RaumNum}" : $"Labor {ID}")
                : RaumTypNavigation?.Name ?? $"Raum {ID}";
    }
}