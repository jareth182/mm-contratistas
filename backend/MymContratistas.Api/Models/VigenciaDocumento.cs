using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    // Vigencia de un documento obligatorio del colaborador (SCTR o examen médico). Cada renovación es una fila nueva,
    // así se conserva el historial; la vigente es la de mayor fecha de vencimiento de su tipo.
    public class VigenciaDocumento
    {
        public const string Sctr = "SCTR";
        public const string ExamenMedico = "Examen Médico";
        public const int DiasPorVencer = 30;

        public static readonly string[] Tipos = { Sctr, ExamenMedico };

        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaVencimiento { get; set; }
        public DateTime FechaRegistro { get; set; }

        public int TrabajadorId { get; set; }
        [JsonIgnore]
        public Trabajador? Trabajador { get; set; }

        // Vigente, Por vencer (faltan 30 días o menos) o Vencido
        public static string EstadoAl(DateOnly vencimiento, DateOnly hoy) =>
            vencimiento < hoy ? "Vencido" : vencimiento <= hoy.AddDays(DiasPorVencer) ? "Por vencer" : "Vigente";
    }
}
