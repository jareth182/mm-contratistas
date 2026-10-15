using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Models;
using MymContratistas.Api.Data;
using System.Net;
using System.Net.Mail;

namespace MymContratistas.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto login)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == login.Correo);

            if (usuario == null)
                return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });

            if (usuario.Estado == "Inactivo")
                return Unauthorized(new { mensaje = "Su cuenta se encuentra desactivada." });

            if (usuario.BloqueoHasta.HasValue && usuario.BloqueoHasta.Value > DateTime.Now)
            {
                var minutosRestantes = (usuario.BloqueoHasta.Value - DateTime.Now).Minutes;
                if (minutosRestantes == 0) minutosRestantes = 1; 
                return Unauthorized(new { mensaje = $"Cuenta bloqueada temporalmente por seguridad. Intente nuevamente en {minutosRestantes} minutos." });
            }

            if (usuario.Password != login.Password)
            {
                usuario.IntentosFallidos++;
                
                if (usuario.IntentosFallidos >= 5)
                {
                    usuario.BloqueoHasta = DateTime.Now.AddMinutes(15);
                    await _context.SaveChangesAsync();
                    return Unauthorized(new { mensaje = "Cuenta bloqueada temporalmente por seguridad. Intente nuevamente en 15 minutos." });
                }
                
                await _context.SaveChangesAsync();
                return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });
            }

            usuario.IntentosFallidos = 0;
            usuario.BloqueoHasta = null;
            await _context.SaveChangesAsync();

            return Ok(new { 
                id = usuario.Id, 
                nombreCompleto = usuario.NombreCompleto, 
                rol = usuario.Rol,
                correo = usuario.Correo
            });
        }

        [HttpPost("recuperar")]
        public async Task<IActionResult> RecuperarPassword([FromBody] RecuperarDto peticion)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == peticion.Correo);
    
            if (usuario == null) 
                return Ok(new { mensaje = "Si el correo se encuentra registrado, recibirá un enlace de recuperación en los próximos minutos." });

            usuario.TokenRecuperacion = Guid.NewGuid().ToString();
            usuario.TokenExpiracion = DateTime.UtcNow.AddHours(24);
            await _context.SaveChangesAsync();

            try
            {
                string miCorreoGmail = "jazarelly.zapata@gmail.com"; 
                string miClaveApp = "qtdo wwup vrcy eypb"; 

                // Atrapamos la ruta invisible que mandó app.js sin errores de modelo
                string urlFrontend = Request.Headers["Frontend-Url"].FirstOrDefault() ?? "file:///C:/Users/User/Documents/DIS/mm-contratistas/frontend/index.html";
                string enlace = $"{urlFrontend}#nueva_password?token={usuario.TokenRecuperacion}";

                MailMessage correo = new MailMessage();
                correo.From = new MailAddress(miCorreoGmail, "Soporte M/M Contratistas");
                correo.To.Add(usuario.Correo);
                correo.Subject = "Restablecer Contraseña - M/M Contratistas";
                correo.IsBodyHtml = true;
                correo.Body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                        <h2>Recuperación de Contraseña</h2>
                        <p>Hola {usuario.NombreCompleto},</p>
                        <p>Has solicitado restablecer tu contraseña. Haz clic en el siguiente enlace para crear una nueva:</p>
                        <br>
                        <a href='{enlace}' style='background-color: #0A1929; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Cambiar mi contraseña</a>
                        <br><br>
                        <p>Si el botón no funciona, copia y pega esta dirección en tu navegador:</p>
                        <p style='color: #666; font-size: 12px;'>{enlace}</p>
                        <br>
                        <p>Este enlace expirará en 24 horas.</p>
                    </div>";

                using (SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587))
                {
                    smtp.Credentials = new NetworkCredential(miCorreoGmail, miClaveApp);
                    smtp.EnableSsl = true;
                    await smtp.SendMailAsync(correo);
                }

                return Ok(new { mensaje = "Si el correo se encuentra registrado, recibirá un enlace de recuperación en los próximos minutos." });
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR SMTP: " + ex.Message);
                return StatusCode(500, new { mensaje = "Hubo un problema de conexión al intentar enviar el correo." });
            }
        }

        [HttpPost("restablecer")]
        public async Task<IActionResult> RestablecerPassword([FromBody] ResetPasswordDto peticion)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.TokenRecuperacion == peticion.Token);
    
            if (usuario == null || usuario.TokenExpiracion < DateTime.UtcNow)
                return BadRequest(new { mensaje = "El enlace de recuperación es inválido o ha expirado." });

            usuario.Password = peticion.NuevaPassword;
            usuario.TokenRecuperacion = null;
            usuario.TokenExpiracion = null;
    
            usuario.IntentosFallidos = 0;
            usuario.BloqueoHasta = null;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Contraseña actualizada correctamente." });
        }
    }
}