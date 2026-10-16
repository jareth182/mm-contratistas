using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Gasto
    {
        public int Id { get; set; }
        public string Fecha { get; set; } = string.Empty;
        public string TipoComprobante { get; set; } = string.Empty;
        public string NumeroComprobante { get; set; } = string.Empty;
        public string Concepto { get; set; } = string.Empty;
        public string Proveedor { get; set; } = string.Empty;
        public string PartidaNombre { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Estado { get; set; } = "Registrado"; // Registrado, Anulado
        public string? ComprobanteUrl { get; set; }
        public string? MotivoAnulacion { get; set; }

        public int ObraId { get; set; }
        [JsonIgnore]
        public Obra? Obra { get; set; }
    }
}