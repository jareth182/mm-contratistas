using System.Net;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    // Envía el correo con el enlace para que una persona nueva defina su contraseña
    public class InvitacionService
    {
        public const int HorasVigencia = 72;

        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly ILogger<InvitacionService> _logger;

        public InvitacionService(IEmailService email, IConfiguration config, ILogger<InvitacionService> logger)
        {
            _email = email;
            _config = config;
            _logger = logger;
        }

        // El usuario debe llevar cargado su Rol. Devuelve false si el correo no pudo enviarse.
        public async Task<bool> EnviarAsync(Usuario usuario, string token)
        {
            try
            {
                var enlace = $"{_config["App:FrontendUrl"]}#nueva_password?token={token}";
                await _email.EnviarAsync(usuario.Correo, "Bienvenido a M/M Contratistas - defina su contraseña", Cuerpo(usuario, enlace));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar la invitación a {Correo}", usuario.Correo);
                return false;
            }
        }

        private static string Cuerpo(Usuario usuario, string enlace) => $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>Bienvenido a M/M Contratistas</h2>
                <p>Hola {WebUtility.HtmlEncode(usuario.NombreCompleto)},</p>
                <p>Se creó una cuenta para usted con el rol <b>{WebUtility.HtmlEncode(usuario.Rol.Nombre)}</b>. Haga clic en el siguiente botón para definir su contraseña:</p>
                <p><a href='{enlace}' style='background-color: #0A1929; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Definir mi contraseña</a></p>
                <p>Si el botón no funciona, copie y pegue esta dirección en su navegador:</p>
                <p style='color: #666; font-size: 12px;'>{enlace}</p>
                <p>El enlace vence en {HorasVigencia} horas.</p>
            </div>";
    }
}
