using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = Roles.Personal)]
    public class ObrasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ObrasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/obras
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Obra>>> GetObras()
        {
            // El .Include le dice a Entity Framework que también traiga los hitos asociados
                return await _context.Obras
                .Include(o => o.Hitos)
                .Include(o => o.Cuadrilla) 
                .Include(o => o.Documentos) 
                .Include(o => o.Partidas) 
                .Include(o => o.Gastos) 
                .Include(o => o.Pagos) 
                .ToListAsync();
        }

        // POST: api/obras
        [HttpPost]
        public async Task<ActionResult<Obra>> PostObra(Obra obra)
        {
            _context.Obras.Add(obra);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetObras), new { id = obra.Id }, obra);
        }

        // POST: api/obras/{obraId}/hitos
        // Esta es la nueva ruta para registrar un hito en una obra específica
        [HttpPost("{obraId}/hitos")]
        public async Task<ActionResult<Hito>> PostHito(int obraId, Hito hito)
        {
            var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null)
            {
                return NotFound("La obra no existe.");
            }

            // Aseguramos que el hito se enlace correctamente a la obra
            hito.ObraId = obraId;
            
            _context.Hitos.Add(hito);
            await _context.SaveChangesAsync();

            return Ok(hito);
        }
        // PUT: api/obras/hitos/{id}
  // PUT: api/obras/hitos/{id}
        [HttpPut("hitos/{id}")]
        public async Task<IActionResult> ActualizarAvanceHito(int id, [FromForm] int nuevoAvance, [FromForm] string? descripcion, [FromForm] IFormFile? foto)
        {
            var hito = await _context.Hitos.FindAsync(id);
            if (hito == null)
            {
                return NotFound("El hito no existe.");
            }

            // Actualizamos los datos del hito
            hito.AvancePorcentaje = nuevoAvance;
            hito.DescripcionAvance = descripcion;

            // Lógica simple para simular guardado de foto
            if (foto != null && foto.Length > 0)
            {
                // En un proyecto real, aquí guardarías el archivo en el disco o en la nube (ej. AWS S3).
                // Por ahora, solo guardaremos el nombre original para simularlo en la base de datos.
                hito.FotoUrl = foto.FileName; 
            }

            if (nuevoAvance >= 100)
            {
                hito.AvancePorcentaje = 100;
                hito.Estado = "Completado";
            }
            else if (nuevoAvance > 0)
            {
                hito.Estado = "En proceso";
            }

            await _context.SaveChangesAsync();
            return Ok(hito);
        }
        public async Task<IActionResult> ActualizarAvanceHito(int id, [FromBody] int nuevoAvance)
        {
            var hito = await _context.Hitos.FindAsync(id);
            if (hito == null)
            {
                return NotFound("El hito no existe.");
            }

            // Actualizamos el porcentaje
            hito.AvancePorcentaje = nuevoAvance;

            // Si llega a 100, cambiamos el estado automáticamente
            if (nuevoAvance >= 100)
            {
                hito.AvancePorcentaje = 100;
                hito.Estado = "Completado";
            }
            else if (nuevoAvance > 0)
            {
                hito.Estado = "En proceso";
            }

            await _context.SaveChangesAsync();
            return Ok(hito);
        }
        // POST: api/obras/{obraId}/cuadrilla
        [HttpPost("{obraId}/cuadrilla")]
        public async Task<ActionResult<Colaborador>> AsignarColaborador(int obraId, Colaborador colaborador)
        {
            var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null) return NotFound("La obra no existe.");

            colaborador.ObraId = obraId;
            
            // Si no viene fecha, le ponemos la de hoy
            if (string.IsNullOrEmpty(colaborador.FechaIngresoObra))
            {
                colaborador.FechaIngresoObra = DateTime.Now.ToString("dd/MM/yyyy");
            }

            _context.Cuadrilla.Add(colaborador);
            await _context.SaveChangesAsync();

            return Ok(colaborador);
        }

        // DELETE: api/obras/cuadrilla/{id}
        [HttpDelete("cuadrilla/{id}")]
        public async Task<IActionResult> RetirarColaborador(int id)
        {
            var colaborador = await _context.Cuadrilla.FindAsync(id);
            if (colaborador == null) return NotFound("El colaborador no existe.");

            _context.Cuadrilla.Remove(colaborador);
            await _context.SaveChangesAsync();

            return Ok();
        }
        // POST: api/obras/{obraId}/documentos
        [HttpPost("{obraId}/documentos")]
        public async Task<IActionResult> SubirDocumento(int obraId, [FromForm] string clasificacion, [FromForm] bool visibleCliente, [FromForm] IFormFile? archivo)
        {
 var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null) return NotFound("La obra no existe.");
            if (archivo == null || archivo.Length == 0) return BadRequest("Debe adjuntar un archivo.");

            // 1. Guardar el archivo físico en una carpeta "Uploads" en tu servidor
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            
            var filePath = Path.Combine(folderPath, archivo.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            // 2. Guardar el registro en PostgreSQL
            var doc = new Documento
            {
                Nombre = archivo.FileName,
                Clasificacion = clasificacion,
                VisibleCliente = visibleCliente,
                FechaCarga = DateTime.Now.ToString("dd/MM/yyyy"),
                ObraId = obraId
            };

            _context.Documentos.Add(doc);
            await _context.SaveChangesAsync();

            return Ok(doc);
        }

        // GET: api/obras/documentos/{id}/descargar
        [HttpGet("documentos/{id}/descargar")]
        public async Task<IActionResult> DescargarDocumento(int id)
        {
            var doc = await _context.Documentos.FindAsync(id);
            if (doc == null) return NotFound("Documento no encontrado.");

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", doc.Nombre);
            if (!System.IO.File.Exists(filePath)) return NotFound("El archivo físico no se encontró.");

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, "application/octet-stream", doc.Nombre);
        }

        // DELETE: api/obras/documentos/{id}
        [HttpDelete("documentos/{id}")]
        public async Task<IActionResult> EliminarDocumento(int id)
        {
            var doc = await _context.Documentos.FindAsync(id);
            if (doc == null) return NotFound();

            _context.Documentos.Remove(doc);
            await _context.SaveChangesAsync();

            return Ok();
        }
        // POST: api/obras/{obraId}/partidas
        [HttpPost("{obraId}/partidas")]
        public async Task<ActionResult<Partida>> AgregarPartida(int obraId, Partida partida)
        {
            var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null) return NotFound("La obra no existe.");

            partida.ObraId = obraId;
            _context.Partidas.Add(partida);
            await _context.SaveChangesAsync();

            return Ok(partida);
        }

        // DELETE: api/obras/partidas/{id}
        [HttpDelete("partidas/{id}")]
        public async Task<IActionResult> EliminarPartida(int id)
        {
            var partida = await _context.Partidas.FindAsync(id);
            if (partida == null) return NotFound();

            _context.Partidas.Remove(partida);
            await _context.SaveChangesAsync();

            return Ok();
            
        }
        // PUT: api/obras/partidas/{id}
        [HttpPut("partidas/{id}")]
        public async Task<IActionResult> EditarPartida(int id, Partida partidaActualizada)
        {
            var partida = await _context.Partidas.FindAsync(id);
            if (partida == null) return NotFound("La partida no existe.");

            // Actualizamos los datos
            partida.Nombre = partidaActualizada.Nombre;
            partida.MontoAsignado = partidaActualizada.MontoAsignado;
            
            await _context.SaveChangesAsync();
            return Ok(partida);
        }
        // POST: api/obras/{obraId}/gastos
        [HttpPost("{obraId}/gastos")]
        public async Task<IActionResult> RegistrarGasto(int obraId, [FromForm] string fecha, [FromForm] string tipo, [FromForm] string numero, [FromForm] string concepto, [FromForm] string proveedor, [FromForm] string partidaNombre, [FromForm] decimal monto, [FromForm] IFormFile? archivo)
        {
            var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null) return NotFound("La obra no existe.");

            var gasto = new Gasto
            {
                Fecha = fecha, TipoComprobante = tipo ?? "", NumeroComprobante = numero ?? "",
                Concepto = concepto, Proveedor = proveedor ?? "", PartidaNombre = partidaNombre,
                Monto = monto, ObraId = obraId, Estado = "Registrado"
            };

            // Si viene un archivo, lo guardamos en la carpeta Uploads
            if (archivo != null && archivo.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                var filePath = Path.Combine(folderPath, archivo.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }
                gasto.ComprobanteUrl = archivo.FileName;
            }

            _context.Gastos.Add(gasto);
            await _context.SaveChangesAsync();
            return Ok(gasto);
        }

        // GET: api/obras/gastos/{id}/descargar
        [HttpGet("gastos/{id}/descargar")]
        public async Task<IActionResult> DescargarComprobante(int id)
        {
            var gasto = await _context.Gastos.FindAsync(id);
            if (gasto == null || string.IsNullOrEmpty(gasto.ComprobanteUrl)) return NotFound();
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", gasto.ComprobanteUrl);
            if (!System.IO.File.Exists(filePath)) return NotFound();
            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, "application/octet-stream", gasto.ComprobanteUrl);
        }

        // PUT: api/obras/gastos/{id}/anular
        [HttpPut("gastos/{id}/anular")]
        public async Task<IActionResult> AnularGasto(int id)
        {
            var gasto = await _context.Gastos.FindAsync(id);
            if (gasto == null) return NotFound();
            
            gasto.Estado = "Anulado"; // Al anularlo, el frontend recalculará el saldo de la partida
            await _context.SaveChangesAsync();
            return Ok();
        }
        // POST: api/obras/{obraId}/pagos
        [HttpPost("{obraId}/pagos")]
        public async Task<IActionResult> RegistrarPago(int obraId, [FromForm] string colaboradorNombre, [FromForm] string periodo, [FromForm] decimal monto, [FromForm] string fechaPago, [FromForm] IFormFile? archivo)
        {
            var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null) return NotFound("La obra no existe.");

            var pago = new Pago
            {
                ColaboradorNombre = colaboradorNombre,
                Periodo = periodo,
                Monto = monto,
                FechaPago = fechaPago,
                ObraId = obraId,
                Estado = "Registrado"
            };

            // Guardar la constancia física si existe
            if (archivo != null && archivo.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                var filePath = Path.Combine(folderPath, archivo.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }
                pago.ConstanciaUrl = archivo.FileName;
            }

            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();
            return Ok(pago);
        }

        // GET: api/obras/pagos/{id}/descargar
        [HttpGet("pagos/{id}/descargar")]
        public async Task<IActionResult> DescargarConstancia(int id)
        {
            var pago = await _context.Pagos.FindAsync(id);
            if (pago == null || string.IsNullOrEmpty(pago.ConstanciaUrl)) return NotFound();
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", pago.ConstanciaUrl);
            if (!System.IO.File.Exists(filePath)) return NotFound();
            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, "application/octet-stream", pago.ConstanciaUrl);
        }
    }
}