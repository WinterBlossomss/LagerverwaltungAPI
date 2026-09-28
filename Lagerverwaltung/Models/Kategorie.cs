using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class Kategorie
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int? Hersteller { get; set; }

    public int? ModelNum { get; set; }

    public virtual ICollection<Artikel> Artikels { get; set; } = new List<Artikel>();

    public virtual Hersteller? HerstellerNavigation { get; set; }
}
