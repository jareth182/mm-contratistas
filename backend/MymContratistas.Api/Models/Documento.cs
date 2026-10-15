using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Documento
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Clasificacion { get; set; } = "Otro";
        public string FechaCarga { get; set; } = string.Empty;
        public bool VisibleCliente { get; set; }

        public int ObraId { get; set; }
        [JsonIgnore]
        public Obra? Obra { get; set; }
    }
}