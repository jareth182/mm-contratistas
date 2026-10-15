using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Obra> Obras { get; set; }
        public DbSet<Hito> Hitos { get; set; } 
        public DbSet<Colaborador> Colaboradores { get; set; } 
        public DbSet<Documento> Documentos { get; set; } 
        public DbSet<Partida> Partidas { get; set; } 
        public DbSet<Gasto> Gastos { get; set; } 
        public DbSet<Pago> Pagos { get; set; } // Nueva línea
    }
}