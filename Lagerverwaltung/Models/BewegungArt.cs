using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class BewegungArt
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Lagerbewegungen> Lagerbewegungens { get; set; } = new List<Lagerbewegungen>();
}
