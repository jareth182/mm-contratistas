using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    public record ResultadoEnvioAlertas(string Estado, int Alertas, int Enviados)
    {
        public const string AntesDeLaHora = "Antes de la hora";
        public const string YaEnviado = "Ya enviado";
        public const string SinAlertas = "Sin alertas";
        public const string SinDestinatarios = "Sin destinatarios";
        public const string Enviado = "Enviado";
        public const string Fallido = "Fallido";
    }

    // Aviso diario por correo al Gerente de Operaciones con la documentación vencida o por vencer (HU24-T4)
    public class AlertasDocumentalesService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificacionHitoService _notificacion;
        private readonly ILogger<AlertasDocumentalesService> _logger;

        public AlertasDocumentalesService(ApplicationDbContext context, NotificacionHitoService notificacion, ILogger<AlertasDocumentalesService> logger)
        {
            _context = context;
            _notificacion = notificacion;
            _logger = logger;
        }

        // Envía el aviso del día si ya pasó la hora programada y todavía no se envió hoy; es seguro llamarlo varias veces al día.
        // Si el correo falla con todos los destinatarios no se registra el envío, para reintentarlo en la siguiente llamada.
        public async Task<ResultadoEnvioAlertas> EnviarSiCorrespondeAsync(DateTime ahora, TimeOnly horaEnvio)
        {
            if (TimeOnly.FromDateTime(ahora) < horaEnvio) return new(ResultadoEnvioAlertas.AntesDeLaHora, 0, 0);

            var hoy = DateOnly.FromDateTime(ahora);
            if (await _context.EnviosAlertaDocumental.AnyAsync(e => e.Fecha == hoy)) return new(ResultadoEnvioAlertas.YaEnviado, 0, 0);

            var alertas = await VigenciasDocumentales.AlertasAsync(_context, hoy);
            if (alertas.Count == 0)
            {
                await RegistrarAsync(hoy, 0, 0);
                return new(ResultadoEnvioAlertas.SinAlertas, 0, 0);
            }

            var correos = await _context.Usuarios
                .Where(u => u.Rol.Nombre == NombresRol.Gerente && u.Estado == "Activo" && u.Correo != "")
                .Select(u => u.Correo).ToListAsync();
            if (correos.Count == 0)
            {
                _logger.LogWarning("Hay {Alertas} alerta(s) de documentación del personal pero no hay un Gerente de Operaciones activo a quien avisar", alertas.Count);
                await RegistrarAsync(hoy, alertas.Count, 0);
                return new(ResultadoEnvioAlertas.SinDestinatarios, alertas.Count, 0);
            }

            var enviados = await _notificacion.AlertasDocumentalesAsync(correos, alertas, hoy);
            if (enviados == 0) return new(ResultadoEnvioAlertas.Fallido, alertas.Count, 0);

            await RegistrarAsync(hoy, alertas.Count, enviados);
            return new(ResultadoEnvioAlertas.Enviado, alertas.Count, enviados);
        }

        private async Task RegistrarAsync(DateOnly hoy, int alertas, int destinatarios)
        {
            _context.EnviosAlertaDocumental.Add(new EnvioAlertaDocumental { Fecha = hoy, EnviadoEn = DateTime.UtcNow, Alertas = alertas, Destinatarios = destinatarios });
            await _context.SaveChangesAsync();
        }
    }
}
