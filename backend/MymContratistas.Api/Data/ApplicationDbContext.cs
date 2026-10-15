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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario { Id = 1, NombreCompleto = "Marco Zapata", Correo = "marco.zapata@mym.pe", Password = "123", Rol = "Gerente de Operaciones" },
                new Usuario { Id = 2, NombreCompleto = "Inversiones Del Río", Correo = "contacto@delrio.pe", Password = "123", Rol = "Cliente" }
            );
        }
    }
}