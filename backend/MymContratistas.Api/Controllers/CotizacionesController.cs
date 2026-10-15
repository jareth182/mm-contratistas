using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;

namespace MymContratistas.Api.Controllers
{
    // Solicitudes de cotización del sitio público (HU07)
    [Route("api/[controller]")]
    [ApiController]
    public class CotizacionesController : ControllerBase
    {
        public const long TamanoMaximoAdjunto = 10 * 1024 * 1024;
        public const int MaximoAdjuntos = 5;
        private const long LimitePeticion = MaximoAdjuntos * TamanoMaximoAdjunto + 1024 * 1024;
        private static readonly string[] ExtensionesPermitidas = { ".pdf", ".jpg", ".jpeg", ".png" };

        private readonly ApplicationDbContext _context;
        private readonly IEmailService _email;
        private readonly ILogger<CotizacionesController> _logger;

        public CotizacionesController(ApplicationDbContext context, IEmailService email, ILogger<CotizacionesController> logger)
        {
            _context = context;
            _email = email;
            _logger = logger;
        }

        public class SolicitudDto
        {
            public string? Nombres { get; set; }
            public string? Telefono { get; set; }
            public string? Correo { get; set; }
            public int? ServicioId { get; set; }
            public string? Distrito { get; set; }
            public string? Descripcion { get; set; }
        }

        private static string CarpetaAdjuntos() => Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "cotizaciones");

        private static bool CorreoValido(string correo)
        {
            try { return new MailAddress(correo).Address == correo && correo.Contains('.'); }
            catch (FormatException) { return false; }
        }

        // La firma del archivo debe coincidir con su extensión
        private static bool ContenidoValido(byte[] inicio, string extension) => extension switch
        {
            ".pdf" => inicio.Length >= 4 && inicio[0] == 0x25 && inicio[1] == 0x50 && inicio[2] == 0x44 && inicio[3] == 0x46,
            ".png" => inicio.Length >= 4 && inicio[0] == 0x89 && inicio[1] == 0x50 && inicio[2] == 0x4E && inicio[3] == 0x47,
            _ => inicio.Length >= 3 && inicio[0] == 0xFF && inicio[1] == 0xD8 && inicio[2] == 0xFF
        };

        // POST: api/cotizaciones  (multipart: campos del formulario + "archivos")
        [HttpPost]
        [AllowAnonymous]
        [RequestSizeLimit(LimitePeticion)]
        [RequestFormLimits(MultipartBodyLengthLimit = LimitePeticion)]
        public async Task<IActionResult> Registrar([FromForm] SolicitudDto dto, [FromForm] List<IFormFile>? archivos)
        {
            var nombres = (dto.Nombres ?? string.Empty).Trim();
            var telefono = (dto.Telefono ?? string.Empty).Trim();
            var correo = (dto.Correo ?? string.Empty).Trim().ToLowerInvariant();
            var distrito = (dto.Distrito ?? string.Empty).Trim();
            var descripcion = (dto.Descripcion ?? string.Empty).Trim();
            archivos ??= new List<IFormFile>();

            var faltantes = new List<string>();
            if (nombres.Length == 0) faltantes.Add("nombres y apellidos");
            if (telefono.Length == 0) faltantes.Add("teléfono");
            if (correo.Length == 0) faltantes.Add("correo electrónico");
            if (dto.ServicioId == null) faltantes.Add("tipo de servicio");
            if (distrito.Length == 0) faltantes.Add("distrito");
            if (descripcion.Length == 0) faltantes.Add("descripción del trabajo");
            if (faltantes.Count > 0) return BadRequest(new { mensaje = $"Complete los campos obligatorios: {string.Join(", ", faltantes)}." });

            if (!Regex.IsMatch(telefono, @"^\+?[\d\s-]{7,15}$")) return BadRequest(new { mensaje = "Ingrese un teléfono válido." });
            if (!CorreoValido(correo)) return BadRequest(new { mensaje = "Ingrese un correo electrónico válido." });

            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == dto.ServicioId && s.Estado == Servicio.Publicado);
            if (servicio == null) return BadRequest(new { mensaje = "Seleccione un tipo de servicio válido." });

            if (archivos.Count > MaximoAdjuntos) return BadRequest(new { mensaje = $"Puede adjuntar como máximo {MaximoAdjuntos} archivos." });
            var contenidos = new List<(IFormFile archivo, string extension, byte[] datos)>();
            foreach (var archivo in archivos)
            {
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (!ExtensionesPermitidas.Contains(extension))
                    return BadRequest(new { mensaje = $"«{archivo.FileName}» no es válido. Solo se aceptan PDF, JPG o PNG." });
                if (archivo.Length == 0) return BadRequest(new { mensaje = $"«{archivo.FileName}» está vacío." });
                if (archivo.Length > TamanoMaximoAdjunto) return BadRequest(new { mensaje = $"«{archivo.FileName}» supera el máximo de 10 MB." });

                using var memoria = new MemoryStream();
                await archivo.CopyToAsync(memoria);
                var datos = memoria.ToArray();
                if (!ContenidoValido(datos, extension))
                    return BadRequest(new { mensaje = $"«{archivo.FileName}» no es un archivo PDF, JPG o PNG válido." });
                contenidos.Add((archivo, extension, datos));
            }

