using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;

namespace MymContratistas.Api.Controllers
{
    // Avisos dentro de la aplicación (la campana) y registro de envíos (HT-05).
    // Cada usuario solo ve y marca sus propios avisos.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificacionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificacionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int UsuarioId => int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private IQueryable<Notificacion> MisAvisos() =>
            _context.Notificaciones.Where(n => n.Canal == Notificacion.CanalApp && n.UsuarioId == UsuarioId);

        // GET: api/notificaciones?pagina=1&tamano=20&soloNoLeidas=false  (del más reciente al más antiguo)
        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] int? pagina, [FromQuery] int? tamano, [FromQuery] bool soloNoLeidas = false)
        {
            var consulta = MisAvisos();
            var noLeidas = await consulta.CountAsync(n => !n.Leida);
            if (soloNoLeidas) consulta = consulta.Where(n => !n.Leida);

            var resultado = await Paginacion.PaginarAsync(
                consulta.OrderByDescending(n => n.Fecha).ThenByDescending(n => n.Id)
                    .Select(n => new { id = n.Id, tipo = n.Tipo, titulo = n.Titulo, mensaje = n.Mensaje, obraId = n.ObraId, hitoId = n.HitoId, fecha = n.Fecha, leida = n.Leida }),
                pagina, tamano);
            return Ok(new { items = resultado.Items, paginacion = resultado.Info, noLeidas });
        }

        // PUT: api/notificaciones/{id}/leida  — un aviso ajeno responde igual que uno inexistente
        [HttpPut("{id:int}/leida")]
        public async Task<IActionResult> MarcarLeida(int id)
        {
            var aviso = await MisAvisos().FirstOrDefaultAsync(n => n.Id == id);
            if (aviso == null) return NotFound("El aviso no existe.");

            if (!aviso.Leida)
            {
                aviso.Leida = true;
                aviso.LeidaEn = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return Ok(new { noLeidas = await MisAvisos().CountAsync(n => !n.Leida) });
        }

        // PUT: api/notificaciones/leidas
        [HttpPut("leidas")]
        public async Task<IActionResult> MarcarTodasLeidas()
        {
            var ahora = DateTime.UtcNow;
            var marcadas = await MisAvisos().Where(n => !n.Leida).ExecuteUpdateAsync(s => s.SetProperty(n => n.Leida, true).SetProperty(n => n.LeidaEn, ahora));
            return Ok(new { marcadas, noLeidas = 0 });
        }

        // GET: api/notificaciones/registro?canal=&estado=&tipo=&desde=&hasta=&pagina=&tamano=
        // Registro de envíos para auditoría: no incluye el contenido de los correos
        [HttpGet("registro")]
        [Authorize(Roles = NombresRol.Administrador + "," + NombresRol.Gerente)]
        public async Task<IActionResult> Registro([FromQuery] string? canal, [FromQuery] string? estado, [FromQuery] string? tipo,
            [FromQuery] string? desde, [FromQuery] string? hasta, [FromQuery] int? pagina, [FromQuery] int? tamano)
        {
            var consulta = _context.Notificaciones.AsQueryable();
            if (!string.IsNullOrWhiteSpace(canal)) consulta = consulta.Where(n => n.Canal == canal.Trim());
            if (!string.IsNullOrWhiteSpace(estado)) consulta = consulta.Where(n => n.Estado == estado.Trim());
            if (!string.IsNullOrWhiteSpace(tipo)) consulta = consulta.Where(n => n.Tipo == tipo.Trim());
            if (!string.IsNullOrWhiteSpace(desde))
            {
                if (!DateOnly.TryParseExact(desde.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return BadRequest("La fecha «desde» debe tener el formato aaaa-mm-dd.");
                var inicio = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
                consulta = consulta.Where(n => n.Fecha >= inicio);
            }
            if (!string.IsNullOrWhiteSpace(hasta))
            {
                if (!DateOnly.TryParseExact(hasta.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h)) return BadRequest("La fecha «hasta» debe tener el formato aaaa-mm-dd.");
                var fin = h.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
                consulta = consulta.Where(n => n.Fecha < fin);
            }

            var resultado = await Paginacion.PaginarAsync(
                consulta.OrderByDescending(n => n.Fecha).ThenByDescending(n => n.Id)
                    .Select(n => new
                    {
                        id = n.Id, tipo = n.Tipo, canal = n.Canal, destinatario = n.Destinatario, titulo = n.Titulo,
                        estado = n.Estado, error = n.Error, intentos = n.Intentos, fecha = n.Fecha, enviadoEn = n.EnviadoEn
                    }),
                pagina, tamano);
            return Ok(new { items = resultado.Items, paginacion = resultado.Info });
        }
    }
}
