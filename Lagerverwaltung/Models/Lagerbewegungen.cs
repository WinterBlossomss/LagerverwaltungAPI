using System;
using System.Collections.Generic;

namespace Lagerverwaltung.Models;

public partial class Lagerbewegungen
{
    public int Id { get; set; }

    public DateOnly Datum { get; set; }

    public int Bewegung { get; set; }

    public int Artikel { get; set; }

    public bool? IsDone { get; set; }

    public int? ArtikelMenge { get; set; }

    public int Benutzer { get; set; }

    public int VonRaum { get; set; }

    public int ZuRaum { get; set; }

    public virtual Benutzer BenutzerNavigation { get; set; } = null!;

    public virtual BewegungArt BewegungNavigation { get; set; } = null!;

    public virtual Raum VonRaumNavigation { get; set; } = null!;

    public virtual Raum ZuRaumNavigation { get; set; } = null!;
}
