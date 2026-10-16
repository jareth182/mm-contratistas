using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Controllers
{
    // Mantenimiento del personal de obra (HU10)
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Personal)]
    public class PersonalController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PersonalController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class TrabajadorDto
        {
            public string? Nombres { get; set; }
            public string? Apellidos { get; set; }
            public string? TipoDocumento { get; set; }
            public string? Documento { get; set; }
            public string? Especialidad { get; set; }
            public string? Telefono { get; set; }
            public decimal JornalDiario { get; set; }
        }

        public class EstadoDto
        {
            public bool Activo { get; set; }
        }

        private static object Proyectar(Trabajador t) => new
        {
            id = t.Id,
            nombres = t.Nombres,
            apellidos = t.Apellidos,
            nombreCompleto = t.NombreCompleto,
            tipoDocumento = t.TipoDocumento,
            documento = t.Documento,
            especialidad = t.Especialidad,
            telefono = t.Telefono,
            jornalDiario = t.JornalDiario,
            estado = t.Estado
        };

        private async Task<string?> Validar(TrabajadorDto dto, int? id)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombres)) return "Los nombres son obligatorios.";
            if (string.IsNullOrWhiteSpace(dto.Apellidos)) return "Los apellidos son obligatorios.";
            var tipo = (dto.TipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
            var documento = (dto.Documento ?? string.Empty).Trim();
            if (tipo != "DNI" && tipo != "CE") return "Seleccione el tipo de documento (DNI o CE).";
            if (tipo == "DNI" && !Regex.IsMatch(documento, @"^\d{8}$")) return "El DNI debe tener 8 dígitos.";
            if (tipo == "CE" && !Regex.IsMatch(documento, @"^[A-Za-z0-9]{9,12}$")) return "El carné de extranjería debe tener entre 9 y 12 caracteres.";
            if (!Trabajador.Especialidades.Contains(dto.Especialidad)) return "Seleccione una especialidad de la lista.";
            if (!string.IsNullOrWhiteSpace(dto.Telefono) && !Regex.IsMatch(dto.Telefono.Trim(), @"^\+?[\d\s-]{7,15}$")) return "Ingrese un teléfono válido.";
            if (dto.JornalDiario <= 0 || dto.JornalDiario > 10000) return "Ingrese un jornal diario válido.";
            if (await _context.Personal.AnyAsync(t => t.Id != id && t.Documento == documento))
                return $"Ya existe un colaborador con el documento {documento}.";
            return null;
        }

        private static void Copiar(TrabajadorDto dto, Trabajador t)
        {
            t.Nombres = dto.Nombres!.Trim();
            t.Apellidos = dto.Apellidos!.Trim();
            t.TipoDocumento = dto.TipoDocumento!.Trim().ToUpperInvariant();
            t.Documento = dto.Documento!.Trim().ToUpperInvariant();
            t.Especialidad = dto.Especialidad!;
            t.Telefono = (dto.Telefono ?? string.Empty).Trim();
            t.JornalDiario = Math.Round(dto.JornalDiario, 2);
        }

        // GET: api/personal?q=texto&soloActivos=true  — busca por nombre, documento o especialidad
        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] string? q, [FromQuery] bool soloActivos = false)
        {
            var consulta = _context.Personal.AsQueryable();
            if (soloActivos) consulta = consulta.Where(t => t.Estado == Trabajador.Activo);
            var texto = (q ?? string.Empty).Trim().ToLowerInvariant();
            if (texto.Length > 0)
                consulta = consulta.Where(t => (t.Nombres + " " + t.Apellidos).ToLower().Contains(texto)
                    || t.Documento.ToLower().StartsWith(texto) || t.Especialidad.ToLower().Contains(texto));

            var lista = await consulta.OrderBy(t => t.Estado).ThenBy(t => t.Apellidos).ThenBy(t => t.Nombres).ToListAsync();
            return Ok(lista.Select(Proyectar));
        }

        [HttpGet("especialidades")]
        public IActionResult Especialidades() => Ok(Trabajador.Especialidades);

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] TrabajadorDto dto)
        {
            var error = await Validar(dto, null);
            if (error != null) return BadRequest(new { mensaje = error });

            var trabajador = new Trabajador();
            Copiar(dto, trabajador);
            _context.Personal.Add(trabajador);
            await _context.SaveChangesAsync();

            // Asignaciones anteriores registradas con su documento pasan a su historial
            await _context.Cuadrilla.Where(c => c.TrabajadorId == null && c.Dni == trabajador.Documento)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.TrabajadorId, trabajador.Id));

            return StatusCode(201, new { mensaje = "Colaborador registrado correctamente.", trabajador = Proyectar(trabajador) });
        }

        // La ficha se edita sin tocar sus asignaciones: el historial queda vinculado por id
        [HttpPut("{id}")]
        public async Task<IActionResult> Editar(int id, [FromBody] TrabajadorDto dto)
        {
            var trabajador = await _context.Personal.FindAsync(id);
            if (trabajador == null) return NotFound(new { mensaje = "El colaborador no existe." });

            var error = await Validar(dto, id);
            if (error != null) return BadRequest(new { mensaje = error });

            Copiar(dto, trabajador);
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Datos del colaborador actualizados.", trabajador = Proyectar(trabajador) });
        }

        // Desactivar lo saca de las listas de asignación pero conserva la ficha y su historial
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] EstadoDto dto)
        {
            var trabajador = await _context.Personal.FindAsync(id);
            if (trabajador == null) return NotFound(new { mensaje = "El colaborador no existe." });

            trabajador.Estado = dto.Activo ? Trabajador.Activo : Trabajador.Inactivo;
            await _context.SaveChangesAsync();
            return Ok(new
            {
                mensaje = dto.Activo ? "Colaborador activado." : "Colaborador desactivado. Ya no aparecerá en las listas de asignación.",
                trabajador = Proyectar(trabajador)
            });
        }

        // GET: api/personal/{id}/historial  — obras en las que fue asignado
        [HttpGet("{id}/historial")]
        public async Task<IActionResult> Historial(int id)
        {
            if (!await _context.Personal.AnyAsync(t => t.Id == id)) return NotFound(new { mensaje = "El colaborador no existe." });

            var historial = await _context.Cuadrilla.Where(c => c.TrabajadorId == id)
                .OrderByDescending(c => c.Id)
                .Select(c => new
                {
                    id = c.Id,
                    obraId = c.ObraId,
                    obra = c.Obra != null ? c.Obra.Nombre : "—",
                    estadoObra = c.Obra != null ? c.Obra.Estado : "",
                    especialidad = c.Especialidad,
                    jornal = c.Jornal,
                    fechaIngreso = c.FechaIngresoObra
                })
                .ToListAsync();
            return Ok(historial);
        }
    }
}
