namespace MymContratistas.Api.Models
{
    // Servicio del catálogo que se muestra en el sitio público (HU09)
    public class Servicio
    {
        public const string Publicado = "Publicado";
        public const string Oculto = "Oculto";

        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        // Descripción breve para el listado del catálogo
        public string Descripcion { get; set; } = string.Empty;
        // Un elemento por línea
        public string Beneficios { get; set; } = string.Empty;
        public string Caracteristicas { get; set; } = string.Empty;
        public int Orden { get; set; }
        public string Estado { get; set; } = Publicado;
    }
}
