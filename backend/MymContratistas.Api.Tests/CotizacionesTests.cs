using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales de la solicitud de cotización en línea (HU07-T6)
    public sealed class CotizacionesTests : PruebaConBaseDeDatos
    {
        private class CorreoDePrueba : IEmailService
        {
            public List<(string Para, string Asunto, string Cuerpo, IReadOnlyList<AdjuntoCorreo> Adjuntos)> Enviados { get; } = new();
            public bool Falla { get; set; }

            public Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, IReadOnlyList<AdjuntoCorreo>? adjuntos = null)
            {
                if (Falla) throw new InvalidOperationException("SMTP caído");
                Enviados.Add((destinatario, asunto, cuerpoHtml, adjuntos ?? Array.Empty<AdjuntoCorreo>()));
                return Task.CompletedTask;
            }
        }

        private readonly CorreoDePrueba _correo = new();
        private readonly CotizacionesController _cotizaciones;
        private readonly int _servicioId;

        public CotizacionesTests()
        {
            _cotizaciones = new CotizacionesController(Db, _correo, NullLogger<CotizacionesController>.Instance);

            var construccion = new Servicio { Nombre = "Construcción de edificios", Descripcion = "x", Orden = 1 };
            Db.Servicios.AddRange(construccion, new Servicio { Nombre = "Interno", Descripcion = "x", Orden = 2, Estado = Servicio.Oculto });
            Db.Usuarios.AddRange(
                new Usuario { Nombres = "Marco", Correo = "gerente@mym.pe", RolId = 2, PasswordHash = "x" },
                new Usuario { Nombres = "Inactivo", Correo = "ex.gerente@mym.pe", RolId = 2, PasswordHash = "x", Estado = "Inactivo" },
                new Usuario { Nombres = "Maribel", Correo = "admin@mym.pe", RolId = 1, PasswordHash = "x" });
            Db.SaveChanges();
            _servicioId = construccion.Id;
        }

        private CotizacionesController.SolicitudDto Valida() => new()
        {
            Nombres = "Ana Torres Miranda", Telefono = "987 654 321", Correo = "Ana.Torres@Correo.com",
            ServicioId = _servicioId, Distrito = "San Isidro", Descripcion = "Ampliación de un tercer piso, 90 m²."
        };

        private static List<IFormFile> Adjuntos(params (string nombre, byte[] contenido)[] archivos) =>
            archivos.Select(a => Archivos.Formulario(a.nombre, a.contenido)).ToList();

        private string CarpetaAdjuntos => Path.Combine(Carpeta, "Uploads", "cotizaciones");

        [Fact]
        public async Task Registrar_guardaLaSolicitudYDevuelveElCodigo()
        {
            var r = await _cotizaciones.Registrar(Valida(), null);

            Assert.Equal(201, Estado(r));
            var codigo = Dato<string>(r, "codigo");
            Assert.Equal($"SC-{DateTime.UtcNow.Year}-0001", codigo);
            Assert.Contains(codigo, Dato<string>(r, "mensaje"));

            var s = await Db.SolicitudesCotizacion.SingleAsync();
            Assert.Equal("ana.torres@correo.com", s.Correo);
            Assert.Equal("Construcción de edificios", s.ServicioNombre);
            Assert.True(s.CorreoNotificado);
        }

        [Fact]
        public async Task Codigos_sonCorrelativos()
        {
            await _cotizaciones.Registrar(Valida(), null);
            await _cotizaciones.Registrar(Valida(), null);
            var r = await _cotizaciones.Registrar(Valida(), null);

            Assert.Equal($"SC-{DateTime.UtcNow.Year}-0003", Dato<string>(r, "codigo"));
        }

        [Fact]
        public async Task CamposFaltantes_seIndicanTodosYNoSeGuardaNada()
        {
            var r = await _cotizaciones.Registrar(new() { Nombres = "Ana" }, null);

            Assert.Equal(400, Estado(r));
            var mensaje = Dato<string>(r, "mensaje");
            foreach (var campo in new[] { "teléfono", "correo electrónico", "tipo de servicio", "distrito", "descripción del trabajo" })
                Assert.Contains(campo, mensaje);
            Assert.DoesNotContain("nombres", mensaje);
            Assert.Empty(Db.SolicitudesCotizacion);
            Assert.Empty(_correo.Enviados);
        }

        [Theory]
        [InlineData("telefono", "abc")]
        [InlineData("correo", "no-es-correo")]
        [InlineData("servicio", "oculto")]
        [InlineData("servicio", "inexistente")]
        public async Task DatosConFormatoInvalido_sonRechazados(string campo, string valor)
        {
            var dto = Valida();
            if (campo == "telefono") dto.Telefono = valor;
            if (campo == "correo") dto.Correo = valor;
            if (valor == "oculto") dto.ServicioId = (await Db.Servicios.SingleAsync(s => s.Estado == Servicio.Oculto)).Id;
            if (valor == "inexistente") dto.ServicioId = 999;

            Assert.Equal(400, Estado(await _cotizaciones.Registrar(dto, null)));
            Assert.Empty(Db.SolicitudesCotizacion);
        }

        [Fact]
        public async Task Adjuntos_PdfJpgPng_seGuardanYSeEnvianAlGerente()
        {
            var r = await _cotizaciones.Registrar(Valida(), Adjuntos(("plano.pdf", Archivos.Pdf()), ("fachada.jpg", Archivos.Jpg()), ("vista.png", Archivos.Png())));

            Assert.Equal(201, Estado(r));
            var adjuntos = await Db.AdjuntosCotizacion.OrderBy(a => a.Id).ToListAsync();
            Assert.Equal(new[] { "plano.pdf", "fachada.jpg", "vista.png" }, adjuntos.Select(a => a.NombreOriginal));
            Assert.All(adjuntos, a => Assert.True(File.Exists(Path.Combine(CarpetaAdjuntos, a.Archivo))));
            Assert.Equal(new[] { "plano.pdf", "fachada.jpg", "vista.png" }, _correo.Enviados.Single().Adjuntos.Select(a => a.Nombre));
        }

        [Theory]
        [InlineData("archivo.docx")]      // formato no permitido
        [InlineData("falso.pdf")]         // extensión correcta, contenido distinto
        [InlineData("grande.pdf")]        // más de 10 MB
        public async Task AdjuntoInvalido_rechazaLaSolicitudCompleta(string nombre)
        {
            var contenido = nombre switch
            {
                "falso.pdf" => Archivos.Png(),
                "grande.pdf" => Archivos.Pdf().Concat(new byte[10 * 1024 * 1024]).ToArray(),
                _ => Archivos.Pdf()
            };

            var r = await _cotizaciones.Registrar(Valida(), Adjuntos(("ok.pdf", Archivos.Pdf()), (nombre, contenido)));

            Assert.Equal(400, Estado(r));
            Assert.Empty(Db.SolicitudesCotizacion);
            Assert.False(Directory.Exists(CarpetaAdjuntos) && Directory.EnumerateFiles(CarpetaAdjuntos).Any());
        }

        [Fact]
        public async Task MasDeCincoAdjuntos_esRechazado()
        {
            var archivos = Enumerable.Range(1, 6).Select(n => ($"p{n}.pdf", Archivos.Pdf())).ToArray();

            Assert.Equal(400, Estado(await _cotizaciones.Registrar(Valida(), Adjuntos(archivos))));
        }

        [Fact]
        public async Task Correo_vaSoloALosGerentesActivos_conLosDatosDeLaSolicitud()
        {
            await _cotizaciones.Registrar(Valida(), null);

            var correo = Assert.Single(_correo.Enviados);
            Assert.Equal("gerente@mym.pe", correo.Para);
            Assert.Contains("SC-", correo.Asunto);
            foreach (var dato in new[] { "Ana Torres Miranda", "987 654 321", "ana.torres@correo.com", "Construcción de edificios", "San Isidro", "tercer piso" })
                Assert.Contains(System.Net.WebUtility.HtmlEncode(dato), correo.Cuerpo);
        }

        [Fact]
        public async Task SinGerenteActivo_seAvisaALosAdministradores()
        {
            await Db.Usuarios.Where(u => u.RolId == 2).ExecuteUpdateAsync(u => u.SetProperty(x => x.Estado, "Inactivo"));

            await _cotizaciones.Registrar(Valida(), null);

            Assert.Equal("admin@mym.pe", Assert.Single(_correo.Enviados).Para);
        }

        [Fact]
        public async Task SiFallaElCorreo_laSolicitudSeConservaYElVisitanteRecibeSuCodigo()
        {
            _correo.Falla = true;

            var r = await _cotizaciones.Registrar(Valida(), null);

            Assert.Equal(201, Estado(r));
            await using var leer = OtroContexto();
            Assert.False((await leer.SolicitudesCotizacion.SingleAsync()).CorreoNotificado);
        }

        [Fact]
        public async Task TextoDelVisitante_seEscapaEnElCorreo()
        {
            var dto = Valida(); dto.Descripcion = "<script>alert(1)</script>";

            await _cotizaciones.Registrar(dto, null);

            Assert.DoesNotContain("<script>", _correo.Enviados.Single().Cuerpo);
        }
    }
}
