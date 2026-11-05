using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;

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

        // Ficha con el estado de su SCTR y su examen médico (HU23)
        private static object ProyectarConDocumentos(Trabajador t, IReadOnlyDictionary<string, VigenciaDocumento>? ultimas, DateOnly hoy) => new
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
            estado = t.Estado,
            sctr = VigenciasDocumentales.Resumen(ultimas, VigenciaDocumento.Sctr, hoy),
            examenMedico = VigenciasDocumentales.Resumen(ultimas, VigenciaDocumento.ExamenMedico, hoy),
            estadoDocumental = VigenciasDocumentales.EstadoGeneral(ultimas, hoy)
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
            var vigencias = await VigenciasDocumentales.UltimasAsync(_context, lista.Select(t => t.Id).ToList());
            var hoy = DateOnly.FromDateTime(DateTime.Now);
            return Ok(lista.Select(t => ProyectarConDocumentos(t, vigencias.GetValueOrDefault(t.Id), hoy)));
        }

        // GET: api/personal/alertas  — documentación vencida o por vencer del personal activo (HU24)
        [HttpGet("alertas")]
        public async Task<IActionResult> Alertas()
        {
            var alertas = await VigenciasDocumentales.AlertasAsync(_context, DateOnly.FromDateTime(DateTime.Now));
            var ultimoEnvio = await _context.EnviosAlertaDocumental.OrderByDescending(e => e.Fecha)
                .Select(e => new { fecha = e.Fecha, alertas = e.Alertas, destinatarios = e.Destinatarios }).FirstOrDefaultAsync();
            return Ok(new
            {
                resumen = new
                {
                    porVencer = alertas.Count(a => a.Estado == "Por vencer"),
                    vencidos = alertas.Count(a => a.Estado == "Vencido"),
                    total = alertas.Count
                },
                alertas = alertas.Select(a => new
                {
                    trabajadorId = a.TrabajadorId,
                    colaborador = a.Colaborador,
                    documento = a.Documento,
                    vencimiento = a.Vencimiento,
                    diasRestantes = a.DiasRestantes,
                    estado = a.Estado
                }),
                ultimoEnvio
            });
        }

        // GET: api/personal/{id}  — ficha del colaborador con su estado documental
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obtener(int id)
        {
            var trabajador = await _context.Personal.FindAsync(id);
            if (trabajador == null) return NotFound(new { mensaje = "El colaborador no existe." });

            var vigencias = await VigenciasDocumentales.UltimasAsync(_context, new[] { id });
            return Ok(ProyectarConDocumentos(trabajador, vigencias.GetValueOrDefault(id), DateOnly.FromDateTime(DateTime.Now)));
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

        public class VigenciaDto
        {
            public string? Tipo { get; set; }
            public DateOnly? FechaInicio { get; set; }
            public DateOnly? FechaVencimiento { get; set; }
            public IFormFile? Archivo { get; set; }
        }

        private static string CarpetaVigencias() => Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "vigencias");

        private static object Proyectar(VigenciaDocumento v, DateOnly hoy, bool actual) => new
        {
            id = v.Id,
            tipo = v.Tipo,
            fechaInicio = v.FechaInicio,
            fechaVencimiento = v.FechaVencimiento,
            fechaRegistro = v.FechaRegistro,
            estado = VigenciaDocumento.EstadoAl(v.FechaVencimiento, hoy),
            // La vigente de cada documento es la de mayor vencimiento; las demás son el historial
            actual,
            archivo = string.IsNullOrEmpty(v.Archivo) ? null : v.ArchivoNombre
        };

        // POST: api/personal/{id}/vigencias  (multipart: tipo, fechaInicio, fechaVencimiento, archivo)
        // Registra o renueva el SCTR o el examen médico; las vigencias anteriores quedan en el historial
        [HttpPost("{id}/vigencias")]
        public async Task<IActionResult> RegistrarVigencia(int id, [FromForm] VigenciaDto dto)
        {
            if (!await _context.Personal.AnyAsync(t => t.Id == id)) return NotFound(new { mensaje = "El colaborador no existe." });

            var tipo = VigenciaDocumento.Tipos.FirstOrDefault(t => string.Equals(t, dto.Tipo?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (tipo == null) return BadRequest(new { mensaje = "Seleccione el documento: SCTR o Examen Médico." });
            if (dto.FechaInicio == null || dto.FechaVencimiento == null) return BadRequest(new { mensaje = "Ingrese las fechas de inicio y de vencimiento." });
            if (dto.FechaVencimiento <= dto.FechaInicio) return BadRequest(new { mensaje = "El vencimiento debe ser posterior al inicio de la vigencia." });

            // Una renovación debe extender la cobertura: si no, la nueva vigencia quedaría oculta tras la anterior
            var vigente = await _context.Vigencias.Where(v => v.TrabajadorId == id && v.Tipo == tipo)
                .OrderByDescending(v => v.FechaVencimiento).ThenByDescending(v => v.Id).FirstOrDefaultAsync();
            if (vigente != null && dto.FechaVencimiento <= vigente.FechaVencimiento)
                return BadRequest(new { mensaje = $"La nueva vigencia debe vencer después de la actual ({vigente.FechaVencimiento:dd/MM/yyyy})." });

            if (dto.Archivo == null || dto.Archivo.Length == 0) return BadRequest(new { mensaje = $"Adjunte el documento del {tipo} (PDF, JPG o PNG)." });
            var nombreOriginal = Path.GetFileName(dto.Archivo.FileName);
            if (nombreOriginal.Length > 150) return BadRequest(new { mensaje = "El nombre del archivo no puede superar los 150 caracteres." });
            var errorArchivo = await ObrasController.ValidarDocumento(dto.Archivo);
            if (errorArchivo != null) return BadRequest(new { mensaje = errorArchivo });

            var guardado = $"{Guid.NewGuid():N}{Path.GetExtension(dto.Archivo.FileName).ToLowerInvariant()}";
            Directory.CreateDirectory(CarpetaVigencias());
            await using (var destino = new FileStream(Path.Combine(CarpetaVigencias(), guardado), FileMode.Create))
                await dto.Archivo.CopyToAsync(destino);

            var vigencia = new VigenciaDocumento
            {
                TrabajadorId = id,
                Tipo = tipo,
                FechaInicio = dto.FechaInicio.Value,
                FechaVencimiento = dto.FechaVencimiento.Value,
                FechaRegistro = DateTime.UtcNow,
                Archivo = guardado,
                ArchivoNombre = nombreOriginal
            };
            _context.Vigencias.Add(vigencia);
            await _context.SaveChangesAsync();

            var mensaje = vigente == null ? $"{tipo} registrado correctamente." : $"{tipo} renovado correctamente. La vigencia anterior quedó en el historial.";
            return StatusCode(201, new { mensaje, vigencia = Proyectar(vigencia, DateOnly.FromDateTime(DateTime.Now), actual: true) });
        }

        // GET: api/personal/{id}/vigencias  — vigente e historial de cada documento, de la más reciente a la más antigua
        [HttpGet("{id}/vigencias")]
        public async Task<IActionResult> Vigencias(int id)
        {
            if (!await _context.Personal.AnyAsync(t => t.Id == id)) return NotFound(new { mensaje = "El colaborador no existe." });

            var hoy = DateOnly.FromDateTime(DateTime.Now);
            var vigencias = await _context.Vigencias.Where(v => v.TrabajadorId == id)
                .OrderByDescending(v => v.FechaVencimiento).ThenByDescending(v => v.Id).ToListAsync();
            var actuales = vigencias.GroupBy(v => v.Tipo).Select(g => g.First().Id).ToHashSet();
            return Ok(vigencias.Select(v => Proyectar(v, hoy, actuales.Contains(v.Id))));
        }

        // GET: api/personal/{id}/vigencias/{vigenciaId}/archivo
        [HttpGet("{id}/vigencias/{vigenciaId}/archivo")]
        public async Task<IActionResult> DescargarDocumentoVigencia(int id, int vigenciaId)
        {
            var vigencia = await _context.Vigencias.FirstOrDefaultAsync(v => v.Id == vigenciaId && v.TrabajadorId == id);
            if (vigencia == null || string.IsNullOrEmpty(vigencia.Archivo)) return NotFound("La vigencia no tiene documento adjunto.");

            var ruta = Path.Combine(CarpetaVigencias(), Path.GetFileName(vigencia.Archivo));
            if (!System.IO.File.Exists(ruta)) return NotFound("El archivo físico no se encontró.");
            return PhysicalFile(ruta, ObrasController.TipoContenido(vigencia.ArchivoNombre), vigencia.ArchivoNombre);
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
                    fechaIngreso = c.FechaIngresoObra,
                    fechaSalida = c.FechaSalida
                })
                .ToListAsync();
            return Ok(historial);
        }
    }
}
