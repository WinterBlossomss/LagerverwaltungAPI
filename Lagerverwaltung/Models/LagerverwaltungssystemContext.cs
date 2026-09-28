using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Models;

public partial class LagerverwaltungssystemContext : DbContext
{
    public LagerverwaltungssystemContext()
    {
    }

    public LagerverwaltungssystemContext(DbContextOptions<LagerverwaltungssystemContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Artikel> Artikels { get; set; }

    public virtual DbSet<Benutzer> Benutzers { get; set; }

    public virtual DbSet<BewegungArt> BewegungArts { get; set; }

    public virtual DbSet<Hersteller> Herstellers { get; set; }

    public virtual DbSet<Kategorie> Kategories { get; set; }

    public virtual DbSet<Lagerbewegungen> Lagerbewegungens { get; set; }

    public virtual DbSet<Raum> Raums { get; set; }

    public virtual DbSet<RaumArtikel> RaumArtikels { get; set; }

    public virtual DbSet<RaumTyp> RaumTyps { get; set; }

    public virtual DbSet<Rolle> Rolles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=Lagerverwaltungssystem;Integrated Security=True;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Artikel>(entity =>
        {
            entity.ToTable("Artikel");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("ID");
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(d => d.KategorieNavigation).WithMany(p => p.Artikels)
                .HasForeignKey(d => d.Kategorie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Artikel_Kategorie");
        });

        modelBuilder.Entity<Benutzer>(entity =>
        {
            entity.ToTable("Benutzer");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Password).HasMaxLength(455);
            entity.Property(e => e.Username).HasMaxLength(20);

            entity.HasOne(d => d.RolleNavigation).WithMany(p => p.Benutzers)
                .HasForeignKey(d => d.Rolle)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Benutzer_Rolle");
        });

        modelBuilder.Entity<BewegungArt>(entity =>
        {
            entity.ToTable("BewegungArt");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name).HasMaxLength(20);
        });

        modelBuilder.Entity<Hersteller>(entity =>
        {
            entity.ToTable("Hersteller");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Kategorie>(entity =>
        {
            entity.ToTable("Kategorie");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(d => d.HerstellerNavigation).WithMany(p => p.Kategories)
                .HasForeignKey(d => d.Hersteller)
                .HasConstraintName("FK_Kategorie_Hersteller");
        });

        modelBuilder.Entity<Lagerbewegungen>(entity =>
        {
            entity.ToTable("Lagerbewegungen");

            entity.Property(e => e.Id).HasColumnName("ID");

            entity.HasOne(d => d.BenutzerNavigation).WithMany(p => p.Lagerbewegungen)
                .HasForeignKey(d => d.Benutzer)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lagerbewegungen_Benutzer");

            entity.HasOne(d => d.BewegungNavigation).WithMany(p => p.Lagerbewegungens)
                .HasForeignKey(d => d.Bewegung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lagerbewegungen_BewegungArt");

            entity.HasOne(d => d.VonRaumNavigation).WithMany(p => p.VonRaumNavigation)
                .HasForeignKey(d => d.VonRaum)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lagerbewegungen_Raum");

            entity.HasOne(d => d.ZuRaumNavigation).WithMany(p => p.ZuRaumNavigation)
                .HasForeignKey(d => d.ZuRaum)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lagerbewegungen_Raum1");
        });

        modelBuilder.Entity<Raum>(entity =>
        {
            entity.ToTable("Raum");

            entity.Property(e => e.Id).HasColumnName("ID");

            entity.HasOne(d => d.BenutzerNavigation).WithMany(p => p.Raeume)
                .HasForeignKey(d => d.Benutzer)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Raum_Benutzer");

            entity.HasOne(d => d.RaumTypNavigation).WithMany(p => p.Raeume)
                .HasForeignKey(d => d.RaumTyp)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Raum_Raum");
        });

        modelBuilder.Entity<RaumArtikel>(entity =>
        {
            entity.HasKey(e => new { e.Raum, e.Art });

            entity.ToTable("RaumArtikel");

            entity.HasOne(d => d.ArtNavigation).WithMany(p => p.RaumArtikels)
                .HasForeignKey(d => d.Art)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RaumArtikel_Artikel");

            entity.HasOne(d => d.RaumNavigation).WithMany(p => p.RaumArtikelNavigation)
                .HasForeignKey(d => d.Raum)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RaumArtikel_Raum");
        });

        modelBuilder.Entity<RaumTyp>(entity =>
        {
            entity.ToTable("RaumTyp");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name).HasMaxLength(20);
        });

        modelBuilder.Entity<Rolle>(entity =>
        {
            entity.ToTable("Rolle");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Name).HasMaxLength(20);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
