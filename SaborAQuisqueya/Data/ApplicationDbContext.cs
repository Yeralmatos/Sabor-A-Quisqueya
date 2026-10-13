using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Models;

namespace SaborAQuisqueya.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // TABLAS DE LA BASE DE DATOS

        public DbSet<Usuario> Usuarios { get; set; }

        public DbSet<Restaurante> Restaurantes { get; set; }

        public DbSet<Horario> Horarios { get; set; }

        public DbSet<Mesa> Mesas { get; set; }

        public DbSet<Reserva> Reservas { get; set; }

        public DbSet<CategoriaPlato> CategoriasPlatos { get; set; }

        public DbSet<Plato> Platos { get; set; }


        // CONFIGURACIÓN DE RELACIONES

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Evitar correos electrónicos duplicados
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Correo)
                .IsUnique();

            // Evitar números de mesa duplicados
            modelBuilder.Entity<Mesa>()
                .HasIndex(m => new { m.RestauranteId, m.Numero })
                .IsUnique();

            // Evitar horarios duplicados por día
            modelBuilder.Entity<Horario>()
                .HasIndex(h => new { h.RestauranteId, h.DiaSemana })
                .IsUnique();

            // Relación entre reservas y usuarios
            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Usuario)
                .WithMany(u => u.Reservas)
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relación entre reservas y restaurante
            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Restaurante)
                .WithMany(rest => rest.Reservas)
                .HasForeignKey(r => r.RestauranteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relación entre reservas y mesas
            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Mesa)
                .WithMany(m => m.Reservas)
                .HasForeignKey(r => r.MesaId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}