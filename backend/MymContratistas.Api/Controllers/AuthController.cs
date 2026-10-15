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
    }
}