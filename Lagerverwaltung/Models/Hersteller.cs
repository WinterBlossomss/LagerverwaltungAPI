using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class Hersteller
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Kategorie> Kategories { get; set; } = new List<Kategorie>();
}
