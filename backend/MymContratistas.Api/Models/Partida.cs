using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Partida
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal MontoAsignado { get; set; }

        public int ObraId { get; set; }
        [JsonIgnore]
        public Obra? Obra { get; set; }
    }
}