using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Data // <-- Aquí estaba el detalle
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Obra> Obras { get; set; }
        public DbSet<Hito> Hitos { get; set; }
        public DbSet<Colaborador> Cuadrilla { get; set; }
        public DbSet<Documento> Documentos { get; set; }
        public DbSet<Partida> Partidas { get; set; }
        public DbSet<Gasto> Gastos { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<ProyectoPortafolio> Portafolio { get; set; }
        public DbSet<ImagenProyecto> ImagenesPortafolio { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Servicio> Servicios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Usuario>().HasIndex(u => u.Correo).IsUnique();
            modelBuilder.Entity<Rol>().HasIndex(r => r.Nombre).IsUnique();
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol).WithMany().HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProyectoPortafolio>()
                .HasMany(p => p.Imagenes).WithOne(i => i.Proyecto).HasForeignKey(i => i.ProyectoPortafolioId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Cliente>().HasIndex(c => c.Documento).IsUnique();
            modelBuilder.Entity<Obra>()
                .HasOne(o => o.ClienteRegistro).WithMany().HasForeignKey(o => o.ClienteId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Usuario>()
                .HasOne<Cliente>().WithMany().HasForeignKey(u => u.ClienteId).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Rol>().HasData(
                new Rol { Id = 1, Nombre = NombresRol.Administrador },
                new Rol { Id = 2, Nombre = NombresRol.Gerente },
                new Rol { Id = 3, Nombre = NombresRol.Supervisor },
                new Rol { Id = 4, Nombre = NombresRol.Cliente });
        }
    }
}
