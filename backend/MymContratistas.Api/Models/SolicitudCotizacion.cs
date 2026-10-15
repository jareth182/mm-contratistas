using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    // Solicitud de cotización enviada desde el sitio público (HU07)
    public class SolicitudCotizacion
    {
        public int Id { get; set; }
        // SC-AAAA-NNNN, correlativo por año
        public string Codigo { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public int? ServicioId { get; set; }
        [JsonIgnore]
        public Servicio? Servicio { get; set; }
        // Nombre del servicio al momento de la solicitud (se conserva aunque el servicio cambie)
        public string ServicioNombre { get; set; } = string.Empty;
        public string Distrito { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        // Si el aviso por correo al Gerente de Operaciones salió bien
        public bool CorreoNotificado { get; set; }

        public List<AdjuntoCotizacion> Adjuntos { get; set; } = new List<AdjuntoCotizacion>();
    }

    public class AdjuntoCotizacion
    {
        public int Id { get; set; }
        public int SolicitudCotizacionId { get; set; }
        [JsonIgnore]
        public SolicitudCotizacion? Solicitud { get; set; }
        // Nombre con el que se guardó en Uploads/cotizaciones
        public string Archivo { get; set; } = string.Empty;
        public string NombreOriginal { get; set; } = string.Empty;
        public long Tamano { get; set; }
    }
}
