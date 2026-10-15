namespace MymContratistas.Api.Models
{
    // Cliente al que se le ejecuta una obra (HU11)
    public class Cliente
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = "DNI"; // DNI, RUC
        public string Documento { get; set; } = string.Empty;
        public string? Correo { get; set; }
        public string? Telefono { get; set; }
    }
}
