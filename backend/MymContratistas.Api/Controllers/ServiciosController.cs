using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Controllers
{
    // Mantenimiento del catálogo de servicios del sitio web (HU09)
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Administrador + "," + NombresRol.Gerente)]
    public class ServiciosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ServiciosController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class ServicioDto
        {
            public string? Nombre { get; set; }
            public string? Descripcion { get; set; }
            public string? Beneficios { get; set; }
            public string? Caracteristicas { get; set; }
            public int? Orden { get; set; }
            public string? Estado { get; set; }
        }

        public static object Proyectar(Servicio s) => new
        {
            id = s.Id,
            nombre = s.Nombre,
            descripcion = s.Descripcion,
            beneficios = s.Beneficios,
            caracteristicas = s.Caracteristicas,
            orden = s.Orden,
            estado = s.Estado
        };

        // Quita líneas vacías y espacios sobrantes de las listas escritas una por línea
        private static string Lineas(string? texto) =>
            string.Join("\n", (texto ?? string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));

        private async Task<string?> Validar(ServicioDto dto, int? id)
        {
            var nombre = (dto.Nombre ?? string.Empty).Trim();
            if (nombre.Length == 0) return "El nombre del servicio es obligatorio.";
            if (string.IsNullOrWhiteSpace(dto.Descripcion)) return "La descripción breve es obligatoria.";
            if (dto.Estado != null && dto.Estado != Servicio.Publicado && dto.Estado != Servicio.Oculto) return "Estado no válido.";
            if (dto.Orden is < 1) return "El orden debe ser 1 o mayor.";
            var repetido = await _context.Servicios.AnyAsync(s => s.Id != id && s.Nombre.ToLower() == nombre.ToLower());
            return repetido ? "Ya existe un servicio con ese nombre." : null;
        }

        // Coloca el servicio en la posición pedida y deja el orden correlativo (1, 2, 3...)
        private async Task Reordenar(Servicio movido, int? posicion)
        {
            var otros = await _context.Servicios.Where(s => s.Id != movido.Id).OrderBy(s => s.Orden).ThenBy(s => s.Id).ToListAsync();
            var indice = Math.Clamp((posicion ?? otros.Count + 1) - 1, 0, otros.Count);
            otros.Insert(indice, movido);
            for (var i = 0; i < otros.Count; i++) otros[i].Orden = i + 1;
        }

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var servicios = await _context.Servicios.OrderBy(s => s.Orden).ThenBy(s => s.Id).ToListAsync();
            return Ok(servicios.Select(Proyectar));
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] ServicioDto dto)
        {
            var error = await Validar(dto, null);
            if (error != null) return BadRequest(new { mensaje = error });

            var servicio = new Servicio
            {
                Nombre = dto.Nombre!.Trim(),
                Descripcion = dto.Descripcion!.Trim(),
                Beneficios = Lineas(dto.Beneficios),
                Caracteristicas = Lineas(dto.Caracteristicas),
                Estado = dto.Estado ?? Servicio.Publicado
            };
            _context.Servicios.Add(servicio);
            await Reordenar(servicio, dto.Orden);
            await _context.SaveChangesAsync();

            return StatusCode(201, new { mensaje = "Servicio registrado correctamente.", servicio = Proyectar(servicio) });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Editar(int id, [FromBody] ServicioDto dto)
        {
            var servicio = await _context.Servicios.FindAsync(id);
            if (servicio == null) return NotFound(new { mensaje = "El servicio no existe." });

            var error = await Validar(dto, id);
            if (error != null) return BadRequest(new { mensaje = error });

            servicio.Nombre = dto.Nombre!.Trim();
            servicio.Descripcion = dto.Descripcion!.Trim();
            servicio.Beneficios = Lineas(dto.Beneficios);
            servicio.Caracteristicas = Lineas(dto.Caracteristicas);
            servicio.Estado = dto.Estado ?? servicio.Estado;
            await Reordenar(servicio, dto.Orden ?? servicio.Orden);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Servicio actualizado correctamente.", servicio = Proyectar(servicio) });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var servicio = await _context.Servicios.FindAsync(id);
            if (servicio == null) return NotFound(new { mensaje = "El servicio no existe." });

            _context.Servicios.Remove(servicio);
            await _context.SaveChangesAsync();

            // Cierra el hueco que dejó en el orden
            var restantes = await _context.Servicios.OrderBy(s => s.Orden).ThenBy(s => s.Id).ToListAsync();
            for (var i = 0; i < restantes.Count; i++) restantes[i].Orden = i + 1;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Servicio eliminado del catálogo." });
        }
    }
}
