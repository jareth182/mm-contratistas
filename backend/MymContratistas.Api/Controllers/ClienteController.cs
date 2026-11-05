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
                    o.FechaInicio,
                    o.FechaFinEstimada,
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

        public const string MensajeSinPermiso = "No tiene permisos para ver este proyecto. Solo puede consultar los proyectos vinculados a su cuenta.";

        private IActionResult SinPermiso() => StatusCode(StatusCodes.Status403Forbidden, MensajeSinPermiso);

        // GET: api/cliente/obras/{id}  (Seguimiento de Obra: avance general, hitos y documentos visibles)
        // Autorización por recurso: la obra debe estar vinculada al cliente; si no existe o es de otro, la respuesta es la misma
        [HttpGet("obras/{id}")]
        public async Task<IActionResult> SeguimientoDeObra(int id)
        {
            var obra = await (await ObrasDelClienteAsync()).Where(o => o.Id == id)
                .Select(o => new
                {
                    o.Id,
                    o.Nombre,
                    o.Cliente,
                    o.Estado,
                    o.Ubicacion,
                    o.AvancePorcentaje,
                    o.FechaInicio,
                    o.FechaFinEstimada,
                    o.FechaCierre,
                    Hitos = o.Hitos.OrderBy(h => h.Orden).Select(h => new
                    {
                        h.Id, h.Orden, h.Nombre, h.Peso, h.Estado, h.AvancePorcentaje, h.FechaPlanificada, h.FechaReal,
                        TieneAvances = h.Avances.Any()
                    }),
                    Documentos = o.Documentos.Where(d => d.VisibleCliente).OrderByDescending(d => d.Id)
                        .Select(d => new { d.Id, d.Nombre, d.Clasificacion, d.FechaCarga })
                })
                .FirstOrDefaultAsync();
            return obra == null ? SinPermiso() : Ok(obra);
        }

        // GET: api/cliente/hitos/{id}/avances  (detalle del hito: descripción de cada avance y sus fotografías)
        [HttpGet("hitos/{id}/avances")]
        public async Task<IActionResult> DetalleDeHito(int id)
        {
            var hito = await (await HitosDelClienteAsync()).Where(h => h.Id == id)
                .Select(h => new { h.Id, h.Nombre, h.Estado, h.AvancePorcentaje, h.FechaPlanificada, h.FechaReal, h.DescripcionAvance })
                .FirstOrDefaultAsync();
            if (hito == null) return SinPermiso();

            var avances = await _context.AvancesHito.Include(a => a.Fotos)
                .Where(a => a.HitoId == id).OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id).ToListAsync();
            return Ok(new
            {
                hito,
                avances = avances.Select(a => new
                {
                    a.Id,
                    a.Porcentaje,
                    a.Descripcion,
                    a.Fecha,
                    Fotos = a.Fotos.OrderBy(f => f.Id).Select(f => new { f.Id, Nombre = f.NombreOriginal })
                })
            });
        }

        // GET: api/cliente/avances/fotos/{id}
        [HttpGet("avances/fotos/{id}")]
        public async Task<IActionResult> VerFotoDeAvance(int id)
        {
            var foto = await _context.FotosAvance.Where(f => f.Id == id)
                .Select(f => new { f.Archivo, HitoId = f.Avance!.HitoId })
                .FirstOrDefaultAsync();
            if (foto == null || !await (await HitosDelClienteAsync()).AnyAsync(h => h.Id == foto.HitoId)) return SinPermiso();

            var ruta = Path.Combine(ObrasController.CarpetaDeAvances, foto.Archivo);
            if (!System.IO.File.Exists(ruta)) return NotFound("El archivo físico no se encontró.");
            return PhysicalFile(ruta, Path.GetExtension(foto.Archivo) == ".png" ? "image/png" : "image/jpeg");
        }

        // GET: api/cliente/documentos/{id}/descargar
        [HttpGet("documentos/{id}/descargar")]
        public async Task<IActionResult> DescargarDocumento(int id)
        {
            var doc = await _context.Documentos.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null || !await (await ObrasDelClienteAsync()).AnyAsync(o => o.Id == doc.ObraId)) return SinPermiso();
            // Dentro de su propio proyecto, un documento de uso interno no existe para el cliente
            if (!doc.VisibleCliente) return NotFound("Documento no encontrado.");

            var ruta = ObrasController.RutaDocumento(doc);
            if (!System.IO.File.Exists(ruta)) return NotFound("El archivo físico no se encontró.");

            return PhysicalFile(ruta, ObrasController.TipoContenido(doc.Nombre), doc.Nombre);
        }

        private async Task<IQueryable<Hito>> HitosDelClienteAsync()
        {
            var obras = await ObrasDelClienteAsync();
            return _context.Hitos.Where(h => obras.Any(o => o.Id == h.ObraId));
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
