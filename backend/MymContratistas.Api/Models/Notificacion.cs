namespace MymContratistas.Api.Models
{
    // Registro de cada aviso del sistema (HT-05): correos y avisos dentro de la aplicación.
    // No guarda el cuerpo de los correos: solo su asunto, destinatario y resultado.
    public class Notificacion
    {
        public const string CanalCorreo = "Correo";
        public const string CanalApp = "App";

        public const string Pendiente = "Pendiente";
        public const string Enviado = "Enviado";
        public const string Fallido = "Fallido";

        // Tipos de aviso
        public const string TipoHitoCompletado = "HitoCompletado";
        public const string TipoComentarioCliente = "ComentarioCliente";
        public const string TipoRespuestaPersonal = "RespuestaPersonal";
        public const string TipoAlertaDocumental = "AlertaDocumental";

        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Canal { get; set; } = CanalCorreo;
        // Cuenta destinataria; obligatoria en los avisos de la app
        public int? UsuarioId { get; set; }
        public string Destinatario { get; set; } = string.Empty;
        // Asunto del correo o título del aviso en la app
        public string Titulo { get; set; } = string.Empty;
        // Texto corto del aviso en la app
        public string Mensaje { get; set; } = string.Empty;
        public int? ObraId { get; set; }
        public int? HitoId { get; set; }

        // Identifica el evento que originó el aviso y a quién va dirigido: el mismo evento no se avisa dos veces
        public string? ClaveDeduplicacion { get; set; }

        public string Estado { get; set; } = Pendiente;
        public string? Error { get; set; }
        public int Intentos { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime? EnviadoEn { get; set; }

        public bool Leida { get; set; }
        public DateTime? LeidaEn { get; set; }
    }
}
