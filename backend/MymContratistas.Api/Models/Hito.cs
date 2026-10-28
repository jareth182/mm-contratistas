using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Hito
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public int Peso { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public DateOnly? FechaPlanificada { get; set; }
        public DateOnly? FechaReal { get; set; }
        public int AvancePorcentaje { get; set; }
        
        // Nuevos campos para registrar el avance
        public string? DescripcionAvance { get; set; }
        public string? FotoUrl { get; set; } // Guardaremos la ruta o nombre del archivo
        // Relación: Este hito pertenece a una Obra
        public int ObraId { get; set; }
        
        [JsonIgnore] // Oculta la obra padre al enviar datos para evitar un bucle infinito
        public Obra? Obra { get; set; }

        // Historial de avances registrados (HU13)
        [JsonIgnore]
        public List<AvanceHito> Avances { get; set; } = new List<AvanceHito>();
    }
}