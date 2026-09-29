using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Models
{
    public partial class LagerverwaltungssystemContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
