using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class Rolle
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Benutzer> Benutzers { get; set; } = new List<Benutzer>();
}
