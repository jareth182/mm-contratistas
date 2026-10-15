using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;

namespace MymContratistas.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Administrador)]
    public class UsuariosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Usuario> _hasher;
        private readonly InvitacionService _invitacion;

        public UsuariosController(ApplicationDbContext context, IPasswordHasher<Usuario> hasher,
            InvitacionService invitacion)
        {
            _context = context;
            _hasher = hasher;
            _invitacion = invitacion;
        }

        public class UsuarioDto
        {
            public string? Nombres { get; set; }
            public string? Apellidos { get; set; }
            public string? Correo { get; set; }
            public int RolId { get; set; }
        }

        public class EstadoDto
        {
            public bool Activo { get; set; }
        }

        private static object Proyectar(Usuario u) => new
        {
            id = u.Id,
            nombres = u.Nombres,
            apellidos = u.Apellidos,
            nombreCompleto = u.NombreCompleto,
            correo = u.Correo,
            rolId = u.RolId,
            rol = u.Rol.Nombre,
            estado = u.Estado
        };

        private int IdActual() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private static bool CorreoValido(string correo)
        {
            try { return new MailAddress(correo).Address == correo && correo.Contains('.'); }
            catch (FormatException) { return false; }
        }

        // Evita dejar el sistema sin ningún administrador activo
        private Task<bool> QuedaOtroAdministrador(int idExcluido) =>
            _context.Usuarios.AnyAsync(u => u.Id != idExcluido && u.Estado == "Activo" && u.Rol.Nombre == NombresRol.Administrador);

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var usuarios = await _context.Usuarios.Include(u => u.Rol)
                .OrderBy(u => u.Nombres).ThenBy(u => u.Apellidos).ToListAsync();
            return Ok(usuarios.Select(Proyectar));
        }

        [HttpGet("roles")]
        public async Task<IActionResult> Roles()
        {
            return Ok(await _context.Roles.OrderBy(r => r.Id).Select(r => new { id = r.Id, nombre = r.Nombre }).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] UsuarioDto dto)
        {
            var nombres = (dto.Nombres ?? string.Empty).Trim();
            var apellidos = (dto.Apellidos ?? string.Empty).Trim();
            var correo = (dto.Correo ?? string.Empty).Trim().ToLowerInvariant();

            if (nombres.Length == 0) return BadRequest(new { mensaje = "Los nombres son obligatorios." });
            if (!CorreoValido(correo)) return BadRequest(new { mensaje = "Ingrese un correo electrónico válido." });
            if (!await _context.Roles.AnyAsync(r => r.Id == dto.RolId)) return BadRequest(new { mensaje = "Seleccione un rol válido." });
            if (await _context.Usuarios.AnyAsync(u => u.Correo.ToLower() == correo))
                return Conflict(new { mensaje = "El correo ya está registrado." });

            var usuario = new Usuario { Nombres = nombres, Apellidos = apellidos, Correo = correo, RolId = dto.RolId };
            // Contraseña aleatoria inutilizable: la persona define la suya con el enlace del correo
            usuario.PasswordHash = _hasher.HashPassword(usuario, Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Guid.NewGuid());
            var token = EnlaceTokens.Generar(usuario, TimeSpan.FromHours(InvitacionService.HorasVigencia));

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            await _context.Entry(usuario).Reference(u => u.Rol).LoadAsync();

            var correoEnviado = await _invitacion.EnviarAsync(usuario, token);

            var mensaje = correoEnviado
                ? "Usuario registrado correctamente. Se envió el enlace para definir la contraseña."
                : "Usuario registrado, pero no se pudo enviar el correo. Use «Recuperar contraseña» para que defina su clave.";
            return StatusCode(201, new { mensaje, correoEnviado, usuario = Proyectar(usuario) });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Editar(int id, [FromBody] UsuarioDto dto)
        {
            var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id);
            if (usuario == null) return NotFound(new { mensaje = "El usuario no existe." });

            var nombres = (dto.Nombres ?? string.Empty).Trim();
            if (nombres.Length == 0) return BadRequest(new { mensaje = "Los nombres son obligatorios." });
            if (!await _context.Roles.AnyAsync(r => r.Id == dto.RolId)) return BadRequest(new { mensaje = "Seleccione un rol válido." });

            var cambiaRol = dto.RolId != usuario.RolId;
            if (cambiaRol && usuario.Rol.Nombre == NombresRol.Administrador && usuario.Estado == "Activo" && !await QuedaOtroAdministrador(id))
                return BadRequest(new { mensaje = "No se puede cambiar el rol del único administrador activo." });

            usuario.Nombres = nombres;
            usuario.Apellidos = (dto.Apellidos ?? string.Empty).Trim();
            usuario.RolId = dto.RolId;
            await _context.SaveChangesAsync();
            await _context.Entry(usuario).Reference(u => u.Rol).LoadAsync();

            return Ok(new { mensaje = "Usuario actualizado correctamente.", usuario = Proyectar(usuario) });
        }

        // Desactivar bloquea el acceso pero conserva el registro y su historial
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] EstadoDto dto)
        {
            var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id);
            if (usuario == null) return NotFound(new { mensaje = "El usuario no existe." });

            if (!dto.Activo)
            {
                if (id == IdActual())
                    return BadRequest(new { mensaje = "No puede desactivar su propia cuenta." });
                if (usuario.Rol.Nombre == NombresRol.Administrador && !await QuedaOtroAdministrador(id))
                    return BadRequest(new { mensaje = "No se puede desactivar al único administrador activo." });
            }

            usuario.Estado = dto.Activo ? "Activo" : "Inactivo";
            if (!dto.Activo)
            {
                usuario.TokenRecuperacion = null;
                usuario.TokenExpiracion = null;
            }
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = dto.Activo ? "Usuario activado correctamente." : "Usuario desactivado correctamente.",
                usuario = Proyectar(usuario)
            });
        }
    }
}
