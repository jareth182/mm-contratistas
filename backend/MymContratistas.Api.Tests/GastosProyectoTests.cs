using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales de HU17: registrar gasto de proyecto
    public sealed class GastosProyectoTests : PruebaConBaseDeDatos
    {
        private class SinCorreo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => Task.CompletedTask;
        }

        private readonly ObrasController _obras;
        private readonly int _obraId;

        public GastosProyectoTests()
        {
            var invitacion = new InvitacionService(new SinCorreo(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, new NotificacionHitoService(new SinCorreo(), NullLogger<NotificacionHitoService>.Instance));
            var obra = new Obra { Nombre = "Los Cedros", Cliente = "Del Río" };
            Db.Obras.Add(obra);
            Db.Partidas.Add(new Partida { Obra = obra, Nombre = "Concreto", MontoAsignado = 1000 });
            Db.SaveChanges();
            _obraId = obra.Id;
        }

        private static IFormFile Archivo(string nombre = "factura.pdf", string contenido = "%PDF-1.4 prueba") =>
            new FormFile(new MemoryStream(Encoding.ASCII.GetBytes(contenido)), 0, contenido.Length, "archivo", nombre);

        private Task<IActionResult> Registrar(decimal monto = 300, string? fecha = "10/09/2026", string? concepto = "Cemento",
            string? partida = "Concreto", IFormFile? archivo = null, bool sinArchivo = false) =>
            _obras.RegistrarGasto(_obraId, fecha, "Factura", "F001-1", concepto, "Andina", partida, monto, sinArchivo ? null : archivo ?? Archivo());

        private async Task<decimal> Saldo()
        {
            var r = await _obras.ListarGastos(_obraId);
            return Dato<IEnumerable<object>>(r, "partidas").Select(p => (decimal)Valor(p, "saldo")).Single();
        }

        [Fact]
        public async Task Registrar_guardaElGastoYDescuentaDelSaldo()
        {
            Assert.Equal(1000, await Saldo());

            Assert.Equal(200, Estado(await Registrar(300)));

            Assert.Equal(700, await Saldo());
            var g = await Db.Gastos.AsNoTracking().SingleAsync();
            Assert.Equal(("Registrado", "Concreto", "factura.pdf"), (g.Estado, g.PartidaNombre, g.ComprobanteUrl));
            Assert.True(File.Exists(ObrasController.RutaComprobante(g)));
        }

        [Fact]
        public async Task SinComprobante_esRechazado()
        {
            var r = await Registrar(sinArchivo: true);

            Assert.Equal(400, Estado(r));
            Assert.Contains("comprobante", ((ObjectResult)r).Value!.ToString()!);
            Assert.Empty(await Db.Gastos.ToListAsync());
        }

        [Theory]
        [InlineData("virus.exe", "MZ contenido")]
        [InlineData("falso.pdf", "no es un pdf")]
        public async Task ComprobanteInvalido_esRechazado(string nombre, string contenido)
        {
            Assert.Equal(400, Estado(await Registrar(archivo: Archivo(nombre, contenido))));
            Assert.Empty(await Db.Gastos.ToListAsync());
        }

        [Theory]
        [InlineData(0, "10/09/2026", "Cemento", "Concreto")]
        [InlineData(-5, "10/09/2026", "Cemento", "Concreto")]
        [InlineData(100, "2026-09-10", "Cemento", "Concreto")]
        [InlineData(100, null, "Cemento", "Concreto")]
        [InlineData(100, "10/09/2026", "  ", "Concreto")]
        [InlineData(100, "10/09/2026", "Cemento", null)]
        [InlineData(100, "10/09/2026", "Cemento", "Inexistente")]
        public async Task DatosInvalidos_sonRechazados(double monto, string? fecha, string? concepto, string? partida)
        {
            Assert.Equal(400, Estado(await Registrar((decimal)monto, fecha, concepto, partida)));
            Assert.Empty(await Db.Gastos.ToListAsync());
        }

        [Fact]
        public async Task MontoMayorAlSaldo_esRechazado()
        {
            await Registrar(800);

            Assert.Equal(400, Estado(await Registrar(300)));
            Assert.Equal(200, Estado(await Registrar(200)));
            Assert.Equal(0, await Saldo());
        }

        [Fact]
        public async Task Anular_devuelveElMontoYConservaElGasto()
        {
            await Registrar(300);
            var id = (await Db.Gastos.SingleAsync()).Id;

            var r = await _obras.AnularGasto(id, new ObrasController.AnulacionDto { Motivo = "  Factura duplicada " });

            Assert.Equal(200, Estado(r));
            Assert.Equal(1000, await Saldo());
            var g = await Db.Gastos.AsNoTracking().SingleAsync();
            Assert.Equal(("Anulado", "Factura duplicada"), (g.Estado, g.MotivoAnulacion));
            Assert.NotNull(g.FechaAnulacion);
            Assert.True(File.Exists(ObrasController.RutaComprobante(g)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task Anular_sinMotivo_esRechazado(string? motivo)
        {
            await Registrar(300);
            var id = (await Db.Gastos.SingleAsync()).Id;

            Assert.Equal(400, Estado(await _obras.AnularGasto(id, new ObrasController.AnulacionDto { Motivo = motivo })));
            Assert.Equal(400, Estado(await _obras.AnularGasto(id, null)));
            Assert.Equal("Registrado", (await Db.Gastos.AsNoTracking().SingleAsync()).Estado);
        }

        [Fact]
        public async Task Anular_dosVeces_esRechazado()
        {
            await Registrar(300);
            var id = (await Db.Gastos.SingleAsync()).Id;
            await _obras.AnularGasto(id, new ObrasController.AnulacionDto { Motivo = "Error" });

            Assert.Equal(400, Estado(await _obras.AnularGasto(id, new ObrasController.AnulacionDto { Motivo = "Otra vez" })));
        }

        [Fact]
        public async Task Anular_gastoInexistente_da404()
        {
            Assert.Equal(404, Estado(await _obras.AnularGasto(999, new ObrasController.AnulacionDto { Motivo = "x" })));
        }

        [Fact]
        public async Task Listado_vaDelMasRecienteAlMasAntiguo()
        {
            await Registrar(10, "05/09/2026", "Medio");
            await Registrar(10, "20/10/2026", "Reciente");
            await Registrar(10, "01/02/2026", "Antiguo");
            await Registrar(10, "05/09/2026", "Medio posterior");

            var r = await _obras.ListarGastos(_obraId);
            var conceptos = Dato<IEnumerable<object>>(r, "gastos").Select(g => (string)Valor(g, "concepto")).ToList();

            Assert.Equal(new[] { "Reciente", "Medio posterior", "Medio", "Antiguo" }, conceptos);
        }

        [Fact]
        public async Task Listado_incluyeLosAnuladosYSoloLosDeLaObra()
        {
            var otra = new Obra { Nombre = "Otra", Cliente = "X" };
            Db.Obras.Add(otra);
            Db.Gastos.Add(new Gasto { Obra = otra, Concepto = "Ajeno", Fecha = "01/01/2026", Monto = 1 });
            Db.SaveChanges();
            await Registrar(10);
            await _obras.AnularGasto((await Db.Gastos.FirstAsync(g => g.ObraId == _obraId)).Id, new ObrasController.AnulacionDto { Motivo = "Error" });

            var gastos = Dato<IEnumerable<object>>(await _obras.ListarGastos(_obraId), "gastos").ToList();

            Assert.Single(gastos);
            Assert.Equal("Anulado", (string)Valor(gastos[0], "estado"));
        }

        [Fact]
        public async Task ObraInexistente_da404()
        {
            Assert.Equal(404, Estado(await _obras.ListarGastos(999)));
            Assert.Equal(404, Estado(await _obras.RegistrarGasto(999, "10/09/2026", "", "", "x", "", "Concreto", 1, Archivo())));
        }

        [Fact]
        public async Task Descargar_devuelveElComprobanteConSuNombreOriginal()
        {
            await Registrar(300);
            var id = (await Db.Gastos.SingleAsync()).Id;

            var r = await _obras.DescargarComprobante(id);

            var archivo = Assert.IsType<PhysicalFileResult>(r);
            Assert.Equal(("application/pdf", "factura.pdf"), (archivo.ContentType, archivo.FileDownloadName));
        }
    }
}
