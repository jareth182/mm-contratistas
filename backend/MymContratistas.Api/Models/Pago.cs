using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Pago
    {
        public int Id { get; set; }
        public string ColaboradorNombre { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string FechaPago { get; set; } = string.Empty;
        public string Estado { get; set; } = "Registrado";
        public string? ConstanciaUrl { get; set; }

        public int ObraId { get; set; }
        [JsonIgnore]
        public Obra? Obra { get; set; }
    }
}