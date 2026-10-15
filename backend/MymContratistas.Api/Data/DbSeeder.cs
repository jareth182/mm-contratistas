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

        // Catálogo inicial del sitio público (solo si aún no hay servicios)
        public static void SembrarServicios(ApplicationDbContext context)
        {
            if (context.Servicios.Any()) return;

            context.Servicios.AddRange(
                new Servicio
                {
                    Orden = 1, Nombre = "Construcción de edificios",
                    Descripcion = "Ejecución integral de edificaciones multifamiliares y comerciales, desde la cimentación hasta los acabados.",
                    Beneficios = "Un solo responsable para toda la obra\nSeguimiento en línea del avance\nPersonal con SCTR vigente",
                    Caracteristicas = "Estructuras de concreto armado\nAlbañilería y tabiquería\nInstalaciones y acabados"
                },
                new Servicio
                {
                    Orden = 2, Nombre = "Remodelación y acabados",
                    Descripcion = "Renovación de interiores, tabiquería, pisos, pintura y carpintería para viviendas y oficinas.",
                    Beneficios = "Plazos cortos y coordinados\nMínima interrupción de su actividad",
                    Caracteristicas = "Pisos y enchapes\nPintura y carpintería\nDrywall y tabiquería"
                },
                new Servicio
                {
                    Orden = 3, Nombre = "Obras civiles menores",
                    Descripcion = "Ampliaciones, cercos perimétricos, losas y trabajos puntuales de albañilería.",
                    Beneficios = "Atención de trabajos puntuales\nPresupuesto por partidas",
                    Caracteristicas = "Ampliaciones\nCercos perimétricos\nLosas y veredas"
                },
                new Servicio
                {
                    Orden = 4, Nombre = "Supervisión de obra",
                    Descripcion = "Coordinación con el ingeniero residente y control del cumplimiento de plazos y seguridad.",
                    Beneficios = "Control de plazos y calidad\nCumplimiento de normas de seguridad",
                    Caracteristicas = "Informes periódicos de avance\nControl de documentación del personal"
                });
            context.SaveChanges();
        }
    }
}