            var solicitud = new SolicitudCotizacion
            {
                Nombres = nombres, Telefono = telefono, Correo = correo,
                ServicioId = servicio.Id, ServicioNombre = servicio.Nombre,
                Distrito = distrito, Descripcion = descripcion,
                FechaRegistro = DateTime.UtcNow
            };

            Directory.CreateDirectory(CarpetaAdjuntos());
            foreach (var (archivo, extension, datos) in contenidos)
            {
                var guardado = $"{Guid.NewGuid():N}{extension}";
                await System.IO.File.WriteAllBytesAsync(Path.Combine(CarpetaAdjuntos(), guardado), datos);
                solicitud.Adjuntos.Add(new AdjuntoCotizacion { Archivo = guardado, NombreOriginal = Path.GetFileName(archivo.FileName), Tamano = datos.Length });
            }

            await GuardarConCodigoAsync(solicitud);

            solicitud.CorreoNotificado = await NotificarAsync(solicitud, contenidos.Select(c => new AdjuntoCorreo(Path.GetFileName(c.archivo.FileName), c.datos)).ToList());
            await _context.SaveChangesAsync();

            return StatusCode(201, new
            {
                codigo = solicitud.Codigo,
                mensaje = $"Su solicitud fue enviada correctamente. Código {solicitud.Codigo}. Nos comunicaremos con usted a la brevedad."
            });
        }

        // Asigna el siguiente correlativo del año; si otra solicitud tomó el mismo código a la vez, reintenta
        private async Task GuardarConCodigoAsync(SolicitudCotizacion solicitud)
        {
            var anio = DateTime.UtcNow.Year;
            var prefijo = $"SC-{anio}-";
            for (var intento = 1; ; intento++)
            {
                var ultimo = await _context.SolicitudesCotizacion.Where(s => s.Codigo.StartsWith(prefijo))
                    .OrderByDescending(s => s.Codigo).Select(s => s.Codigo).FirstOrDefaultAsync();
                var siguiente = ultimo == null ? 1 : int.Parse(ultimo[prefijo.Length..]) + 1;
                solicitud.Codigo = $"{prefijo}{siguiente:D4}";

                if (solicitud.Id == 0) _context.SolicitudesCotizacion.Add(solicitud);
                try
                {
                    await _context.SaveChangesAsync();
                    return;
                }
                catch (DbUpdateException) when (intento < 3)
                {
                    // Código repetido por una solicitud simultánea: se calcula de nuevo
                }
            }
        }

        // Avisa por correo a los Gerentes de Operaciones activos (o a los administradores si no hay ninguno)
        private async Task<bool> NotificarAsync(SolicitudCotizacion s, IReadOnlyList<AdjuntoCorreo> adjuntos)
        {
            var destinatarios = await _context.Usuarios
                .Where(u => u.Estado == "Activo" && u.Rol.Nombre == NombresRol.Gerente).Select(u => u.Correo).ToListAsync();
            if (destinatarios.Count == 0)
                destinatarios = await _context.Usuarios
                    .Where(u => u.Estado == "Activo" && u.Rol.Nombre == NombresRol.Administrador).Select(u => u.Correo).ToListAsync();
            if (destinatarios.Count == 0)
            {
                _logger.LogWarning("No hay Gerente de Operaciones ni administrador activo para avisar de la solicitud {Codigo}", s.Codigo);
                return false;
            }

            var todoBien = true;
            foreach (var destinatario in destinatarios)
            {
                try
                {
                    await _email.EnviarAsync(destinatario, $"Nueva solicitud de cotización {s.Codigo}", Cuerpo(s), adjuntos);
                }
                catch (Exception ex)
                {
                    todoBien = false;
                    _logger.LogError(ex, "No se pudo avisar de la solicitud {Codigo} a {Correo}", s.Codigo, destinatario);
                }
            }
            return todoBien;
        }

        private static string Cuerpo(SolicitudCotizacion s)
        {
            string E(string texto) => WebUtility.HtmlEncode(texto);
            var adjuntos = s.Adjuntos.Count == 0 ? "Sin adjuntos" : string.Join(", ", s.Adjuntos.Select(a => E(a.NombreOriginal)));
            return $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>Nueva solicitud de cotización {E(s.Codigo)}</h2>
                <table cellpadding='6' style='border-collapse: collapse;'>
                    <tr><td><b>Nombres y apellidos</b></td><td>{E(s.Nombres)}</td></tr>
                    <tr><td><b>Teléfono</b></td><td>{E(s.Telefono)}</td></tr>
                    <tr><td><b>Correo</b></td><td>{E(s.Correo)}</td></tr>
                    <tr><td><b>Servicio</b></td><td>{E(s.ServicioNombre)}</td></tr>
                    <tr><td><b>Distrito</b></td><td>{E(s.Distrito)}</td></tr>
                    <tr><td><b>Adjuntos</b></td><td>{adjuntos}</td></tr>
                </table>
                <p><b>Descripción del trabajo</b></p>
                <p style='white-space: pre-line;'>{E(s.Descripcion)}</p>
            </div>";
        }
    }
}
