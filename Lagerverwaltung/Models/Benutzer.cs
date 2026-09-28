using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class Benutzer
{
    public int Id { get; set; }

    public int Rolle { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public virtual ICollection<Lagerbewegungen> Lagerbewegungens { get; set; } = new List<Lagerbewegungen>();

    public virtual ICollection<Raum> Raums { get; set; } = new List<Raum>();

    public virtual Rolle RolleNavigation { get; set; } = null!;
}
