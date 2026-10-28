using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    public class Documento
    {
        public static readonly string[] Clasificaciones = { "Contrato", "Permiso", "Plano", "Comprobante", "Otro" };

        public int Id { get; set; }
        // Nombre original del archivo, tal como lo ve el usuario
        public string Nombre { get; set; } = string.Empty;
        // Nombre con el que se guardó en Uploads/documentos; vacío en los documentos anteriores a HU15
        [JsonIgnore]
        public string Archivo { get; set; } = string.Empty;
        public string Clasificacion { get; set; } = "Otro";
        public string FechaCarga { get; set; } = string.Empty;
        public bool VisibleCliente { get; set; }

        public int ObraId { get; set; }
        [JsonIgnore]
        public Obra? Obra { get; set; }
    }
}