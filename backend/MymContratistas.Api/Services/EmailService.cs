using System.Net;
using System.Net.Mail;

namespace MymContratistas.Api.Services
{
    public interface IEmailService
    {
        Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml);
    }

    // Envía por SMTP con los datos de .env (Smtp__*). Sin configuración, escribe el correo en el log (útil en desarrollo).
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            var usuario = _config["Smtp:User"];
            var clave = _config["Smtp:Password"];

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(clave))
            {
                _logger.LogWarning("SMTP no configurado. Correo para {Destinatario} | {Asunto}\n{Cuerpo}", destinatario, asunto, cuerpoHtml);
                return;
            }

            using var mensaje = new MailMessage
            {
                From = new MailAddress(_config["Smtp:From"] ?? usuario, "Soporte M/M Contratistas"),
                Subject = asunto,
                Body = cuerpoHtml,
                IsBodyHtml = true
            };
            mensaje.To.Add(destinatario);

            using var smtp = new SmtpClient(_config["Smtp:Host"] ?? "smtp.gmail.com", int.Parse(_config["Smtp:Port"] ?? "587"))
            {
                Credentials = new NetworkCredential(usuario, clave),
                EnableSsl = true
            };
            await smtp.SendMailAsync(mensaje);
        }
    }
}
