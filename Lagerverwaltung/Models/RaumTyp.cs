using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class RaumTyp
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Raum> Raeume { get; set; } = new List<Raum>();
}