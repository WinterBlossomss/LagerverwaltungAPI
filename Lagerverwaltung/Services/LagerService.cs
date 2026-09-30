namespace Lagerverwaltung.Services
{
    public class LagerService
    {
        /// <summary>
        /// Defines the roles used in the inventory management system.
        /// </summary>
        public static class Rollen
        {
            public const string Admin = "Administrator";
            public const string Lehrer = "LehrerIn";
        }

        /// <summary>
        /// Defines the types of rooms in the inventory system.
        /// </summary>
        public static class RaumTypen
        {
            public const string Hauptlager = "Hauptlager";
            public const string Umbuchungslager = "Umbuchungslager";
            public const string Labor = "Labor";
        }

        /// <summary>
        /// Defines the types of movements that can occur in the inventory system.
        /// </summary>
        public static class BewegungArten
        {
            public const string Zugang = "Zugang"; // Neuer Artikel ins Hauptlager
            public const string Ausgabe = "Ausgabe"; // Hauptlager -> Labor, IsDone = Übernahme bestätigt
            public const string Uebernahme = "Übernahme"; // Digitale Bestätigung durch die LehrerIn
            public const string Rueckgabe = "Rückgabe"; // Labor -> Umbuchungslager, IsDone = vom Admin entgegengenommen
            public const string Einbuchung = "Einbuchung"; // Umbuchungslager -> Hauptlager
        }

        /// <summary>
        /// Custom exception class for LagerService-related errors.
        /// </summary>
        public class LagerException : Exception
        {
            public LagerException(string message) : base(message)
            {
            }
        }
    }
}