using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Models;
using MymContratistas.Api.Data;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MymContratistas.Api.Services;
using System.Security.Cryptography;
using System.Text;

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
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ApplicationDbContext context, IPasswordHasher<Usuario> hasher, TokenService tokens,
            IEmailService email, IConfiguration config, ILogger<AuthController> logger)
        {
            _context = context;
            _hasher = hasher;
            _tokens = tokens;
            _email = email;
            _config = config;
            _logger = logger;
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

        private const string MensajeRecuperacion = "Si el correo se encuentra registrado, recibirá un enlace de recuperación en los próximos minutos.";
        private const int HorasVigenciaEnlace = 24;
        public const int LongitudMinimaPassword = 8;

        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        // Responde siempre lo mismo para no revelar si el correo existe
        [AllowAnonymous]
        [HttpPost("recuperar")]
        public async Task<IActionResult> RecuperarPassword([FromBody] RecuperarDto peticion)
        {
            var correo = (peticion.Correo ?? string.Empty).Trim().ToLowerInvariant();
            if (correo.Length == 0)
                return BadRequest(new { mensaje = "Ingrese un correo electrónico válido." });

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == correo);
            if (usuario == null || usuario.Estado != "Activo")
                return Ok(new { mensaje = MensajeRecuperacion });

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            usuario.TokenRecuperacion = HashToken(token);
            usuario.TokenExpiracion = DateTime.UtcNow.AddHours(HorasVigenciaEnlace);
            await _context.SaveChangesAsync();

            try
            {
                var enlace = $"{_config["App:FrontendUrl"]}#nueva_password?token={token}";
                await _email.EnviarAsync(usuario.Correo, "Restablecer contraseña - M/M Contratistas", CuerpoCorreo(usuario.NombreCompleto, enlace));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el correo de recuperación");
            }

            return Ok(new { mensaje = MensajeRecuperacion });
        }

        private static string CuerpoCorreo(string nombre, string enlace) => $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>Recuperación de contraseña</h2>
                <p>Hola {WebUtility.HtmlEncode(nombre)},</p>
                <p>Recibimos una solicitud para restablecer su contraseña. Haga clic en el siguiente botón para crear una nueva:</p>
                <p><a href='{enlace}' style='background-color: #0A1929; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Cambiar mi contraseña</a></p>
                <p>Si el botón no funciona, copie y pegue esta dirección en su navegador:</p>
                <p style='color: #666; font-size: 12px;'>{enlace}</p>
                <p>Este enlace vence en {HorasVigenciaEnlace} horas. Si usted no lo solicitó, ignore este mensaje.</p>
            </div>";

        private Task<Usuario?> UsuarioPorToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return Task.FromResult<Usuario?>(null);
            var hash = HashToken(token);
            return _context.Usuarios.FirstOrDefaultAsync(u => u.TokenRecuperacion == hash && u.TokenExpiracion > DateTime.UtcNow);
        }

        // Permite al frontend avisar antes de mostrar el formulario si el enlace venció
        [AllowAnonymous]
        [HttpGet("validar-token")]
        public async Task<IActionResult> ValidarToken([FromQuery] string? token)
        {
            return await UsuarioPorToken(token) == null
                ? BadRequest(new { mensaje = "El enlace de recuperación es inválido o ha expirado." })
                : Ok();
        }

        [AllowAnonymous]
        [HttpPost("restablecer")]
        public async Task<IActionResult> RestablecerPassword([FromBody] ResetPasswordDto peticion)
        {
            var usuario = await UsuarioPorToken(peticion.Token);
            if (usuario == null)
                return BadRequest(new { mensaje = "El enlace de recuperación es inválido o ha expirado." });

            if ((peticion.NuevaPassword ?? string.Empty).Length < LongitudMinimaPassword)
                return BadRequest(new { mensaje = $"La contraseña debe tener al menos {LongitudMinimaPassword} caracteres." });

            usuario.PasswordHash = _hasher.HashPassword(usuario, peticion.NuevaPassword!);
            usuario.TokenRecuperacion = null;
            usuario.TokenExpiracion = null;
            usuario.IntentosFallidos = 0;
            usuario.BloqueoHasta = null;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Contraseña actualizada correctamente." });
        }
    }
}
