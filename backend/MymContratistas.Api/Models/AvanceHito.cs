using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    // Avance registrado sobre un hito (HU13). El hito conserva el porcentaje vigente; aquí queda el historial.
    public class AvanceHito
    {
        public int Id { get; set; }
        public int Porcentaje { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Responsable { get; set; } = string.Empty;

        public int HitoId { get; set; }
        [JsonIgnore]
        public Hito? Hito { get; set; }

        public List<FotoAvance> Fotos { get; set; } = new List<FotoAvance>();
    }

    public class FotoAvance
    {
        public int Id { get; set; }
        public int AvanceHitoId { get; set; }
        [JsonIgnore]
        public AvanceHito? Avance { get; set; }

        // Nombre con el que se guardó el archivo en Uploads/avances
        public string Archivo { get; set; } = string.Empty;
        public string NombreOriginal { get; set; } = string.Empty;
    }
}
