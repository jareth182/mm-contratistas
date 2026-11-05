using System.Text.Json.Serialization;

namespace MymContratistas.Api.Models
{
    // Comentario sobre un hito (HU22). El cliente abre la conversación y el personal responde:
    // una respuesta apunta, por ComentarioPadreId, al comentario del cliente que contesta.
    public class ComentarioHito
    {
        public const int LongitudMaxima = 500;

        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }

        // Se guarda el nombre tal como era al comentar, para que el historial no cambie si se edita la cuenta
        public int AutorUsuarioId { get; set; }
        public string AutorNombre { get; set; } = string.Empty;
        public bool EsPersonal { get; set; }

        public int? ComentarioPadreId { get; set; }
        [JsonIgnore]
        public ComentarioHito? ComentarioPadre { get; set; }

        public int HitoId { get; set; }
        [JsonIgnore]
        public Hito? Hito { get; set; }

        // Devuelve el mensaje de error o null si el texto es válido
        public static string? Validar(string? texto)
        {
            var limpio = texto?.Trim() ?? string.Empty;
            if (limpio.Length == 0) return "Escriba el comentario antes de enviarlo.";
            if (limpio.Length > LongitudMaxima) return $"El comentario no puede superar los {LongitudMaxima} caracteres.";
            return null;
        }

        // Lo que ven el cliente y el personal: sin identificadores internos de cuenta
        public object Vista() => new
        {
            id = Id,
            texto = Texto,
            fecha = Fecha,
            autor = AutorNombre,
            esPersonal = EsPersonal,
            respondeA = ComentarioPadreId
        };
    }
}
