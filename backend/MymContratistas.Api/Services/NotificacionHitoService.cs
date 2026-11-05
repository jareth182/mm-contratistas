using System.Net;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    // Avisos del seguimiento de obra y del control documental: hito completado (HU13), comentarios (HU22) y
    // documentación por vencer (HU24). Cada aviso se envía por correo y, si el destinatario tiene cuenta, también
    // queda en su campana; todo pasa por NotificacionService, que lo registra y evita duplicados (HT-05).
    public class NotificacionHitoService
    {
        private const int LargoMensajeApp = 140;

        private readonly NotificacionService _notificaciones;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificacionHitoService> _logger;

        public NotificacionHitoService(NotificacionService notificaciones, ApplicationDbContext context, ILogger<NotificacionHitoService> logger)
        {
            _notificaciones = notificaciones;
            _context = context;
            _logger = logger;
        }

        private static string Resumir(string texto) =>
            texto.Length <= LargoMensajeApp ? texto : texto[..(LargoMensajeApp - 1)].TrimEnd() + "…";

        // Devuelve false si el correo no pudo enviarse; registrar el avance nunca depende de este envío
        public async Task<bool> HitoCompletadoAsync(Obra obra, Hito hito, string? correoCliente)
        {
            var clave = $"hito:{hito.Id}";
            if (obra.ClienteId != null)
            {
                var cuentas = await _context.Usuarios.Where(u => u.ClienteId == obra.ClienteId && u.Estado == "Activo").Select(u => new { u.Id, u.Correo }).ToListAsync();
                foreach (var cuenta in cuentas)
                    await _notificaciones.AvisarEnAppAsync(new SolicitudNotificacion(Notificacion.TipoHitoCompletado,
                        $"Hito completado: {hito.Nombre}", $"{obra.Nombre} · avance de la obra {obra.AvancePorcentaje}%", obra.Id, hito.Id, clave), cuenta.Id, cuenta.Correo);
            }

            var resultado = await _notificaciones.EnviarCorreoAsync(new SolicitudNotificacion(Notificacion.TipoHitoCompletado,
                $"Hito completado en su obra «{obra.Nombre}»", null, obra.Id, hito.Id, clave), correoCliente, null, Cuerpo(obra, hito));
            return ResultadoNotificacion.Entregado(resultado);
        }

        // HU22: avisa a los destinatarios (Gerente de Operaciones) del comentario nuevo de un cliente.
        // Cada envío es independiente; devuelve true solo si hubo correos y a todos les llegó.
        public async Task<bool> ComentarioDelClienteAsync(Obra obra, Hito hito, ComentarioHito comentario, IReadOnlyCollection<DestinatarioNotificacion> destinatarios)
        {
            if (destinatarios.Count == 0)
            {
                _logger.LogWarning("El comentario {Comentario} del hito {Hito} no tiene a quién notificar: no hay un Gerente de Operaciones activo", comentario.Id, hito.Nombre);
                return false;
            }
            var clave = $"comentario:{comentario.Id}";
            var cuerpo = CuerpoComentario("Nuevo comentario de un cliente",
                $"<b>{WebUtility.HtmlEncode(comentario.AutorNombre)}</b> comentó en el hito <b>{WebUtility.HtmlEncode(hito.Nombre)}</b> de la obra <b>{WebUtility.HtmlEncode(obra.Nombre)}</b>:",
                comentario, "Ingrese al panel de administración, en el detalle del hito, para responder.");
            var app = new SolicitudNotificacion(Notificacion.TipoComentarioCliente, $"Nuevo comentario de {comentario.AutorNombre}",
                $"{hito.Nombre} · {obra.Nombre}: {Resumir(comentario.Texto)}", obra.Id, hito.Id, clave);
            var correo = new SolicitudNotificacion(Notificacion.TipoComentarioCliente, $"Nuevo comentario del cliente en «{hito.Nombre}» · {obra.Nombre}", null, obra.Id, hito.Id, clave);
            return await AvisarATodosAsync(destinatarios, app, correo, cuerpo);
        }

        // HU22: avisa al cliente que el equipo respondió su comentario
        public async Task<bool> RespuestaAlClienteAsync(Obra obra, Hito hito, ComentarioHito respuesta, DestinatarioNotificacion cliente)
        {
            var clave = $"respuesta:{respuesta.Id}";
            var cuerpo = CuerpoComentario("Respuesta a su comentario",
                $"<b>{WebUtility.HtmlEncode(respuesta.AutorNombre)}</b> de M/M Contratistas respondió su comentario en el hito <b>{WebUtility.HtmlEncode(hito.Nombre)}</b> de la obra <b>{WebUtility.HtmlEncode(obra.Nombre)}</b>:",
                respuesta, "Ingrese a su panel de seguimiento, en el detalle del hito, para ver la conversación.");
            var app = new SolicitudNotificacion(Notificacion.TipoRespuestaPersonal, "El equipo respondió su comentario",
                $"{hito.Nombre} · {obra.Nombre}: {Resumir(respuesta.Texto)}", obra.Id, hito.Id, clave);
            var correo = new SolicitudNotificacion(Notificacion.TipoRespuestaPersonal, $"Respondimos su comentario en «{hito.Nombre}» · {obra.Nombre}", null, obra.Id, hito.Id, clave);
            return await AvisarATodosAsync(new[] { cliente }, app, correo, cuerpo);
        }

        // HU24: resumen diario de documentación vencida o por vencer para el Gerente de Operaciones.
        // Devuelve a cuántos destinatarios les quedó entregado el correo (en este envío o en uno anterior del mismo día);
        // un fallo con uno no impide el envío a los demás.
        public async Task<int> AlertasDocumentalesAsync(IReadOnlyCollection<DestinatarioNotificacion> destinatarios, IReadOnlyList<AlertaDocumental> alertas, DateOnly hoy)
        {
            var vencidos = alertas.Count(a => a.Estado == "Vencido");
            var porVencer = alertas.Count - vencidos;
            var resumen = $"{vencidos} vencido{(vencidos == 1 ? "" : "s")}, {porVencer} por vencer";
            var clave = $"alertas:{hoy:yyyy-MM-dd}";
            var filas = string.Concat(alertas.Select(a => $@"<tr>
                    <td style='padding:6px 10px;border-bottom:1px solid #ddd'>{WebUtility.HtmlEncode(a.Colaborador)}</td>
                    <td style='padding:6px 10px;border-bottom:1px solid #ddd'>{WebUtility.HtmlEncode(a.Documento)}</td>
                    <td style='padding:6px 10px;border-bottom:1px solid #ddd'>{a.Vencimiento:dd/MM/yyyy}</td>
                    <td style='padding:6px 10px;border-bottom:1px solid #ddd;text-align:right'>{a.DiasRestantes}</td>
                    <td style='padding:6px 10px;border-bottom:1px solid #ddd;color:{(a.Estado == "Vencido" ? "#B3402A" : "#9A6B00")}'><b>{a.Estado}</b></td></tr>"));
            var cuerpo = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>Documentación del personal</h2>
                <p>Estos colaboradores tienen el SCTR o el examen médico ocupacional vencido o por vencer en los próximos {VigenciaDocumento.DiasPorVencer} días:</p>
                <table style='border-collapse:collapse;font-size:14px'>
                    <thead><tr style='background:#F5F7F8;text-align:left'><th style='padding:6px 10px'>Colaborador</th><th style='padding:6px 10px'>Documento</th><th style='padding:6px 10px'>Vencimiento</th><th style='padding:6px 10px'>Días restantes</th><th style='padding:6px 10px'>Estado</th></tr></thead>
                    <tbody>{filas}</tbody>
                </table>
                <p>Un colaborador con documentación vencida no puede asignarse a una cuadrilla. Ingrese al panel de administración, en Personal, para registrar las renovaciones.</p>
            </div>";
            var app = new SolicitudNotificacion(Notificacion.TipoAlertaDocumental, "Documentación del personal", $"{resumen}. Revise las alertas para registrar las renovaciones.", null, null, clave);
            var correo = new SolicitudNotificacion(Notificacion.TipoAlertaDocumental, $"Documentación del personal al {hoy:dd/MM/yyyy}: {resumen}", null, null, null, clave);

            var entregados = 0;
            foreach (var d in destinatarios)
            {
                if (d.UsuarioId != null) await _notificaciones.AvisarEnAppAsync(app, d.UsuarioId.Value, d.Correo);
                if (ResultadoNotificacion.Entregado(await _notificaciones.EnviarCorreoAsync(correo, d.Correo, d.UsuarioId, cuerpo))) entregados++;
            }
            return entregados;
        }

        // Aviso en la app y correo para cada destinatario; true si hubo al menos un correo entregado y ninguno falló
        private async Task<bool> AvisarATodosAsync(IReadOnlyCollection<DestinatarioNotificacion> destinatarios, SolicitudNotificacion app, SolicitudNotificacion correo, string cuerpoHtml)
        {
            var entregados = 0;
            var fallidos = 0;
            foreach (var d in destinatarios)
            {
                if (d.UsuarioId != null) await _notificaciones.AvisarEnAppAsync(app, d.UsuarioId.Value, d.Correo);
                var resultado = await _notificaciones.EnviarCorreoAsync(correo, d.Correo, d.UsuarioId, cuerpoHtml);
                if (ResultadoNotificacion.Entregado(resultado)) entregados++;
                else if (resultado == ResultadoNotificacion.Fallido) fallidos++;
            }
            return entregados > 0 && fallidos == 0;
        }

        private static string Cuerpo(Obra obra, Hito hito) => $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>Avance de su obra</h2>
                <p>Se completó el hito <b>{WebUtility.HtmlEncode(hito.Nombre)}</b> de la obra <b>{WebUtility.HtmlEncode(obra.Nombre)}</b>.</p>
                <p>Fecha de término: {hito.FechaReal:dd/MM/yyyy}<br>Avance total de la obra: {obra.AvancePorcentaje}%</p>
                <p>Ingrese a su panel de seguimiento para ver las fotografías y el detalle del avance.</p>
            </div>";

        private static string CuerpoComentario(string titulo, string presentacion, ComentarioHito comentario, string cierre) => $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>{titulo}</h2>
                <p>{presentacion}</p>
                <blockquote style='margin: 12px 0; padding: 8px 14px; border-left: 4px solid #F2A900; background: #F5F7F8;'>{WebUtility.HtmlEncode(comentario.Texto).ReplaceLineEndings("<br>")}</blockquote>
                <p>{cierre}</p>
            </div>";
    }
}
