using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Models;
using MymContratistas.Api.Data;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MymContratistas.Api.Services;
using System.Net.Mail;

namespace MymContratistas.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private const int MaxIntentosFallidos = 5;
        private const int MinutosBloqueo = 15;

        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Usuario> _hasher;
        private readonly TokenService _tokens;

        public AuthController(ApplicationDbContext context, IPasswordHasher<Usuario> hasher, TokenService tokens)
        {
            _context = context;
            _hasher = hasher;
            _tokens = tokens;
        }

        private object RespuestaSesion(Usuario usuario)
        {
            var (token, expira) = _tokens.Generar(usuario);
            return new
            {
                token,
                expira,
                usuario = new { id = usuario.Id, nombreCompleto = usuario.NombreCompleto, correo = usuario.Correo, rol = usuario.Rol }
            };
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto login)
        {
            var correo = (login.Correo ?? string.Empty).Trim().ToLowerInvariant();
            if (correo.Length == 0 || string.IsNullOrEmpty(login.Password))
                return BadRequest(new { mensaje = "Debe ingresar su correo y contraseña." });

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == correo);

            // Mismo mensaje si el usuario no existe o la contraseña es incorrecta
            var credencialesInvalidas = Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });
            if (usuario == null) return credencialesInvalidas;

            if (usuario.BloqueoHasta.HasValue && usuario.BloqueoHasta.Value > DateTime.UtcNow)
            {
                var minutos = (int)Math.Ceiling((usuario.BloqueoHasta.Value - DateTime.UtcNow).TotalMinutes);
                return Unauthorized(new { mensaje = $"Cuenta bloqueada temporalmente por seguridad. Intente nuevamente en {minutos} minutos." });
            }

            var resultado = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, login.Password);
            if (resultado == PasswordVerificationResult.Failed)
            {
                usuario.IntentosFallidos++;
                if (usuario.IntentosFallidos >= MaxIntentosFallidos)
                {
                    usuario.BloqueoHasta = DateTime.UtcNow.AddMinutes(MinutosBloqueo);
                    usuario.IntentosFallidos = 0;
                    await _context.SaveChangesAsync();
                    return Unauthorized(new { mensaje = $"Cuenta bloqueada temporalmente por seguridad. Intente nuevamente en {MinutosBloqueo} minutos." });
                }
                await _context.SaveChangesAsync();
                return credencialesInvalidas;
            }

            if (usuario.Estado == "Inactivo")
                return Unauthorized(new { mensaje = "Su cuenta se encuentra desactivada. Comuníquese con el administrador." });

            if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
                usuario.PasswordHash = _hasher.HashPassword(usuario, login.Password);

            usuario.IntentosFallidos = 0;
            usuario.BloqueoHasta = null;
            await _context.SaveChangesAsync();

            return Ok(RespuestaSesion(usuario));
        }

        // Devuelve los datos del usuario autenticado (valida que el token siga vigente)
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var usuario = await UsuarioActual();
            if (usuario == null) return Unauthorized();
            return Ok(new { id = usuario.Id, nombreCompleto = usuario.NombreCompleto, correo = usuario.Correo, rol = usuario.Rol });
        }

        // Emite un token nuevo mientras la sesión siga activa (el usuario eligió «Continuar»)
        [Authorize]
        [HttpPost("renovar")]
        public async Task<IActionResult> Renovar()
        {
            var usuario = await UsuarioActual();
            if (usuario == null) return Unauthorized(new { mensaje = "La sesión ya no es válida." });
            return Ok(RespuestaSesion(usuario));
        }

        private async Task<Usuario?> UsuarioActual()
        {
            var idTexto = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idTexto, out var id)) return null;
            var usuario = await _context.Usuarios.FindAsync(id);
            return usuario is { Estado: "Activo" } ? usuario : null;
        }

        [AllowAnonymous]
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

        [AllowAnonymous]
        [HttpPost("restablecer")]
        public async Task<IActionResult> RestablecerPassword([FromBody] ResetPasswordDto peticion)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.TokenRecuperacion == peticion.Token);
    
            if (usuario == null || usuario.TokenExpiracion < DateTime.UtcNow)
                return BadRequest(new { mensaje = "El enlace de recuperación es inválido o ha expirado." });

            usuario.PasswordHash = _hasher.HashPassword(usuario, peticion.NuevaPassword);
            usuario.TokenRecuperacion = null;
            usuario.TokenExpiracion = null;
    
            usuario.IntentosFallidos = 0;
            usuario.BloqueoHasta = null;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Contraseña actualizada correctamente." });
        }
    }
}