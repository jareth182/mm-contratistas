using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Models;
using MymContratistas.Api.Data;
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

            // 1. Si no existe el correo
            if (usuario == null)
                return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos." });

            // 2. Si fue desactivado por la empresa
            if (usuario.Estado == "Inactivo")
                return Unauthorized(new { mensaje = "Su cuenta se encuentra desactivada." });

            // 3. Verificamos si está bloqueado por múltiples intentos (HU-01 Criterio 4)
            if (usuario.BloqueoHasta.HasValue && usuario.BloqueoHasta.Value > DateTime.Now)
            {
                var minutosRestantes = (usuario.BloqueoHasta.Value - DateTime.Now).Minutes;
                if (minutosRestantes == 0) minutosRestantes = 1; // Para que no diga 0 minutos
                return Unauthorized(new { mensaje = $"Cuenta bloqueada temporalmente por seguridad. Intente nuevamente en {minutosRestantes} minutos." });
            }

            // 4. Verificamos que la contraseña coincida
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

            // 5. Login exitoso: limpiamos los intentos fallidos
            usuario.IntentosFallidos = 0;
            usuario.BloqueoHasta = null;
            await _context.SaveChangesAsync();

            // Devolvemos los datos básicos para que el frontend arme el menú
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
    
            // Por seguridad, siempre devolvemos el mismo mensaje aunque el correo no exista (evita escaneo de correos)
            if (usuario == null) 
                return Ok(new { mensaje = "Si el correo se encuentra registrado, recibirá un enlace de recuperación en los próximos minutos." });

            // Generamos un token seguro y le damos 24 horas de vigencia (Criterio 2)
            usuario.TokenRecuperacion = Guid.NewGuid().ToString();
            usuario.TokenExpiracion = DateTime.UtcNow.AddHours(24);
            await _context.SaveChangesAsync();

            // SIMULACIÓN: Como aún no conectamos un servidor de correos real (SMTP), 
            // devolveremos el token al frontend para que puedas probar la pantalla "Nueva Contraseña".
            return Ok(new { 
                mensaje = "Si el correo se encuentra registrado, recibirá un enlace de recuperación en los próximos minutos.",
                tokenSimulado = usuario.TokenRecuperacion 
            });
        }

        [HttpPost("restablecer")]
        public async Task<IActionResult> RestablecerPassword([FromBody] ResetPasswordDto peticion)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.TokenRecuperacion == peticion.Token);
    
            if (usuario == null || usuario.TokenExpiracion < DateTime.UtcNow)
                return BadRequest(new { mensaje = "El enlace de recuperación es inválido o ha expirado." });

            // Actualizamos la contraseña y destruimos el token
            usuario.Password = peticion.NuevaPassword;
            usuario.TokenRecuperacion = null;
            usuario.TokenExpiracion = null;
    
            // Si la cuenta estaba bloqueada (HU-01), al cambiar la clave la desbloqueamos
            usuario.IntentosFallidos = 0;
            usuario.BloqueoHasta = null;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Contraseña actualizada correctamente." });
        }
    }
}