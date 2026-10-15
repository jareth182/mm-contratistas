using Microsoft.AspNetCore.Identity;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Data
{
    // Crea los usuarios iniciales (uno por rol) cuando la tabla está vacía.
    public static class DbSeeder
    {
        public static void SembrarUsuarios(ApplicationDbContext context, IPasswordHasher<Usuario> hasher, string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || context.Usuarios.Any()) return;

            var iniciales = new[]
            {
                new Usuario { Nombres = "Maribel", Apellidos = "Minaya Arellano", Correo = "maribel.minaya@mym.pe", RolId = 1 },
                new Usuario { Nombres = "Marco", Apellidos = "Zapata Neyra", Correo = "marco.zapata@mym.pe", RolId = 2 },
                new Usuario { Nombres = "Rómulo", Apellidos = "Ccahuana Ayala", Correo = "romulo.ccahuana@mym.pe", RolId = 3 },
                new Usuario { Nombres = "Inversiones Del Río", Apellidos = "S.A.C.", Correo = "contacto@delrio.pe", RolId = 4 }
            };

            foreach (var u in iniciales)
                u.PasswordHash = hasher.HashPassword(u, password);

            context.Usuarios.AddRange(iniciales);
            context.SaveChanges();
        }
    }
}
