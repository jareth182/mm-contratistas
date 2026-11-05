namespace MymContratistas.Api.Models
{
    // Registro del aviso diario de documentación por vencer (HU24). Una fila por día: evita enviar dos veces
    // el mismo día si la aplicación se reinicia, y permite enviarlo tarde si estaba apagada a la hora programada.
    public class EnvioAlertaDocumental
    {
        public int Id { get; set; }
        public DateOnly Fecha { get; set; }
        public DateTime EnviadoEn { get; set; }
        public int Alertas { get; set; }
        public int Destinatarios { get; set; }
    }
}
