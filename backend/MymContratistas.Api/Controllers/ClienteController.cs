using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Cliente)]
    public class ClienteController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClienteController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/cliente/obras
        // La obra se vincula por la ficha de cliente de la cuenta (HU11); las obras anteriores,
        // sin ficha, se siguen reconociendo por el nombre del cliente registrado en la obra
        [HttpGet("obras")]
        public async Task<IActionResult> MisObras()
        {
            var obras = await (await ObrasDelClienteAsync())
                .Select(o => new
                {
                    o.Id,
                    o.Nombre,
                    o.Cliente,
                    o.Estado,
                    o.AvancePorcentaje,
                    // Solo los documentos que el equipo marcó como visibles para el cliente (HU15)
                    Documentos = o.Documentos.Where(d => d.VisibleCliente).OrderByDescending(d => d.Id)
                        .Select(d => new { d.Id, d.Nombre, d.Clasificacion, d.FechaCarga }),
                    Hitos = o.Hitos.OrderBy(h => h.Orden).Select(h => new
                    {
                        h.Id, h.Orden, h.Nombre, h.Peso, h.Estado, h.AvancePorcentaje, h.FechaPlanificada, h.FechaReal
                    })
                })
                .ToListAsync();
            return Ok(obras);
        }

        // GET: api/cliente/documentos/{id}/descargar
        [HttpGet("documentos/{id}/descargar")]
        public async Task<IActionResult> DescargarDocumento(int id)
        {
            var doc = await _context.Documentos.FirstOrDefaultAsync(d => d.Id == id && d.VisibleCliente);
            if (doc == null || !await (await ObrasDelClienteAsync()).AnyAsync(o => o.Id == doc.ObraId))
                return NotFound("Documento no encontrado.");

            var ruta = ObrasController.RutaDocumento(doc);
            if (!System.IO.File.Exists(ruta)) return NotFound("El archivo físico no se encontró.");

            return PhysicalFile(ruta, ObrasController.TipoContenido(doc.Nombre), doc.Nombre);
        }

        // La obra se vincula por la ficha de cliente de la cuenta; las anteriores, por el nombre del cliente
        private async Task<IQueryable<Obra>> ObrasDelClienteAsync()
        {
            var nombre = (User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty).Trim().ToLower();
            int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId);
            var clienteId = await _context.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.ClienteId).FirstOrDefaultAsync();

            return _context.Obras.Where(o => (clienteId != null && o.ClienteId == clienteId)
                                          || (o.ClienteId == null && o.Cliente.Trim().ToLower() == nombre));
        }
    }
}
