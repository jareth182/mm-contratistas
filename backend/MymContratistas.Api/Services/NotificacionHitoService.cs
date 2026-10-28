using System.Net;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    // Avisa al cliente por correo cuando se completa un hito de su obra (HU13)
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

        private static string Cuerpo(Obra obra, Hito hito) => $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>Avance de su obra</h2>
                <p>Se completó el hito <b>{WebUtility.HtmlEncode(hito.Nombre)}</b> de la obra <b>{WebUtility.HtmlEncode(obra.Nombre)}</b>.</p>
                <p>Fecha de término: {hito.FechaReal:dd/MM/yyyy}<br>Avance total de la obra: {obra.AvancePorcentaje}%</p>
                <p>Ingrese a su panel de seguimiento para ver las fotografías y el detalle del avance.</p>
            </div>";
    }
}
