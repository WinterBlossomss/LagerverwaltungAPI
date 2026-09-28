using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class Artikel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int? SerienNum { get; set; }

    public int Kategorie { get; set; }

    public virtual Kategorie KategorieNavigation { get; set; } = null!;

    public virtual ICollection<RaumArtikel> RaumArtikels { get; set; } = new List<RaumArtikel>();
}
