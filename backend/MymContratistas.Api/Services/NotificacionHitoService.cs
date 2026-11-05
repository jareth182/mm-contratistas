using System.Net;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    // Avisos por correo sobre los hitos de una obra: hito completado (HU13) y comentarios del cliente (HU22)
    public class NotificacionHitoService
    {
        private readonly IEmailService _email;
        private readonly ILogger<NotificacionHitoService> _logger;

        public NotificacionHitoService(IEmailService email, ILogger<NotificacionHitoService> logger)
        {
            _email = email;
            _logger = logger;
        }

        // Devuelve false si el correo no pudo enviarse; registrar el avance nunca depende de este envío
        public async Task<bool> HitoCompletadoAsync(Obra obra, Hito hito, string correoCliente)
        {
            try
            {
                await _email.EnviarAsync(correoCliente, $"Hito completado en su obra «{obra.Nombre}»", Cuerpo(obra, hito));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo notificar el hito {Hito} de la obra {Obra} a {Correo}", hito.Nombre, obra.Nombre, correoCliente);
                return false;
            }
        }

        // HU22: avisa a los destinatarios (Gerente de Operaciones) del comentario nuevo de un cliente.
        // Cada envío es independiente; devuelve true solo si hubo destinatarios y a todos les llegó.
        public async Task<bool> ComentarioDelClienteAsync(Obra obra, Hito hito, ComentarioHito comentario, IReadOnlyCollection<string> destinatarios)
        {
            if (destinatarios.Count == 0)
            {
                _logger.LogWarning("El comentario {Comentario} del hito {Hito} no tiene a quién notificar: no hay un Gerente de Operaciones activo", comentario.Id, hito.Nombre);
                return false;
            }
            var asunto = $"Nuevo comentario del cliente en «{hito.Nombre}» · {obra.Nombre}";
            var cuerpo = CuerpoComentario("Nuevo comentario de un cliente",
                $"<b>{WebUtility.HtmlEncode(comentario.AutorNombre)}</b> comentó en el hito <b>{WebUtility.HtmlEncode(hito.Nombre)}</b> de la obra <b>{WebUtility.HtmlEncode(obra.Nombre)}</b>:",
                comentario, "Ingrese al panel de administración, en el detalle del hito, para responder.");
            return await EnviarATodosAsync(destinatarios, asunto, cuerpo, $"comentario {comentario.Id} del hito {hito.Nombre}");
        }

        // HU22: avisa al cliente que el equipo respondió su comentario
        public async Task<bool> RespuestaAlClienteAsync(Obra obra, Hito hito, ComentarioHito respuesta, string correoCliente)
        {
            var asunto = $"Respondimos su comentario en «{hito.Nombre}» · {obra.Nombre}";
            var cuerpo = CuerpoComentario("Respuesta a su comentario",
                $"<b>{WebUtility.HtmlEncode(respuesta.AutorNombre)}</b> de M/M Contratistas respondió su comentario en el hito <b>{WebUtility.HtmlEncode(hito.Nombre)}</b> de la obra <b>{WebUtility.HtmlEncode(obra.Nombre)}</b>:",
                respuesta, "Ingrese a su panel de seguimiento, en el detalle del hito, para ver la conversación.");
            return await EnviarATodosAsync(new[] { correoCliente }, asunto, cuerpo, $"respuesta {respuesta.Id} del hito {hito.Nombre}");
        }

        private async Task<bool> EnviarATodosAsync(IEnumerable<string> destinatarios, string asunto, string cuerpo, string detalle)
        {
            var todosEnviados = true;
            foreach (var correo in destinatarios)
            {
                try { await _email.EnviarAsync(correo, asunto, cuerpo); }
                catch (Exception ex)
                {
                    todosEnviados = false;
                    _logger.LogError(ex, "No se pudo notificar el {Detalle} a {Correo}", detalle, correo);
                }
            }
            return todosEnviados;
        }

        private static string CuerpoComentario(string titulo, string presentacion, ComentarioHito comentario, string cierre) => $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>{titulo}</h2>
                <p>{presentacion}</p>
                <blockquote style='margin: 12px 0; padding: 8px 14px; border-left: 4px solid #F2A900; background: #F5F7F8;'>{WebUtility.HtmlEncode(comentario.Texto).ReplaceLineEndings("<br>")}</blockquote>
                <p>{cierre}</p>
            </div>";

        private static string Cuerpo(Obra obra, Hito hito) => $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>Avance de su obra</h2>
                <p>Se completó el hito <b>{WebUtility.HtmlEncode(hito.Nombre)}</b> de la obra <b>{WebUtility.HtmlEncode(obra.Nombre)}</b>.</p>
                <p>Fecha de término: {hito.FechaReal:dd/MM/yyyy}<br>Avance total de la obra: {obra.AvancePorcentaje}%</p>
                <p>Ingrese a su panel de seguimiento para ver las fotografías y el detalle del avance.</p>
            </div>";
    }
}
