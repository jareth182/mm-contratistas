using System.ComponentModel.DataAnnotations.Schema;

namespace MymContratistas.Api.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Estado { get; set; } = "Activo"; // Activo, Inactivo

        public int RolId { get; set; }
        public Rol Rol { get; set; } = null!;

        // Control de acceso: bloqueo por intentos fallidos
        public int IntentosFallidos { get; set; } = 0;
        public DateTime? BloqueoHasta { get; set; }

        // Enlace de recuperación / definición de contraseña (se guarda el hash del token)
        public string? TokenRecuperacion { get; set; }
        public DateTime? TokenExpiracion { get; set; }

        [NotMapped]
        public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
    }
}
