namespace MymContratistas.Api.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty; // Administrador, Gerente de Operaciones, Cliente
        public string Estado { get; set; } = "Activo"; // Activo, Inactivo
        
        // Para cumplir el criterio 4 de tu HU-01 (Seguridad)
        public int IntentosFallidos { get; set; } = 0;
        public DateTime? BloqueoHasta { get; set; }
        public string? TokenRecuperacion { get; set; }
        public DateTime? TokenExpiracion { get; set; }
    }
}