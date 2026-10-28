using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;

namespace MymContratistas.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Personal)]
    public class ObrasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Usuario> _hasher;
        private readonly InvitacionService _invitacion;
        private readonly NotificacionHitoService _notificacion;

        public ObrasController(ApplicationDbContext context, IPasswordHasher<Usuario> hasher, InvitacionService invitacion, NotificacionHitoService notificacion)
        {
            _context = context;
            _hasher = hasher;
            _invitacion = invitacion;
            _notificacion = notificacion;
        }

        public class ObraDto
        {
            public string? Nombre { get; set; }
            public int ClienteId { get; set; }
            public string? Ubicacion { get; set; }
            public string? Descripcion { get; set; }
            public DateOnly? FechaInicio { get; set; }
            public DateOnly? FechaFinEstimada { get; set; }
        }

        public class FinalizarDto
        {
            public DateOnly? FechaCierre { get; set; }
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
        // Crea el expediente en estado «En Ejecución» y habilita el panel de seguimiento del cliente
        [HttpPost]
        public async Task<IActionResult> PostObra([FromBody] ObraDto dto)
        {
            var nombre = (dto.Nombre ?? string.Empty).Trim();
            if (nombre.Length == 0) return BadRequest(new { mensaje = "El nombre del proyecto es obligatorio." });

            var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
            if (cliente == null) return BadRequest(new { mensaje = "Seleccione un cliente registrado." });

            if (dto.FechaInicio == null) return BadRequest(new { mensaje = "La fecha de inicio es obligatoria." });
            if (dto.FechaFinEstimada != null && dto.FechaFinEstimada < dto.FechaInicio)
                return BadRequest(new { mensaje = "La fecha estimada de fin no puede ser anterior a la de inicio." });

            var obra = new Obra
            {
                Nombre = nombre,
                Cliente = cliente.Nombre,
                ClienteId = cliente.Id,
                Ubicacion = (dto.Ubicacion ?? string.Empty).Trim(),
                Descripcion = (dto.Descripcion ?? string.Empty).Trim(),
                FechaInicio = dto.FechaInicio,
                FechaFinEstimada = dto.FechaFinEstimada,
                Estado = Obra.EnEjecucion,
                AvancePorcentaje = 0
            };
            _context.Obras.Add(obra);

            var panel = await PrepararPanelClienteAsync(cliente);
            await _context.SaveChangesAsync();

            var correoEnviado = panel.Invitar != null && await _invitacion.EnviarAsync(panel.Invitar.Value.usuario, panel.Invitar.Value.token);

            var mensaje = "Proyecto registrado en estado En Ejecución. " + panel.Aviso;
            if (panel.Invitar != null)
                mensaje += correoEnviado
                    ? " Se envió el enlace para que defina su contraseña."
                    : " No se pudo enviar el correo: use «Recuperar contraseña» para que defina su clave.";

            return StatusCode(201, new { mensaje, panelHabilitado = panel.Habilitado, correoEnviado, obra });
        }

        // PUT: api/obras/{id}/finalizar
        [HttpPut("{id}/finalizar")]
        public async Task<IActionResult> Finalizar(int id, [FromBody] FinalizarDto? dto)
        {
            var obra = await _context.Obras.FindAsync(id);
            if (obra == null) return NotFound(new { mensaje = "La obra no existe." });
            if (obra.Estado == Obra.Finalizado) return Conflict(new { mensaje = "La obra ya está finalizada." });

            var hoy = DateOnly.FromDateTime(DateTime.Now);
            var cierre = dto?.FechaCierre ?? hoy;
            if (cierre > hoy) return BadRequest(new { mensaje = "La fecha de cierre no puede ser futura." });
            if (obra.FechaInicio != null && cierre < obra.FechaInicio)
                return BadRequest(new { mensaje = "La fecha de cierre no puede ser anterior a la fecha de inicio de la obra." });

            obra.Estado = Obra.Finalizado;
            obra.FechaCierre = cierre;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"Obra finalizada con fecha de cierre {cierre:dd/MM/yyyy}.", obra });
        }

        // Garantiza que el cliente tenga una cuenta con rol Cliente vinculada a su ficha.
        // La invitación se envía después de guardar, para no avisar de algo que luego falle.
        private async Task<(bool Habilitado, string Aviso, (Usuario usuario, string token)? Invitar)> PrepararPanelClienteAsync(Cliente cliente)
        {
            var vinculado = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.ClienteId == cliente.Id);
            if (vinculado != null)
            {
                return vinculado.Estado == "Activo"
                    ? (true, "El cliente ya tiene acceso a su panel de seguimiento.", null)
                    : (false, "La cuenta del cliente está inactiva: actívela en Usuarios para que vea su panel.", null);
            }

            var correo = (cliente.Correo ?? string.Empty).Trim().ToLowerInvariant();
            if (correo.Length == 0)
                return (false, "El cliente no tiene correo registrado, por lo que su panel de seguimiento aún no está habilitado.", null);

            var existente = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Correo.ToLower() == correo);
            if (existente != null)
            {
                if (existente.Rol.Nombre != NombresRol.Cliente)
                    return (false, "El correo del cliente pertenece a un usuario del personal; su panel no se habilitó.", null);
                existente.ClienteId = cliente.Id;
                return existente.Estado == "Activo"
                    ? (true, "El cliente ya tiene acceso a su panel de seguimiento.", null)
                    : (false, "La cuenta del cliente está inactiva: actívela en Usuarios para que vea su panel.", null);
            }

            var rol = await _context.Roles.FirstAsync(r => r.Nombre == NombresRol.Cliente);
            var usuario = new Usuario { Nombres = cliente.Nombre, Correo = correo, RolId = rol.Id, Rol = rol, ClienteId = cliente.Id };
            // Contraseña aleatoria inutilizable: el cliente define la suya con el enlace del correo
            usuario.PasswordHash = _hasher.HashPassword(usuario, Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Guid.NewGuid());
            var token = EnlaceTokens.Generar(usuario, TimeSpan.FromHours(InvitacionService.HorasVigencia));
            _context.Usuarios.Add(usuario);

            return (true, "El cliente ya tiene acceso a su panel de seguimiento.", (usuario, token));
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

        public const long TamanoMaximoFoto = 5 * 1024 * 1024;
        public const int MaximoFotosPorAvance = 5;
        private const int MaximoDescripcionAvance = 1000;

        private static string CarpetaAvances() => Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "avances");

        private static object Proyectar(AvanceHito a) => new
        {
            id = a.Id,
            porcentaje = a.Porcentaje,
            descripcion = a.Descripcion,
            fecha = a.Fecha,
            responsable = a.Responsable,
            fotos = a.Fotos.OrderBy(f => f.Id).Select(f => new { id = f.Id, nombre = f.NombreOriginal })
        };

        // Devuelve el mensaje de error o null si todas las fotografías son JPG/PNG válidas
        private static async Task<string?> ValidarFotos(List<IFormFile> fotos)
        {
            if (fotos.Count > MaximoFotosPorAvance) return $"Solo puede adjuntar hasta {MaximoFotosPorAvance} fotografías por avance.";
            foreach (var foto in fotos)
            {
                var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                    return $"«{foto.FileName}» no es una imagen JPG o PNG.";
                if (foto.Length > TamanoMaximoFoto) return $"«{foto.FileName}» supera el tamaño máximo de 5 MB.";

                var inicio = new byte[8];
                await using var lectura = foto.OpenReadStream();
                var leidos = await lectura.ReadAsync(inicio.AsMemory(0, inicio.Length));
                if (!PortafolioController.EsImagenValida(inicio.AsSpan(0, leidos).ToArray(), extension))
                    return $"«{foto.FileName}» no es una imagen JPG o PNG válida.";
            }
            return null;
        }

        // POST: api/obras/hitos/{id}/avances  (multipart: porcentaje, descripcion, fotos)
        [HttpPost("hitos/{id}/avances")]
        public async Task<IActionResult> RegistrarAvance(int id, [FromForm] int porcentaje, [FromForm] string? descripcion, [FromForm] List<IFormFile>? fotos)
        {
            var hito = await _context.Hitos.FindAsync(id);
            if (hito == null) return NotFound("El hito no existe.");
            if (hito.Estado == "Completado") return BadRequest("El hito ya está completado y no admite más avances.");
            if (porcentaje < 1 || porcentaje > 100) return BadRequest("El porcentaje de avance debe estar entre 1 y 100.");
            if (porcentaje <= hito.AvancePorcentaje) return BadRequest($"El porcentaje debe ser mayor al avance actual del hito ({hito.AvancePorcentaje}%).");
            if (string.IsNullOrWhiteSpace(descripcion)) return BadRequest("La descripción del trabajo realizado es obligatoria.");
            if (descripcion.Trim().Length > MaximoDescripcionAvance) return BadRequest($"La descripción no puede superar los {MaximoDescripcionAvance} caracteres.");

            fotos ??= new List<IFormFile>();
            var errorFotos = await ValidarFotos(fotos);
            if (errorFotos != null) return BadRequest(errorFotos);

            var avance = new AvanceHito
            {
                HitoId = id,
                Porcentaje = porcentaje,
                Descripcion = descripcion.Trim(),
                Fecha = DateTime.UtcNow,
                Responsable = User?.FindFirstValue(ClaimTypes.Name) ?? "—"
            };

            Directory.CreateDirectory(CarpetaAvances());
            foreach (var foto in fotos)
            {
                var guardado = $"{Guid.NewGuid():N}{Path.GetExtension(foto.FileName).ToLowerInvariant()}";
                await using (var destino = new FileStream(Path.Combine(CarpetaAvances(), guardado), FileMode.Create))
                    await foto.CopyToAsync(destino);
                avance.Fotos.Add(new FotoAvance { Archivo = guardado, NombreOriginal = Path.GetFileName(foto.FileName) });
            }
            _context.AvancesHito.Add(avance);

            hito.AvancePorcentaje = porcentaje;
            hito.DescripcionAvance = avance.Descripcion;
            var completado = porcentaje == 100;
            hito.Estado = completado ? "Completado" : "En proceso";
            if (completado) hito.FechaReal ??= DateOnly.FromDateTime(DateTime.Now);

            // El avance de la obra es el promedio de los hitos ponderado por su peso
            var obra = await _context.Obras.Include(o => o.ClienteRegistro).FirstAsync(o => o.Id == hito.ObraId);
            var hitos = await _context.Hitos.Where(h => h.ObraId == hito.ObraId).ToListAsync();
            obra.AvancePorcentaje = Math.Min(PesoTotal, (int)Math.Round(hitos.Sum(h => h.AvancePorcentaje * h.Peso) / (double)PesoTotal, MidpointRounding.AwayFromZero));
            await _context.SaveChangesAsync();

            var mensaje = "Avance registrado correctamente.";
            if (completado)
            {
                var correo = obra.ClienteRegistro?.Correo;
                if (string.IsNullOrWhiteSpace(correo))
                    mensaje += " El hito quedó completado; el cliente no tiene correo registrado, por lo que no se le notificó.";
                else if (await _notificacion.HitoCompletadoAsync(obra, hito, correo))
                    mensaje += " El hito quedó completado y se notificó al cliente por correo.";
                else
                    mensaje += " El hito quedó completado, pero no se pudo enviar el correo al cliente.";
            }
            return Ok(new { mensaje, hito, avanceObra = obra.AvancePorcentaje, avance = Proyectar(avance) });
        }

        // GET: api/obras/hitos/{id}/avances  (del más reciente al más antiguo)
        [HttpGet("hitos/{id}/avances")]
        public async Task<IActionResult> HistorialAvances(int id)
        {
            var hito = await _context.Hitos.FindAsync(id);
            if (hito == null) return NotFound("El hito no existe.");

            var avances = await _context.AvancesHito.Include(a => a.Fotos)
                .Where(a => a.HitoId == id).OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id).ToListAsync();
            return Ok(avances.Select(Proyectar));
        }

        // GET: api/obras/avances/fotos/{id}
        [HttpGet("avances/fotos/{id}")]
        public async Task<IActionResult> VerFotoAvance(int id)
        {
            var foto = await _context.FotosAvance.FindAsync(id);
            if (foto == null) return NotFound("La fotografía no existe.");

            var ruta = Path.Combine(CarpetaAvances(), foto.Archivo);
            if (!System.IO.File.Exists(ruta)) return NotFound("El archivo físico no se encontró.");

            return PhysicalFile(ruta, Path.GetExtension(foto.Archivo) == ".png" ? "image/png" : "image/jpeg");
        }

        public class SalidaDto
        {
            public DateOnly? FechaSalida { get; set; }
        }

        private static readonly string[] FormatosFecha = { "dd/MM/yyyy", "d/M/yyyy" };

        private static DateOnly? FechaIngreso(Colaborador c) =>
            DateOnly.TryParseExact(c.FechaIngresoObra, FormatosFecha, null, System.Globalization.DateTimeStyles.None, out var f) ? f : null;

        // GET: api/obras/{obraId}/cuadrilla/disponibles?buscar=  (personal activo que aún no está en la cuadrilla de la obra)
        [HttpGet("{obraId}/cuadrilla/disponibles")]
        public async Task<IActionResult> ColaboradoresDisponibles(int obraId, [FromQuery] string? buscar)
        {
            if (!await _context.Obras.AnyAsync(o => o.Id == obraId)) return NotFound("La obra no existe.");

            var enObra = await _context.Cuadrilla.Where(c => c.ObraId == obraId && c.FechaSalida == null && c.TrabajadorId != null)
                .Select(c => c.TrabajadorId!.Value).ToListAsync();
            var candidatos = _context.Personal.Where(t => t.Estado == Trabajador.Activo && !enObra.Contains(t.Id));

            var texto = (buscar ?? string.Empty).Trim().ToLower();
            if (texto.Length > 0)
                candidatos = candidatos.Where(t => (t.Nombres + " " + t.Apellidos).ToLower().Contains(texto)
                    || t.Documento.ToLower().Contains(texto) || t.Especialidad.ToLower().Contains(texto));

            var lista = await candidatos.OrderBy(t => t.Apellidos).ThenBy(t => t.Nombres).ToListAsync();
            var vigencias = await VigenciasDocumentales.UltimasAsync(_context, lista.Select(t => t.Id).ToList());
            var hoy = DateOnly.FromDateTime(DateTime.Now);

            return Ok(lista.Select(t =>
            {
                vigencias.TryGetValue(t.Id, out var ultimas);
                object Documento(string tipo) => ultimas != null && ultimas.TryGetValue(tipo, out var v)
                    ? new { estado = VigenciaDocumento.EstadoAl(v.FechaVencimiento, hoy), vencimiento = (DateOnly?)v.FechaVencimiento }
                    : new { estado = "Sin registrar", vencimiento = (DateOnly?)null };
                var impedimento = VigenciasDocumentales.Impedimento(t.NombreCompleto, ultimas, hoy);
                return new
                {
                    id = t.Id,
                    nombreCompleto = t.NombreCompleto,
                    documento = t.Documento,
                    especialidad = t.Especialidad,
                    jornalDiario = t.JornalDiario,
                    sctr = Documento(VigenciaDocumento.Sctr),
                    examenMedico = Documento(VigenciaDocumento.ExamenMedico),
                    apto = impedimento == null,
                    motivo = impedimento
                };
            }));
        }

        // POST: api/obras/{obraId}/cuadrilla
        [HttpPost("{obraId}/cuadrilla")]
        public async Task<ActionResult<Colaborador>> AsignarColaborador(int obraId, Colaborador colaborador)
        {
            var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null) return NotFound("La obra no existe.");
            if (obra.Estado == Obra.Finalizado) return BadRequest("La obra está finalizada y no admite nuevas asignaciones.");
            if (colaborador.TrabajadorId == null) return BadRequest("Seleccione un colaborador de la lista de personal.");

            // Elegido de la ficha del personal (HU10): solo colaboradores activos y con sus datos registrados
            var trabajador = await _context.Personal.FindAsync(colaborador.TrabajadorId);
            if (trabajador == null) return BadRequest("El colaborador no existe.");
            if (trabajador.Estado != Trabajador.Activo) return BadRequest("El colaborador está inactivo y no puede asignarse.");
            if (await _context.Cuadrilla.AnyAsync(c => c.ObraId == obraId && c.TrabajadorId == trabajador.Id && c.FechaSalida == null))
                return BadRequest("El colaborador ya está asignado a esta obra.");

            // SCTR y examen médico deben estar vigentes
            var hoy = DateOnly.FromDateTime(DateTime.Now);
            var vigencias = await VigenciasDocumentales.UltimasAsync(_context, new[] { trabajador.Id });
            vigencias.TryGetValue(trabajador.Id, out var ultimas);
            var impedimento = VigenciasDocumentales.Impedimento(trabajador.NombreCompleto, ultimas, hoy);
            if (impedimento != null) return BadRequest(impedimento);

            var asignacion = new Colaborador
            {
                ObraId = obraId,
                TrabajadorId = trabajador.Id,
                NombreCompleto = trabajador.NombreCompleto,
                Dni = trabajador.Documento,
                Especialidad = trabajador.Especialidad,
                Jornal = colaborador.Jornal > 0 ? colaborador.Jornal : trabajador.JornalDiario,
                EstadoSctr = VigenciaDocumento.EstadoAl(ultimas![VigenciaDocumento.Sctr].FechaVencimiento, hoy),
                FechaIngresoObra = hoy.ToString("dd/MM/yyyy")
            };

            _context.Cuadrilla.Add(asignacion);
            await _context.SaveChangesAsync();

            return Ok(asignacion);
        }

        // PUT: api/obras/cuadrilla/{id}/salida
        // La salida no borra la asignación: queda en el historial de participación del colaborador
        [HttpPut("cuadrilla/{id}/salida")]
        public async Task<IActionResult> RegistrarSalida(int id, [FromBody] SalidaDto? dto)
        {
            var colaborador = await _context.Cuadrilla.FindAsync(id);
            if (colaborador == null) return NotFound("El colaborador no existe.");
            if (colaborador.FechaSalida != null) return BadRequest("La salida de este colaborador ya fue registrada.");

            var hoy = DateOnly.FromDateTime(DateTime.Now);
            var salida = dto?.FechaSalida ?? hoy;
            if (salida > hoy) return BadRequest("La fecha de salida no puede ser futura.");
            if (FechaIngreso(colaborador) is { } ingreso && salida < ingreso)
                return BadRequest($"La fecha de salida no puede ser anterior al ingreso ({ingreso:dd/MM/yyyy}).");

            colaborador.FechaSalida = salida;
            await _context.SaveChangesAsync();
            return Ok(colaborador);
        }

        public const long TamanoMaximoDocumento = 10 * 1024 * 1024;

        private static string CarpetaDocumentos() => Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "documentos");

        // Ruta del archivo físico; los documentos cargados antes de HU15 se guardaban con su nombre en Uploads
        internal static string RutaDocumento(Documento doc) => string.IsNullOrEmpty(doc.Archivo)
            ? Path.Combine(Directory.GetCurrentDirectory(), "Uploads", Path.GetFileName(doc.Nombre))
            : Path.Combine(CarpetaDocumentos(), doc.Archivo);

        internal static string TipoContenido(string nombre) => Path.GetExtension(nombre).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        // Devuelve el mensaje de error o null si el archivo es un PDF, JPG o PNG válido de hasta 10 MB
        private static async Task<string?> ValidarDocumento(IFormFile archivo)
        {
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (extension != ".pdf" && extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                return "Solo se permiten archivos PDF, JPG o PNG.";
            if (archivo.Length > TamanoMaximoDocumento) return "El archivo supera el tamaño máximo de 10 MB.";

            var inicio = new byte[8];
            await using var lectura = archivo.OpenReadStream();
            var leidos = await lectura.ReadAsync(inicio.AsMemory(0, inicio.Length));
            var contenido = inicio.AsSpan(0, leidos).ToArray();
            var valido = extension == ".pdf"
                ? leidos >= 4 && contenido[0] == 0x25 && contenido[1] == 0x50 && contenido[2] == 0x44 && contenido[3] == 0x46   // %PDF
                : PortafolioController.EsImagenValida(contenido, extension);
            return valido ? null : "El contenido del archivo no corresponde a un PDF, JPG o PNG válido.";
        }

        // POST: api/obras/{obraId}/documentos
        [HttpPost("{obraId}/documentos")]
        public async Task<IActionResult> SubirDocumento(int obraId, [FromForm] string? clasificacion, [FromForm] bool visibleCliente, [FromForm] IFormFile? archivo)
        {
            var obra = await _context.Obras.FindAsync(obraId);
            if (obra == null) return NotFound("La obra no existe.");
            if (archivo == null || archivo.Length == 0) return BadRequest("Debe adjuntar un archivo.");

            var tipo = Documento.Clasificaciones.FirstOrDefault(c => string.Equals(c, clasificacion?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (tipo == null) return BadRequest($"Seleccione una clasificación: {string.Join(", ", Documento.Clasificaciones)}.");

            var error = await ValidarDocumento(archivo);
            if (error != null) return BadRequest(error);

            // El archivo se guarda con un nombre propio: así no se sobrescriben documentos con el mismo nombre
            var guardado = $"{Guid.NewGuid():N}{Path.GetExtension(archivo.FileName).ToLowerInvariant()}";
            Directory.CreateDirectory(CarpetaDocumentos());
            await using (var destino = new FileStream(Path.Combine(CarpetaDocumentos(), guardado), FileMode.Create))
                await archivo.CopyToAsync(destino);

            var doc = new Documento
            {
                Nombre = Path.GetFileName(archivo.FileName),
                Archivo = guardado,
                Clasificacion = tipo,
                VisibleCliente = visibleCliente,
                FechaCarga = DateTime.Now.ToString("dd/MM/yyyy"),
                ObraId = obraId
            };

            _context.Documentos.Add(doc);
            await _context.SaveChangesAsync();

            return Ok(doc);
        }

        // PUT: api/obras/documentos/{id}/visibilidad
        [HttpPut("documentos/{id}/visibilidad")]
        public async Task<IActionResult> CambiarVisibilidadDocumento(int id, [FromBody] VisibilidadDto dto)
        {
            var doc = await _context.Documentos.FindAsync(id);
            if (doc == null) return NotFound("Documento no encontrado.");

            doc.VisibleCliente = dto.VisibleCliente;
            await _context.SaveChangesAsync();
            return Ok(doc);
        }

        public class VisibilidadDto
        {
            public bool VisibleCliente { get; set; }
        }

        // GET: api/obras/documentos/{id}/descargar
        [HttpGet("documentos/{id}/descargar")]
        public async Task<IActionResult> DescargarDocumento(int id)
        {
            var doc = await _context.Documentos.FindAsync(id);
            if (doc == null) return NotFound("Documento no encontrado.");

            var ruta = RutaDocumento(doc);
            if (!System.IO.File.Exists(ruta)) return NotFound("El archivo físico no se encontró.");

            return PhysicalFile(ruta, TipoContenido(doc.Nombre), doc.Nombre);
        }

        // DELETE: api/obras/documentos/{id}
        [HttpDelete("documentos/{id}")]
        public async Task<IActionResult> EliminarDocumento(int id)
        {
            var doc = await _context.Documentos.FindAsync(id);
            if (doc == null) return NotFound("Documento no encontrado.");

            var ruta = RutaDocumento(doc);
            _context.Documentos.Remove(doc);
            await _context.SaveChangesAsync();

            // Solo se borran los archivos propios de HU15; los antiguos pueden compartir nombre con otros
            if (!string.IsNullOrEmpty(doc.Archivo) && System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);

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