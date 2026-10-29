using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Pago
    {
        // Los pagos al personal se imputan a esta partida del presupuesto
        public const string PartidaManoDeObra = "Mano de obra";

        public int Id { get; set; }
        public int? ColaboradorId { get; set; }
        public string ColaboradorNombre { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string FechaPago { get; set; } = string.Empty;
        public string Estado { get; set; } = "Registrado"; // Registrado, Anulado
        // Partida a la que se imputó el pago
        public string PartidaNombre { get; set; } = PartidaManoDeObra;
        // Nombre original de la constancia, tal como lo ve el usuario
        public string? ConstanciaUrl { get; set; }
        // Nombre con el que se guardó en Uploads/pagos; vacío en los pagos anteriores a HU18
        [JsonIgnore]
        public string Archivo { get; set; } = string.Empty;
        public string? MotivoAnulacion { get; set; }
        public DateTime? FechaAnulacion { get; set; }

        public int ObraId { get; set; }
        [JsonIgnore]
        public Obra? Obra { get; set; }
    }
}
