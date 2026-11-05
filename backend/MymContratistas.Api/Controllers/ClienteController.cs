using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;

namespace MymContratistas.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Cliente)]
    public class ClienteController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificacionHitoService _notificacion;

        public ClienteController(ApplicationDbContext context, NotificacionHitoService notificacion)
        {
            _context = context;
            _notificacion = notificacion;
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

        private const string EstadoAnulado = "Anulado";

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
                comentarios = (await ComentariosDeAsync(id)).Select(x => x.Vista()),
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

        public class ComentarioDto
        {
            public string? Texto { get; set; }
        }

        private async Task<List<ComentarioHito>> ComentariosDeAsync(int hitoId) =>
            await _context.ComentariosHito.Where(x => x.HitoId == hitoId).OrderBy(x => x.Fecha).ThenBy(x => x.Id).ToListAsync();

        // POST: api/cliente/hitos/{id}/comentarios  (HU22: comentario del cliente sobre un hito de su obra)
        [HttpPost("hitos/{id}/comentarios")]
        public async Task<IActionResult> Comentar(int id, [FromBody] ComentarioDto? dto)
        {
            var hito = await (await HitosDelClienteAsync()).Include(h => h.Obra).FirstOrDefaultAsync(h => h.Id == id);
            if (hito == null) return SinPermiso();

            var error = ComentarioHito.Validar(dto?.Texto);
            if (error != null) return BadRequest(error);

            int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId);
            var autor = await _context.Usuarios.Where(u => u.Id == usuarioId).FirstOrDefaultAsync();
            var comentario = new ComentarioHito
            {
                HitoId = id,
                Texto = dto!.Texto!.Trim(),
                Fecha = DateTime.UtcNow,
                AutorUsuarioId = usuarioId,
                AutorNombre = autor?.NombreCompleto is { Length: > 0 } nombre ? nombre : User?.FindFirstValue(ClaimTypes.Name) ?? "Cliente",
                EsPersonal = false
            };
            _context.ComentariosHito.Add(comentario);
            await _context.SaveChangesAsync();

            // Gerente de Operaciones activo: el aviso nunca impide que el comentario quede registrado
            var correos = await _context.Usuarios
                .Where(u => u.Rol.Nombre == NombresRol.Gerente && u.Estado == "Activo" && u.Correo != "")
                .Select(u => u.Correo).ToListAsync();
            var avisado = await _notificacion.ComentarioDelClienteAsync(hito.Obra!, hito, comentario, correos);

            return Ok(new
            {
                mensaje = avisado ? "Comentario enviado correctamente. El equipo de M/M Contratistas fue notificado." : "Comentario enviado correctamente.",
                comentario = comentario.Vista()
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

        // GET: api/cliente/obras/{id}/gastos  (HU21: resumen acumulado por partida y detalle de gastos)
        // Solo gastos vigentes: los anulados no se listan ni suman. No incluye presupuesto, saldos, pagos al personal ni márgenes.
        [HttpGet("obras/{id}/gastos")]
        public async Task<IActionResult> GastosDeObra(int id)
        {
            var obra = await (await ObrasDelClienteAsync()).Where(o => o.Id == id)
                .Select(o => new { o.Id, o.Nombre, o.AvancePorcentaje })
                .FirstOrDefaultAsync();
            if (obra == null) return SinPermiso();

            var gastos = (await _context.Gastos.Where(g => g.ObraId == id && g.Estado != EstadoAnulado).ToListAsync())
                .OrderByDescending(g => ObrasController.FechaOrden(g.Fecha)).ThenByDescending(g => g.Id).ToList();
            return Ok(new
            {
                obra,
                total = gastos.Sum(g => g.Monto),
                resumen = gastos.GroupBy(g => g.PartidaNombre)
                    .Select(p => new { partida = p.Key, total = p.Sum(g => g.Monto), cantidad = p.Count() })
                    .OrderByDescending(p => p.total).ThenBy(p => p.partida).ToList(),
                gastos = gastos.Select(g => new
                {
                    id = g.Id,
                    fecha = g.Fecha,
                    concepto = g.Concepto,
                    proveedor = g.Proveedor,
                    partida = g.PartidaNombre,
                    monto = g.Monto,
                    tipoComprobante = g.TipoComprobante,
                    numeroComprobante = g.NumeroComprobante,
                    comprobante = string.IsNullOrEmpty(g.ComprobanteUrl) ? null : g.ComprobanteUrl
                })
            });
        }

        // GET: api/cliente/gastos/{id}/comprobante  (se usa para ver el comprobante ampliado y para descargarlo)
        [HttpGet("gastos/{id}/comprobante")]
        public async Task<IActionResult> ComprobanteDeGasto(int id)
        {
            var gasto = await _context.Gastos.FirstOrDefaultAsync(g => g.Id == id);
            if (gasto == null || !await (await ObrasDelClienteAsync()).AnyAsync(o => o.Id == gasto.ObraId)) return SinPermiso();
            // Dentro de su propio proyecto, un gasto anulado no existe para el cliente
            if (gasto.Estado == EstadoAnulado || string.IsNullOrEmpty(gasto.ComprobanteUrl)) return NotFound("Comprobante no encontrado.");

            var ruta = ObrasController.RutaComprobante(gasto);
            if (!System.IO.File.Exists(ruta)) return NotFound("El archivo físico no se encontró.");
            return PhysicalFile(ruta, ObrasController.TipoContenido(gasto.ComprobanteUrl), gasto.ComprobanteUrl);
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
