using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Models;

public partial class Lagerbewegungen
{
    [ForeignKey("Artikel")]
    public virtual Artikel ArtikelNavigation { get; set; }
}