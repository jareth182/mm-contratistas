using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Personal)]
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
                .Include(o => o.Hitos.OrderBy(h => h.Orden))
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

        public class HitoDto
        {
            public string? Nombre { get; set; }
            public int Peso { get; set; }
            public DateOnly? FechaPlanificada { get; set; }
        }

        private const int PesoTotal = 100;

        // Valida los datos de un hito; devuelve el mensaje de error o null si todo está bien
        private async Task<string?> ValidarHito(HitoDto dto, int obraId, int? hitoId)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre)) return "El nombre del hito es obligatorio.";
            if (dto.Peso < 1 || dto.Peso > PesoTotal) return "El peso debe estar entre 1 y 100.";

            var otros = await _context.Hitos.Where(h => h.ObraId == obraId && h.Id != hitoId).ToListAsync();
            if (otros.Any(h => string.Equals(h.Nombre, dto.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)))
                return "Ya existe un hito con ese nombre en la obra.";

            var acumulado = otros.Sum(h => h.Peso);
            if (acumulado + dto.Peso > PesoTotal)
                return $"La suma de los pesos no puede superar {PesoTotal}%. Disponible: {PesoTotal - acumulado}%.";
            return null;
        }

        // POST: api/obras/{obraId}/hitos
        [HttpPost("{obraId}/hitos")]
        public async Task<ActionResult<Hito>> PostHito(int obraId, [FromBody] HitoDto dto)
        {
            var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null) return NotFound("La obra no existe.");

            var error = await ValidarHito(dto, obraId, null);
            if (error != null) return BadRequest(error);

            var siguienteOrden = (await _context.Hitos.Where(h => h.ObraId == obraId).MaxAsync(h => (int?)h.Orden) ?? 0) + 1;
            var hito = new Hito
            {
                Nombre = dto.Nombre!.Trim(),
                Peso = dto.Peso,
                FechaPlanificada = dto.FechaPlanificada,
                Orden = siguienteOrden,
                Estado = "Pendiente",
                AvancePorcentaje = 0,
                ObraId = obraId
            };

            _context.Hitos.Add(hito);
            await _context.SaveChangesAsync();
            return Ok(hito);
        }

        // Un hito es "pendiente" mientras no tenga ningún avance registrado
        private static bool TieneAvance(Hito h) =>
            h.Estado != "Pendiente" || h.AvancePorcentaje > 0 || !string.IsNullOrEmpty(h.DescripcionAvance) || !string.IsNullOrEmpty(h.FotoUrl);

        // PUT: api/obras/hitos/{id}/definicion  (solo hitos pendientes)
        [HttpPut("hitos/{id}/definicion")]
        public async Task<IActionResult> EditarHito(int id, [FromBody] HitoDto dto)
        {
            var hito = await _context.Hitos.FindAsync(id);
            if (hito == null) return NotFound("El hito no existe.");
            if (TieneAvance(hito)) return BadRequest("Solo se pueden editar hitos pendientes, sin avances registrados.");

            var error = await ValidarHito(dto, hito.ObraId, id);
            if (error != null) return BadRequest(error);

            hito.Nombre = dto.Nombre!.Trim();
            hito.Peso = dto.Peso;
            hito.FechaPlanificada = dto.FechaPlanificada;
            await _context.SaveChangesAsync();
            return Ok(hito);
        }

        // DELETE: api/obras/hitos/{id}
        [HttpDelete("hitos/{id}")]
        public async Task<IActionResult> EliminarHito(int id)
        {
            var hito = await _context.Hitos.FindAsync(id);
            if (hito == null) return NotFound("El hito no existe.");
            if (TieneAvance(hito)) return BadRequest("No se puede eliminar un hito con avances o gastos asociados.");

            _context.Hitos.Remove(hito);
            await _context.SaveChangesAsync();

            // Mantiene el orden correlativo (1, 2, 3...) de los hitos restantes
            var restantes = await _context.Hitos.Where(h => h.ObraId == hito.ObraId).OrderBy(h => h.Orden).ToListAsync();
            for (var i = 0; i < restantes.Count; i++) restantes[i].Orden = i + 1;
            await _context.SaveChangesAsync();
            return Ok();
        }

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
                hito.FechaReal ??= DateOnly.FromDateTime(DateTime.Now);
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