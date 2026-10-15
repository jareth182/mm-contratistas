using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Controllers
{
    // Contenido del sitio público: no requiere sesión y solo expone lo publicado
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class SitioController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SitioController(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<ProyectoPortafolio> Publicados() =>
            _context.Portafolio.Where(p => p.Estado == ProyectoPortafolio.Publicado);

        // GET: api/sitio/proyectos  (HU05)
        [HttpGet("proyectos")]
        public async Task<IActionResult> Proyectos()
        {
            var proyectos = await Publicados()
                .OrderByDescending(p => p.Anio).ThenBy(p => p.Nombre)
                .Select(p => new
                {
                    id = p.Id,
                    nombre = p.Nombre,
                    tipoObra = p.TipoObra,
                    ubicacion = p.Ubicacion,
                    anio = p.Anio,
                    imagenPrincipalId = p.Imagenes.Where(i => i.EsPrincipal).Select(i => (int?)i.Id).FirstOrDefault()
                })
                .ToListAsync();
            return Ok(proyectos);
        }

        // GET: api/sitio/proyectos/{id}
        [HttpGet("proyectos/{id}")]
        public async Task<IActionResult> Proyecto(int id)
        {
            var p = await Publicados().Include(x => x.Imagenes).FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound(new { mensaje = "El proyecto no existe o ya no está publicado." });

            return Ok(new
            {
                id = p.Id,
                nombre = p.Nombre,
                tipoObra = p.TipoObra,
                ubicacion = p.Ubicacion,
                anio = p.Anio,
                descripcion = p.Descripcion,
                imagenes = p.Imagenes.OrderByDescending(i => i.EsPrincipal).ThenBy(i => i.Id)
                    .Select(i => new { id = i.Id, nombre = i.NombreOriginal, esPrincipal = i.EsPrincipal }).ToList()
            });
        }

        // GET: api/sitio/servicios  — catálogo publicado, en el orden definido (HU09)
        [HttpGet("servicios")]
        public async Task<IActionResult> Servicios()
        {
            var servicios = await _context.Servicios.Where(s => s.Estado == Servicio.Publicado)
                .OrderBy(s => s.Orden).ThenBy(s => s.Id)
                .Select(s => new { id = s.Id, nombre = s.Nombre, descripcion = s.Descripcion })
                .ToListAsync();
            return Ok(servicios);
        }

        // GET: api/sitio/imagenes/{id}  — solo imágenes de proyectos publicados
        [HttpGet("imagenes/{id}")]
        public async Task<IActionResult> Imagen(int id)
        {
            var imagen = await _context.ImagenesPortafolio
                .FirstOrDefaultAsync(i => i.Id == id && i.Proyecto!.Estado == ProyectoPortafolio.Publicado);
            if (imagen == null) return NotFound();

            var ruta = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "portafolio", imagen.Archivo);
            if (!System.IO.File.Exists(ruta)) return NotFound();

            var tipo = Path.GetExtension(imagen.Archivo) == ".png" ? "image/png" : "image/jpeg";
            return PhysicalFile(ruta, tipo);
        }
    }
}
