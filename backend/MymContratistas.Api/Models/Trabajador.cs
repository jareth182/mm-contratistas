namespace MymContratistas.Api.Models
{
    // Ficha de un colaborador de obra (HU10). Sus asignaciones a obras están en Cuadrilla (Colaborador).
    public class Trabajador
    {
        public const string Activo = "Activo";
        public const string Inactivo = "Inactivo";

        public static readonly string[] Especialidades = { "Maestro de Obra", "Operario", "Oficial", "Ayudante", "Peón" };

        public int Id { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = "DNI"; // DNI, CE
        public string Documento { get; set; } = string.Empty;
        public string Especialidad { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public decimal JornalDiario { get; set; }
        public string Estado { get; set; } = Activo;

        public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
    }
}
