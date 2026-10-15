using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Obra
    {
        public const string EnEjecucion = "En Ejecución";
        public const string Finalizado = "Finalizado";

        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        // Nombre del cliente tal como se mostraba al registrar la obra; se completa desde ClienteId
        public string Cliente { get; set; } = string.Empty;
        public string Estado { get; set; } = EnEjecucion;
        public int AvancePorcentaje { get; set; }

        // Expediente del proyecto (HU11)
        public int? ClienteId { get; set; }
        [JsonIgnore]
        public Cliente? ClienteRegistro { get; set; }
        public string Ubicacion { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFinEstimada { get; set; }
        public DateOnly? FechaCierre { get; set; }

        // Nueva relación: Una obra contiene una lista de hitos
        public List<Hito> Hitos { get; set; } = new List<Hito>();
        public List<Colaborador> Cuadrilla { get; set; } = new List<Colaborador>();
        public List<Documento> Documentos { get; set; } = new List<Documento>();
        public List<Partida> Partidas { get; set; } = new List<Partida>();
        public List<Gasto> Gastos { get; set; } = new List<Gasto>();
        public List<Pago> Pagos { get; set; } = new List<Pago>();
    }

}
