using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Controllers
{
    // Gestión de los proyectos históricos que se publican en el sitio web (HU08)
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Administrador + "," + NombresRol.Gerente)]
    public class PortafolioController : ControllerBase
    {
        public const long TamanoMaximoImagen = 5 * 1024 * 1024;
        private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png" };

        private readonly ApplicationDbContext _context;

        public PortafolioController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class ProyectoDto
        {
            public string? Nombre { get; set; }
            public string? TipoObra { get; set; }
            public string? Ubicacion { get; set; }
            public int Anio { get; set; }
            public string? Descripcion { get; set; }
        }

        private static string CarpetaImagenes() => Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "portafolio");

        private static string? ValidarProyecto(ProyectoDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre)) return "El nombre del proyecto es obligatorio.";
            if (string.IsNullOrWhiteSpace(dto.TipoObra)) return "Seleccione el tipo de obra.";
            if (dto.Anio < 1990 || dto.Anio > DateTime.Now.Year + 1) return "Ingrese un año de ejecución válido.";
            return null;
        }

        // Los bytes iniciales deben coincidir con JPG o PNG, no basta con la extensión
        private static bool EsImagenValida(byte[] inicio, string extension)
        {
            var esPng = inicio.Length >= 8 && inicio[0] == 0x89 && inicio[1] == 0x50 && inicio[2] == 0x4E && inicio[3] == 0x47;
            var esJpg = inicio.Length >= 3 && inicio[0] == 0xFF && inicio[1] == 0xD8 && inicio[2] == 0xFF;
            return extension == ".png" ? esPng : esJpg;
        }

        private static object Proyectar(ProyectoPortafolio p) => new
        {
            id = p.Id,
            nombre = p.Nombre,
            tipoObra = p.TipoObra,
            ubicacion = p.Ubicacion,
            anio = p.Anio,
            descripcion = p.Descripcion,
            estado = p.Estado,
            imagenes = p.Imagenes.OrderByDescending(i => i.EsPrincipal).ThenBy(i => i.Id)
                .Select(i => new { id = i.Id, nombre = i.NombreOriginal, esPrincipal = i.EsPrincipal }).ToList()
        };

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var proyectos = await _context.Portafolio.Include(p => p.Imagenes)
                .OrderByDescending(p => p.Anio).ThenBy(p => p.Nombre).ToListAsync();
            return Ok(proyectos.Select(Proyectar));
        }

        // Se guarda siempre como Borrador; publicar es una acción aparte
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] ProyectoDto dto)
        {
            var error = ValidarProyecto(dto);
            if (error != null) return BadRequest(new { mensaje = error });

            var proyecto = new ProyectoPortafolio
            {
                Nombre = dto.Nombre!.Trim(),
                TipoObra = dto.TipoObra!.Trim(),
                Ubicacion = (dto.Ubicacion ?? string.Empty).Trim(),
                Anio = dto.Anio,
                Descripcion = (dto.Descripcion ?? string.Empty).Trim(),
                Estado = ProyectoPortafolio.Borrador
            };
            _context.Portafolio.Add(proyecto);
            await _context.SaveChangesAsync();

            return StatusCode(201, new { mensaje = "Proyecto guardado como borrador.", proyecto = Proyectar(proyecto) });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Editar(int id, [FromBody] ProyectoDto dto)
        {
            var proyecto = await _context.Portafolio.Include(p => p.Imagenes).FirstOrDefaultAsync(p => p.Id == id);
            if (proyecto == null) return NotFound(new { mensaje = "El proyecto no existe." });

            var error = ValidarProyecto(dto);
            if (error != null) return BadRequest(new { mensaje = error });

            proyecto.Nombre = dto.Nombre!.Trim();
            proyecto.TipoObra = dto.TipoObra!.Trim();
            proyecto.Ubicacion = (dto.Ubicacion ?? string.Empty).Trim();
            proyecto.Anio = dto.Anio;
            proyecto.Descripcion = (dto.Descripcion ?? string.Empty).Trim();
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Proyecto actualizado correctamente.", proyecto = Proyectar(proyecto) });
        }

        [HttpPut("{id}/publicar")]
        public async Task<IActionResult> Publicar(int id)
        {
            var proyecto = await _context.Portafolio.Include(p => p.Imagenes).FirstOrDefaultAsync(p => p.Id == id);
            if (proyecto == null) return NotFound(new { mensaje = "El proyecto no existe." });

            // Datos mínimos para mostrarlo en el sitio público
            var faltantes = new List<string>();
            if (string.IsNullOrWhiteSpace(proyecto.Ubicacion)) faltantes.Add("ubicación");
            if (string.IsNullOrWhiteSpace(proyecto.Descripcion)) faltantes.Add("descripción");
            if (!proyecto.Imagenes.Any(i => i.EsPrincipal)) faltantes.Add("imagen principal");
            if (faltantes.Count > 0)
                return BadRequest(new { mensaje = $"Para publicar complete: {string.Join(", ", faltantes)}." });

            proyecto.Estado = ProyectoPortafolio.Publicado;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Proyecto publicado correctamente.", proyecto = Proyectar(proyecto) });
        }

        [HttpPut("{id}/despublicar")]
        public async Task<IActionResult> Despublicar(int id)
        {
            var proyecto = await _context.Portafolio.Include(p => p.Imagenes).FirstOrDefaultAsync(p => p.Id == id);
            if (proyecto == null) return NotFound(new { mensaje = "El proyecto no existe." });

            proyecto.Estado = ProyectoPortafolio.Borrador;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Proyecto retirado del portafolio público.", proyecto = Proyectar(proyecto) });
        }

        // Elimina el proyecto con sus imágenes (registros y archivos)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var proyecto = await _context.Portafolio.Include(p => p.Imagenes).FirstOrDefaultAsync(p => p.Id == id);
            if (proyecto == null) return NotFound(new { mensaje = "El proyecto no existe." });

            var archivos = proyecto.Imagenes.Select(i => Path.Combine(CarpetaImagenes(), i.Archivo)).ToList();
            _context.Portafolio.Remove(proyecto);
            await _context.SaveChangesAsync();

            foreach (var ruta in archivos)
                if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);

            return Ok(new { mensaje = "Proyecto eliminado del portafolio." });
        }

        // POST: api/portafolio/{id}/imagenes  (multipart, campo "archivos")
        [HttpPost("{id}/imagenes")]
        public async Task<IActionResult> SubirImagenes(int id, [FromForm] List<IFormFile> archivos)
        {
            var proyecto = await _context.Portafolio.Include(p => p.Imagenes).FirstOrDefaultAsync(p => p.Id == id);
            if (proyecto == null) return NotFound(new { mensaje = "El proyecto no existe." });
            if (archivos == null || archivos.Count == 0) return BadRequest(new { mensaje = "Seleccione al menos una imagen." });

            // Se valida todo antes de guardar nada: o suben todas o ninguna
            foreach (var archivo in archivos)
            {
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (!ExtensionesPermitidas.Contains(extension))
                    return BadRequest(new { mensaje = $"«{archivo.FileName}» no es válido. Solo se aceptan imágenes JPG o PNG." });
                if (archivo.Length == 0)
                    return BadRequest(new { mensaje = $"«{archivo.FileName}» está vacío." });
                if (archivo.Length > TamanoMaximoImagen)
                    return BadRequest(new { mensaje = $"«{archivo.FileName}» supera el máximo de 5 MB." });

                var inicio = new byte[8];
                int leidos;
                await using (var lectura = archivo.OpenReadStream())
                    leidos = await lectura.ReadAsync(inicio.AsMemory(0, inicio.Length));
                if (!EsImagenValida(inicio.AsSpan(0, leidos).ToArray(), extension))
                    return BadRequest(new { mensaje = $"«{archivo.FileName}» no es una imagen JPG o PNG válida." });
            }

            Directory.CreateDirectory(CarpetaImagenes());
            var sinPrincipal = !proyecto.Imagenes.Any(i => i.EsPrincipal);

            foreach (var archivo in archivos)
            {
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                var guardado = $"{Guid.NewGuid():N}{extension}";
                await using (var destino = new FileStream(Path.Combine(CarpetaImagenes(), guardado), FileMode.Create))
                    await archivo.CopyToAsync(destino);

                _context.ImagenesPortafolio.Add(new ImagenProyecto
                {
                    ProyectoPortafolioId = id,
                    Archivo = guardado,
                    NombreOriginal = Path.GetFileName(archivo.FileName),
                    // Sin imagen principal, la primera que se sube lo es
                    EsPrincipal = sinPrincipal
                });
                sinPrincipal = false;
            }
            await _context.SaveChangesAsync();

            await _context.Entry(proyecto).Collection(p => p.Imagenes).LoadAsync();
            var mensaje = archivos.Count == 1 ? "Imagen subida correctamente." : $"{archivos.Count} imágenes subidas correctamente.";
            return Ok(new { mensaje, proyecto = Proyectar(proyecto) });
        }

        [HttpPut("imagenes/{imagenId}/principal")]
        public async Task<IActionResult> MarcarPrincipal(int imagenId)
        {
            var imagen = await _context.ImagenesPortafolio.FindAsync(imagenId);
            if (imagen == null) return NotFound(new { mensaje = "La imagen no existe." });

            var imagenes = await _context.ImagenesPortafolio.Where(i => i.ProyectoPortafolioId == imagen.ProyectoPortafolioId).ToListAsync();
            foreach (var i in imagenes) i.EsPrincipal = i.Id == imagenId;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Imagen principal actualizada." });
        }

        [HttpDelete("imagenes/{imagenId}")]
        public async Task<IActionResult> EliminarImagen(int imagenId)
        {
            var imagen = await _context.ImagenesPortafolio.FindAsync(imagenId);
            if (imagen == null) return NotFound(new { mensaje = "La imagen no existe." });

            var proyecto = await _context.Portafolio.Include(p => p.Imagenes).FirstAsync(p => p.Id == imagen.ProyectoPortafolioId);
            _context.ImagenesPortafolio.Remove(imagen);

            // Si se quita la principal, otra toma su lugar; sin imágenes un proyecto publicado vuelve a borrador
            var restantes = proyecto.Imagenes.Where(i => i.Id != imagenId).OrderBy(i => i.Id).ToList();
            if (imagen.EsPrincipal && restantes.Count > 0) restantes[0].EsPrincipal = true;
            if (restantes.Count == 0) proyecto.Estado = ProyectoPortafolio.Borrador;
            await _context.SaveChangesAsync();

            var ruta = Path.Combine(CarpetaImagenes(), imagen.Archivo);
            if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);

            return Ok(new { mensaje = "Imagen eliminada." });
        }

        // GET: api/portafolio/imagenes/{imagenId}
        [HttpGet("imagenes/{imagenId}")]
        public async Task<IActionResult> VerImagen(int imagenId)
        {
            var imagen = await _context.ImagenesPortafolio.FindAsync(imagenId);
            if (imagen == null) return NotFound();

            var ruta = Path.Combine(CarpetaImagenes(), imagen.Archivo);
            if (!System.IO.File.Exists(ruta)) return NotFound();

            var tipo = Path.GetExtension(imagen.Archivo) == ".png" ? "image/png" : "image/jpeg";
            return PhysicalFile(ruta, tipo);
        }
    }
}
