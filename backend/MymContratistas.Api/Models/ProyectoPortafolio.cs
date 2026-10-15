using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    // Proyecto histórico que se muestra en el sitio público (HU08)
    public class ProyectoPortafolio
    {
        public const string Borrador = "Borrador";
        public const string Publicado = "Publicado";

        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string TipoObra { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public int Anio { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = Borrador;

        // Servicio con el que se relaciona en el sitio público (HU06)
        public int? ServicioId { get; set; }
        [JsonIgnore]
        public Servicio? Servicio { get; set; }

        public List<ImagenProyecto> Imagenes { get; set; } = new List<ImagenProyecto>();
    }

    public class ImagenProyecto
    {
        public int Id { get; set; }
        public int ProyectoPortafolioId { get; set; }
        [JsonIgnore]
        public ProyectoPortafolio? Proyecto { get; set; }

        // Nombre con el que se guardó el archivo en Uploads/portafolio
        public string Archivo { get; set; } = string.Empty;
        public string NombreOriginal { get; set; } = string.Empty;
        public bool EsPrincipal { get; set; }
    }
}
