using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class RaumArtikel
{
    public int Raum { get; set; }

    public int Art { get; set; }

    public int Menge { get; set; }

    public virtual Artikel ArtNavigation { get; set; } = null!;

    public virtual Raum RaumNavigation { get; set; } = null!;
}
