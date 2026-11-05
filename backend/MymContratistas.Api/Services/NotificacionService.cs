using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    public record DestinatarioNotificacion(int? UsuarioId, string? Correo);

    // Qué se avisa. «Clave» identifica el evento (p. ej. «comentario:15»): con ella el mismo aviso no se repite
    // al mismo destinatario. Sin clave no hay control de duplicados.
    public record SolicitudNotificacion(string Tipo, string Titulo, string? Mensaje = null, int? ObraId = null, int? HitoId = null, string? Clave = null);

    public static class ResultadoNotificacion
    {
        public const string Enviado = "Enviado";
        public const string Duplicado = "Duplicado";
        public const string Fallido = "Fallido";
        public const string SinDestino = "SinDestino";

        // El aviso quedó entregado ahora o en un envío anterior
        public static bool Entregado(string resultado) => resultado is Enviado or Duplicado;
    }

    // Servicio unificado de notificaciones (HT-05): envía por correo (EmailService) o deja un aviso en la aplicación,
    // registra cada envío en la tabla Notificaciones y evita enviar dos veces el mismo aviso al mismo destinatario.
    public class NotificacionService
    {
        // Un envío «Pendiente» más antiguo que esto se considera interrumpido (p. ej. la aplicación se cerró) y puede reintentarse
        public static readonly TimeSpan PendienteCaducaEn = TimeSpan.FromMinutes(10);
        private const int LargoMaximoError = 300;

        private readonly ApplicationDbContext _context;
        private readonly IEmailService _email;
        private readonly ILogger<NotificacionService> _logger;

        public NotificacionService(ApplicationDbContext context, IEmailService email, ILogger<NotificacionService> logger)
        {
            _context = context;
            _email = email;
            _logger = logger;
        }

        // Envía un correo y lo registra. Nunca lanza: devuelve un ResultadoNotificacion.
        public async Task<string> EnviarCorreoAsync(SolicitudNotificacion solicitud, string? correo, int? usuarioId, string cuerpoHtml)
        {
            if (string.IsNullOrWhiteSpace(correo)) return ResultadoNotificacion.SinDestino;
            correo = correo.Trim();

            var registro = await ReservarAsync(solicitud, Notificacion.CanalCorreo, usuarioId, correo);
            if (registro == null) return ResultadoNotificacion.Duplicado;

            try
            {
                await _email.EnviarAsync(correo, solicitud.Titulo, cuerpoHtml);
                registro.Estado = Notificacion.Enviado;
                registro.EnviadoEn = DateTime.UtcNow;
                registro.Error = null;
                await _context.SaveChangesAsync();
                return ResultadoNotificacion.Enviado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el aviso {Tipo} a {Correo}", solicitud.Tipo, correo);
                registro.Estado = Notificacion.Fallido;
                registro.Error = Recortar(ex.Message);
                await GuardarSinFallarAsync();
                return ResultadoNotificacion.Fallido;
            }
        }

        // Deja un aviso en la campana del usuario. Nunca lanza.
        public async Task<string> AvisarEnAppAsync(SolicitudNotificacion solicitud, int usuarioId, string? correo = null)
        {
            try
            {
                var registro = await ReservarAsync(solicitud, Notificacion.CanalApp, usuarioId, correo ?? string.Empty);
                if (registro == null) return ResultadoNotificacion.Duplicado;

                registro.Estado = Notificacion.Enviado;
                registro.EnviadoEn = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return ResultadoNotificacion.Enviado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar el aviso {Tipo} para el usuario {Usuario}", solicitud.Tipo, usuarioId);
                return ResultadoNotificacion.Fallido;
            }
        }

        // Reserva el aviso en la tabla antes de enviarlo. Devuelve null si ya se envió (o está enviándose) a ese destinatario.
        // El índice único de la clave resuelve la carrera entre dos procesos que intenten enviar lo mismo a la vez.
        private async Task<Notificacion?> ReservarAsync(SolicitudNotificacion s, string canal, int? usuarioId, string destinatario)
        {
            var clave = s.Clave == null ? null : $"{s.Tipo}|{s.Clave}|{canal}|{(canal == Notificacion.CanalApp && usuarioId != null ? "u" + usuarioId : destinatario.ToLowerInvariant())}";
            var ahora = DateTime.UtcNow;

            if (clave != null)
            {
                var previo = await _context.Notificaciones.FirstOrDefaultAsync(n => n.ClaveDeduplicacion == clave);
                if (previo != null)
                {
                    var interrumpido = previo.Estado == Notificacion.Pendiente && ahora - previo.Fecha > PendienteCaducaEn;
                    if (previo.Estado == Notificacion.Enviado || (previo.Estado == Notificacion.Pendiente && !interrumpido)) return null;

                    // Falló antes o quedó interrumpido: se reintenta sobre el mismo registro
                    previo.Estado = Notificacion.Pendiente;
                    previo.Intentos++;
                    previo.Fecha = ahora;
                    await _context.SaveChangesAsync();
                    return previo;
                }
            }

            var nuevo = new Notificacion
            {
                Tipo = s.Tipo, Canal = canal, UsuarioId = usuarioId, Destinatario = destinatario,
                Titulo = s.Titulo, Mensaje = s.Mensaje ?? string.Empty, ObraId = s.ObraId, HitoId = s.HitoId,
                ClaveDeduplicacion = clave, Estado = Notificacion.Pendiente, Intentos = 1, Fecha = ahora
            };
            _context.Notificaciones.Add(nuevo);
            try
            {
                await _context.SaveChangesAsync();
                return nuevo;
            }
            catch (DbUpdateException) when (clave != null)
            {
                // Otro proceso reservó la misma clave justo antes: ese es el que envía
                _context.Entry(nuevo).State = EntityState.Detached;
                return null;
            }
        }

        private async Task GuardarSinFallarAsync()
        {
            try { await _context.SaveChangesAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "No se pudo registrar el resultado de un envío"); }
        }

        private static string Recortar(string texto) => texto.Length <= LargoMaximoError ? texto : texto[..LargoMaximoError];
    }
}
