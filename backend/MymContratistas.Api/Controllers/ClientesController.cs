using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Controllers
{
    // Clientes a los que se les asigna un proyecto (HU11)
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = NombresRol.Personal)]
    public class ClientesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class ClienteDto
        {
            public string? Nombre { get; set; }
            public string? TipoDocumento { get; set; }
            public string? Documento { get; set; }
            public string? Correo { get; set; }
            public string? Telefono { get; set; }
        }

        private static object Proyectar(Cliente c) => new
        {
            id = c.Id,
            nombre = c.Nombre,
            tipoDocumento = c.TipoDocumento,
            documento = c.Documento,
            correo = c.Correo,
            telefono = c.Telefono
        };

        private static bool CorreoValido(string correo)
        {
            try { return new MailAddress(correo).Address == correo && correo.Contains('.'); }
            catch (FormatException) { return false; }
        }

        // GET: api/clientes?q=texto  — busca por nombre o por número de documento
        [HttpGet]
        public async Task<IActionResult> Buscar([FromQuery] string? q)
        {
            var consulta = _context.Clientes.AsQueryable();
            var texto = (q ?? string.Empty).Trim();
            if (texto.Length > 0)
            {
                var minuscula = texto.ToLowerInvariant();
                consulta = consulta.Where(c => c.Nombre.ToLower().Contains(minuscula) || c.Documento.StartsWith(texto));
            }

            var clientes = await consulta.OrderBy(c => c.Nombre).Take(20).ToListAsync();
            return Ok(clientes.Select(Proyectar));
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] ClienteDto dto)
        {
            var nombre = (dto.Nombre ?? string.Empty).Trim();
            var tipo = (dto.TipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
            var documento = (dto.Documento ?? string.Empty).Trim();
            var correo = (dto.Correo ?? string.Empty).Trim().ToLowerInvariant();
            var telefono = (dto.Telefono ?? string.Empty).Trim();

            if (nombre.Length == 0) return BadRequest(new { mensaje = "El nombre o razón social es obligatorio." });
            if (tipo != "DNI" && tipo != "RUC") return BadRequest(new { mensaje = "Seleccione el tipo de documento (DNI o RUC)." });
            if (!Regex.IsMatch(documento, tipo == "DNI" ? @"^\d{8}$" : @"^\d{11}$"))
                return BadRequest(new { mensaje = tipo == "DNI" ? "El DNI debe tener 8 dígitos." : "El RUC debe tener 11 dígitos." });
            if (correo.Length > 0 && !CorreoValido(correo)) return BadRequest(new { mensaje = "Ingrese un correo electrónico válido." });

            if (await _context.Clientes.AnyAsync(c => c.Documento == documento))
                return Conflict(new { mensaje = $"Ya existe un cliente con el documento {documento}." });

            var cliente = new Cliente
            {
                Nombre = nombre,
                TipoDocumento = tipo,
                Documento = documento,
                Correo = correo.Length > 0 ? correo : null,
                Telefono = telefono.Length > 0 ? telefono : null
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return StatusCode(201, new { mensaje = "Cliente registrado correctamente.", cliente = Proyectar(cliente) });
        }
    }
}
