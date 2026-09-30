using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Services
{
    public static class SeedService
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LagerverwaltungssystemContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Benutzer>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedIndicator>>();

            try
            {
                logger.LogInformation("Seeding roles...");
                var adminRolle = await GetOrAddRolleAsync(context, Rollen.Admin);
                var lehrerRolle = await GetOrAddRolleAsync(context, Rollen.Lehrer);

                logger.LogInformation("Seeding users...");
                await AddUserAsync(context, hasher, logger, "ADMIN", "Admin123!", adminRolle);
                await AddUserAsync(context, hasher, logger, "Dennis", "123", lehrerRolle);
                await AddUserAsync(context, hasher, logger, "Emily", "123", lehrerRolle);
                await AddUserAsync(context, hasher, logger, "Michael", "123", lehrerRolle);

                logger.LogInformation("Seeding Stammdaten...");
                foreach (var t in new[] { RaumTypen.Hauptlager, RaumTypen.Umbuchungslager, RaumTypen.Labor })
                    await GetOrAddRaumTypAsync(context, t);
                foreach (var a in new[] { BewegungArten.Zugang, BewegungArten.Ausgabe, BewegungArten.Uebernahme, BewegungArten.Rueckgabe, BewegungArten.Einbuchung })
                    await GetOrAddBewegungArtAsync(context, a);

                // Retrieve users for seeding rooms and articles
                var admin = await context.Benutzers.FirstAsync(b => b.Username == "ADMIN");
                var dennis = await context.Benutzers.FirstAsync(b => b.Username == "Dennis");
                var emily = await context.Benutzers.FirstAsync(b => b.Username == "Emily");
                var michael = await context.Benutzers.FirstAsync(b => b.Username == "Michael");

                logger.LogInformation("Seeding rooms...");
                var haupt = await GetOrAddRaumAsync(context, RaumTypen.Hauptlager, null, admin);
                var umb = await GetOrAddRaumAsync(context, RaumTypen.Umbuchungslager, null, admin);
                var r309 = await GetOrAddRaumAsync(context, RaumTypen.Labor, 309, dennis);
                var r310 = await GetOrAddRaumAsync(context, RaumTypen.Labor, 310, emily);
                var r311 = await GetOrAddRaumAsync(context, RaumTypen.Labor, 311, michael);
                var r312 = await GetOrAddRaumAsync(context, RaumTypen.Labor, 312, dennis);

                // Seed articles only if there are no articles in the database
                if (!await context.Artikels.AnyAsync())
                {
                    logger.LogInformation("Seeding Testdaten...");
                    await SeedTestdatenAsync(context, admin, dennis, emily, michael, haupt, umb, r309, r310, r311, r312);
                }

                logger.LogInformation("Seeding completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        private static async Task SeedTestdatenAsync(LagerverwaltungssystemContext context,
            Benutzer admin, Benutzer dennis, Benutzer emily, Benutzer michael,
            Raum haupt, Raum umb, Raum r309, Raum r310, Raum r311, Raum r312)
        {
            // Seed categories
            var kategorien = new Dictionary<string, Kategorie>();
            foreach (var name in new[] { "PCs", "Notebooks", "Monitore", "Netzwerk", "Beamer", "Eingabegeräte", "Kabel & Adapter", "Sonstiges" })
            {
                var k = new Kategorie { Name = name };
                context.Kategories.Add(k);
                kategorien[name] = k;
            }
            await context.SaveChangesAsync();

            // Get "BewegungArts" as a dictionary for easy access
            var arten = await context.BewegungArts.ToDictionaryAsync(a => a.Name);
            var heute = DateOnly.FromDateTime(DateTime.Now);

            // Seed articles
            // (ID, Bezeichnung, Kategorie, Seriennummer)
            var artikel = new (int Id, string Name, string Kat, int? Sn)[]
            {
                (1, "Dell OptiPlex 7010", "PCs", 30418822),
                (2, "Dell OptiPlex 7010", "PCs", 30418823),
                (3, "Lenovo ThinkPad L14", "Notebooks", 71203344),
                (4, "Lenovo ThinkPad L14", "Notebooks", 71203345),
                (5, "HP ProBook 450", "Notebooks", 55100921),
                (6, "Dell P2422H", "Monitore", 90211470),
                (7, "Dell P2422H", "Monitore", 90211471),
                (8, "Dell P2422H", "Monitore", 90211472),
                (9, "Dell P2422H", "Monitore", 90211473),
                (10, "Dell P2422H", "Monitore", 90211474),
                (11, "Cisco Catalyst 2960", "Netzwerk", 88342019),
                (12, "Cisco Catalyst 2960", "Netzwerk", 88342020),
                (13, "Ubiquiti U6 Lite", "Netzwerk", 61044812),
                (14, "Epson EB-W49", "Beamer", 40077315),
                (15, "Dell P2422H", "Monitore", 90211468),
                (16, "Lenovo ThinkPad L14", "Notebooks", 71203346),
                (17, "HP ProBook 450", "Notebooks", 55100922),
                (18, "Logitech K120 Tastatur", "Eingabegeräte", null),
                (19, "Logitech B100 Maus", "Eingabegeräte", null),
                (20, "Netzwerkkabel Cat6 2 m", "Kabel & Adapter", null),
                (21, "HDMI-Kabel 3 m", "Kabel & Adapter", null),
                (22, "USB-C-auf-HDMI-Adapter", "Kabel & Adapter", null),
                (23, "Steckdosenleiste 6-fach", "Sonstiges", null),
            };
            foreach (var a in artikel)
                context.Artikels.Add(new Artikel { ID = a.Id, Name = a.Name, SerienNum = a.Sn, KategorieNavigation = kategorien[a.Kat] });

            // Aktueller Bestand (Artikel, Raum, Menge)
            var bestand = new (int Art, Raum Raum, int Menge)[]
            {
                (1, haupt, 1), (2, haupt, 1), (5, haupt, 1), (6, haupt, 1), (7, haupt, 1), (8, haupt, 1), (12, haupt, 1),
                (18, haupt, 24), (19, haupt, 30), (20, haupt, 20), (21, haupt, 12), (22, haupt, 3),
                (3, r309, 1), (4, r309, 1), (18, r309, 16),
                (11, r310, 1), (15, r310, 1), (19, r310, 12),
                (13, r311, 1), (20, r311, 20),
                (14, r312, 1), (16, r312, 1), (23, r312, 4),
                (17, umb, 1), (22, umb, 2),
            };
            foreach (var b in bestand)
                context.RaumArtikels.Add(new RaumArtikel { Art = b.Art, Raum = b.Raum.ID, Menge = b.Menge });

            void Buche(string art, int artikelId, int menge, Raum von, Raum zu, Benutzer user, bool done, int tageZurueck) =>
                context.Lagerbewegungens.Add(new Lagerbewegungen
                {
                    Datum = heute.AddDays(-tageZurueck),
                    Bewegung = arten[art].ID,
                    Artikel = artikelId,
                    ArtikelMenge = menge,
                    VonRaum = von.ID,
                    ZuRaum = zu.ID,
                    Benutzer = user.ID,
                    IsDone = done
                });

            // Offene Übergaben (Bestand ist bereits aus dem Hauptlager ausgebucht)
            var offen = new (int Art, int Menge, Raum Zu, int Tage)[] { (9, 1, r309, 1), (10, 1, r309, 1), (18, 6, r310, 4), (20, 10, r312, 2) };

            // Zugänge - alles, was heute irgendwo liegt oder unterwegs ist, kam einmal ins Hauptlager
            foreach (var a in artikel)
            {
                var summe = bestand.Where(b => b.Art == a.Id).Sum(b => b.Menge) + offen.Where(o => o.Art == a.Id).Sum(o => o.Menge);
                if (summe > 0) Buche(BewegungArten.Zugang, a.Id, summe, haupt, haupt, admin, true, 30);
            }

            // Abgeschlossene Ausgaben + Übernahmen für den Laborbestand
            foreach (var b in bestand.Where(b => b.Raum.ID != haupt.ID && b.Raum.ID != umb.ID))
            {
                var lehrer = b.Raum.Benutzer == dennis.ID ? dennis : b.Raum.Benutzer == emily.ID ? emily : michael;
                Buche(BewegungArten.Ausgabe, b.Art, b.Menge, haupt, b.Raum, admin, true, 28);
                Buche(BewegungArten.Uebernahme, b.Art, b.Menge, haupt, b.Raum, lehrer, true, 27);
            }
            foreach (var o in offen)
                Buche(BewegungArten.Ausgabe, o.Art, o.Menge, haupt, o.Zu, admin, false, o.Tage);

            // Rückgaben: HP ProBook offen, Adapter bereits entgegengenommen (bereit zur Einbuchung)
            Buche(BewegungArten.Ausgabe, 17, 1, haupt, r310, admin, true, 20);
            Buche(BewegungArten.Uebernahme, 17, 1, haupt, r310, emily, true, 20);
            Buche(BewegungArten.Rueckgabe, 17, 1, r310, umb, emily, false, 1);
            Buche(BewegungArten.Ausgabe, 22, 2, haupt, r311, admin, true, 20);
            Buche(BewegungArten.Uebernahme, 22, 2, haupt, r311, michael, true, 19);
            Buche(BewegungArten.Rueckgabe, 22, 2, r311, umb, michael, true, 3);

            await context.SaveChangesAsync();
        }

        private static async Task<Rolle> GetOrAddRolleAsync(LagerverwaltungssystemContext context, string name)
        {
            var rolle = await context.Rolles.FirstOrDefaultAsync(r => r.Name == name);
            if (rolle == null)
            {
                rolle = new Rolle { Name = name };
                context.Rolles.Add(rolle);
                await context.SaveChangesAsync();   // sets Rolle.ID after saving
            }
            return rolle;
        }

        private static async Task<RaumTyp> GetOrAddRaumTypAsync(LagerverwaltungssystemContext context, string name)
        {
            var typ = await context.RaumTyps.FirstOrDefaultAsync(r => r.Name == name);
            if (typ == null)
            {
                typ = new RaumTyp { Name = name };
                context.RaumTyps.Add(typ);
                await context.SaveChangesAsync();
            }
            return typ;
        }

        private static async Task<BewegungArt> GetOrAddBewegungArtAsync(LagerverwaltungssystemContext context, string name)
        {
            var art = await context.BewegungArts.FirstOrDefaultAsync(r => r.Name == name);
            if (art == null)
            {
                art = new BewegungArt { Name = name };
                context.BewegungArts.Add(art);
                await context.SaveChangesAsync();
            }
            return art;
        }

        private static async Task<Raum> GetOrAddRaumAsync(LagerverwaltungssystemContext context, string typName, int? raumNum, Benutzer verantwortlich)
        {
            var typ = await context.RaumTyps.FirstAsync(t => t.Name == typName);
            var raum = await context.Raums.FirstOrDefaultAsync(r => r.RaumTyp == typ.ID && r.RaumNum == raumNum);
            if (raum == null)
            {
                raum = new Raum { RaumTyp = typ.ID, RaumNum = raumNum, Benutzer = verantwortlich.ID };
                context.Raums.Add(raum);
                await context.SaveChangesAsync();
            }
            return raum;
        }

        private static async Task AddUserAsync(
            LagerverwaltungssystemContext context,
            IPasswordHasher<Benutzer> hasher,
            ILogger logger,
            string username, string password, Rolle rolle)
        {
            if (await context.Benutzers.AnyAsync(b => b.Username == username))
                return;

            var benutzer = new Benutzer
            {
                Username = username,
                RolleNavigation = rolle
            };
            benutzer.Password = hasher.HashPassword(benutzer, password);

            context.Benutzers.Add(benutzer);
            await context.SaveChangesAsync();
            logger.LogInformation("Created user {Username} with role {Role}", username, rolle.Name);
        }

        // Marker class to indicate that the seeding process has been completed. This is used for logging purposes.
        private sealed class SeedIndicator
        { }
    }
}