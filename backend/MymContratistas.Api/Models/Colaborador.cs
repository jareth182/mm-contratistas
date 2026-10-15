using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Colaborador
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string Especialidad { get; set; } = string.Empty;
        public decimal Jornal { get; set; }
        public string EstadoSctr { get; set; } = "Vigente"; // Vigente, Por vencer, Vencido
        public string FechaIngresoObra { get; set; } = string.Empty;

        // Ficha del personal (HU10); permite conservar su historial de obras
        public int? TrabajadorId { get; set; }
        [JsonIgnore]
        public Trabajador? Trabajador { get; set; }

        // Relación con Obra
        public int? ObraId { get; set; }
        [JsonIgnore]
        public Obra? Obra { get; set; }
    }
}