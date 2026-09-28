using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class Raum
{
    public int Id { get; set; }

    public int RaumTyp { get; set; }

    public int Benutzer { get; set; }

    public int? RaumNum { get; set; }

    public virtual Benutzer BenutzerNavigation { get; set; } = null!;

    public virtual ICollection<Lagerbewegungen> LagerbewegungenVonRaumNavigations { get; set; } = new List<Lagerbewegungen>();

    public virtual ICollection<Lagerbewegungen> LagerbewegungenZuRaumNavigations { get; set; } = new List<Lagerbewegungen>();

    public virtual ICollection<RaumArtikel> RaumArtikels { get; set; } = new List<RaumArtikel>();

    public virtual RaumTyp RaumTypNavigation { get; set; } = null!;
}
