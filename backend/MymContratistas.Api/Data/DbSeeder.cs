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
                new Usuario { NombreCompleto = "Maribel Minaya Arellano", Correo = "maribel.minaya@mym.pe", Rol = Roles.Administrador },
                new Usuario { NombreCompleto = "Marco Zapata Neyra", Correo = "marco.zapata@mym.pe", Rol = Roles.Gerente },
                new Usuario { NombreCompleto = "Rómulo Ccahuana Ayala", Correo = "romulo.ccahuana@mym.pe", Rol = Roles.Supervisor },
                new Usuario { NombreCompleto = "Inversiones Del Río S.A.C.", Correo = "contacto@delrio.pe", Rol = Roles.Cliente }
            };

            foreach (var u in iniciales)
                u.PasswordHash = hasher.HashPassword(u, password);

            context.Usuarios.AddRange(iniciales);
            context.SaveChanges();
        }
    }
}
