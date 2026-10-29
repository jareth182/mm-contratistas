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
    // Pruebas funcionales de HU18: registro y anulación de pagos a personal (HU18-T6)
    public sealed class PagosPersonalTests : PruebaConBaseDeDatos
    {
        private class SinCorreo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => Task.CompletedTask;
        }

        private readonly ObrasController _obras;
        private readonly int _obraId;
        private readonly int _colaboradorId;

        public PagosPersonalTests()
        {
            var invitacion = new InvitacionService(new SinCorreo(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, new NotificacionHitoService(new SinCorreo(), NullLogger<NotificacionHitoService>.Instance));
            var obra = new Obra { Nombre = "Los Cedros", Cliente = "Del Río" };
            var colaborador = new Colaborador { NombreCompleto = "Juan Pérez", Especialidad = "Operario", Jornal = 90, Obra = obra };
            Db.Obras.Add(obra);
            Db.Partidas.Add(new Partida { Obra = obra, Nombre = "Mano de obra", MontoAsignado = 2000 });
            Db.Cuadrilla.Add(colaborador);
            Db.SaveChanges();
            _obraId = obra.Id;
            _colaboradorId = colaborador.Id;
        }

        private static IFormFile Archivo(string nombre = "constancia.pdf", string contenido = "%PDF-1.4 prueba") =>
            new FormFile(new MemoryStream(Encoding.ASCII.GetBytes(contenido)), 0, contenido.Length, "archivo", nombre);

        private Task<IActionResult> Pagar(decimal monto = 540, int? colaborador = null, string? desde = "11/08/2026", string? hasta = "16/08/2026",
            string? fecha = "17/08/2026", IFormFile? archivo = null, bool sinArchivo = false, int? obraId = null) =>
            _obras.RegistrarPago(obraId ?? _obraId, colaborador ?? _colaboradorId, desde, hasta, monto, fecha, sinArchivo ? null : archivo ?? Archivo());

        private async Task<decimal> Saldo()
        {
            var r = await _obras.ListarPagos(_obraId);
            return (decimal)Valor(Dato<object>(r, "partida"), "saldo");
        }

        [Fact]
        public async Task Registrar_guardaElPagoYLoImputaAManoDeObra()
        {
            Assert.Equal(200, Estado(await Pagar(540)));

            var p = await Db.Pagos.AsNoTracking().SingleAsync();
            Assert.Equal(("Juan Pérez", "11/08/2026 - 16/08/2026", "Registrado", "Mano de obra", "constancia.pdf"),
                (p.ColaboradorNombre, p.Periodo, p.Estado, p.PartidaNombre, p.ConstanciaUrl));
            Assert.True(File.Exists(ObrasController.RutaConstancia(p)));
            Assert.Equal(1460, await Saldo());
        }

        [Fact]
        public async Task ElPagoCuentaEnElPresupuestoYImpideEliminarLaPartida()
        {
            await Pagar(540);

            var presupuesto = await _obras.Presupuesto(_obraId);
            var partida = Dato<IEnumerable<object>>(presupuesto, "partidas").Single();
            Assert.Equal(540m, (decimal)Valor(partida, "gastado"));
            Assert.Equal(1, (int)Valor(partida, "gastosAsociados"));

            var id = (await Db.Partidas.SingleAsync()).Id;
            Assert.Equal(400, Estado(await _obras.EliminarPartida(id)));
            Assert.Equal(400, Estado(await _obras.EditarPartida(id, new ObrasController.PartidaDto { Nombre = "Mano de obra", MontoAsignado = 500 })));
        }

        [Fact]
        public async Task ElPagoDescuentaElSaldoQueVenGastos()
        {
            await Pagar(1900);
            var r = await _obras.RegistrarGasto(_obraId, "10/09/2026", "Factura", "F1", "Peón extra", "X", "Mano de obra", 200, Archivo());

            Assert.Equal(400, Estado(r));
        }

        [Fact]
        public async Task SinConstancia_esRechazado()
        {
            var r = await Pagar(sinArchivo: true);

            Assert.Equal(400, Estado(r));
            Assert.Contains("constancia", ((ObjectResult)r).Value!.ToString()!);
            Assert.Empty(await Db.Pagos.ToListAsync());
        }

        [Theory]
        [InlineData("virus.exe", "MZ contenido")]
        [InlineData("falso.pdf", "no es un pdf")]
        public async Task ConstanciaInvalida_esRechazada(string nombre, string contenido)
        {
            Assert.Equal(400, Estado(await Pagar(archivo: Archivo(nombre, contenido))));
            Assert.Empty(await Db.Pagos.ToListAsync());
        }

        [Theory]
        [InlineData(0, "11/08/2026", "16/08/2026", "17/08/2026")]
        [InlineData(-10, "11/08/2026", "16/08/2026", "17/08/2026")]
        [InlineData(100, null, "16/08/2026", "17/08/2026")]
        [InlineData(100, "11/08/2026", "no es fecha", "17/08/2026")]
        [InlineData(100, "16/08/2026", "11/08/2026", "17/08/2026")]
        [InlineData(100, "11/08/2026", "16/08/2026", null)]
        [InlineData(100, "11/08/2026", "16/08/2026", "2026-08-17")]
        public async Task DatosInvalidos_sonRechazados(double monto, string? desde, string? hasta, string? fecha)
        {
            Assert.Equal(400, Estado(await Pagar((decimal)monto, desde: desde, hasta: hasta, fecha: fecha)));
            Assert.Empty(await Db.Pagos.ToListAsync());
        }

        [Fact]
        public async Task SoloSePuedePagarAColaboradoresActivosDeLaCuadrillaDeLaObra()
        {
            var otra = new Obra { Nombre = "Otra", Cliente = "X" };
            var ajeno = new Colaborador { NombreCompleto = "Ajeno", Obra = otra };
            var retirado = new Colaborador { NombreCompleto = "Retirado", ObraId = _obraId, FechaSalida = new DateOnly(2026, 8, 1) };
            Db.Cuadrilla.AddRange(ajeno, retirado);
            Db.SaveChanges();

            Assert.Equal(400, Estado(await Pagar(colaborador: ajeno.Id)));
            Assert.Equal(400, Estado(await Pagar(colaborador: retirado.Id)));
            Assert.Equal(400, Estado(await Pagar(colaborador: 9999)));
            Assert.Empty(await Db.Pagos.ToListAsync());
        }

        [Fact]
        public async Task SinPartidaManoDeObra_esRechazado()
        {
            Db.Partidas.RemoveRange(Db.Partidas);
            Db.SaveChanges();

            var r = await Pagar();

            Assert.Equal(400, Estado(r));
            Assert.Contains("Mano de obra", ((ObjectResult)r).Value!.ToString()!);
        }

        [Fact]
        public async Task MontoMayorAlSaldo_esRechazado()
        {
            await Pagar(1500);

            Assert.Equal(400, Estado(await Pagar(600)));
            Assert.Equal(200, Estado(await Pagar(500)));
            Assert.Equal(0, await Saldo());
        }

        [Fact]
        public async Task Anular_excluyeElPagoDeLosTotalesYLoConserva()
        {
            await Pagar(540);
            var id = (await Db.Pagos.SingleAsync()).Id;

            var r = await _obras.AnularPago(id, new ObrasController.AnulacionDto { Motivo = "  Pago duplicado " });

            Assert.Equal(200, Estado(r));
            Assert.Equal(2000, await Saldo());
            var p = await Db.Pagos.AsNoTracking().SingleAsync();
            Assert.Equal(("Anulado", "Pago duplicado"), (p.Estado, p.MotivoAnulacion));
            Assert.NotNull(p.FechaAnulacion);
            Assert.True(File.Exists(ObrasController.RutaConstancia(p)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task Anular_sinMotivo_esRechazado(string? motivo)
        {
            await Pagar();
            var id = (await Db.Pagos.SingleAsync()).Id;

            Assert.Equal(400, Estado(await _obras.AnularPago(id, new ObrasController.AnulacionDto { Motivo = motivo })));
            Assert.Equal(400, Estado(await _obras.AnularPago(id, null)));
            Assert.Equal("Registrado", (await Db.Pagos.AsNoTracking().SingleAsync()).Estado);
        }

        [Fact]
        public async Task Anular_dosVecesOInexistente_esRechazado()
        {
            await Pagar();
            var id = (await Db.Pagos.SingleAsync()).Id;
            await _obras.AnularPago(id, new ObrasController.AnulacionDto { Motivo = "Error" });

            Assert.Equal(400, Estado(await _obras.AnularPago(id, new ObrasController.AnulacionDto { Motivo = "Otra vez" })));
            Assert.Equal(404, Estado(await _obras.AnularPago(999, new ObrasController.AnulacionDto { Motivo = "x" })));
        }

        [Fact]
        public async Task Listado_vaDelMasRecienteAlMasAntiguo_ysoloConLaCuadrillaActiva()
        {
            Db.Cuadrilla.Add(new Colaborador { NombreCompleto = "Retirado", ObraId = _obraId, FechaSalida = new DateOnly(2026, 8, 1) });
            Db.SaveChanges();
            await Pagar(10, fecha: "05/09/2026");
            await Pagar(10, fecha: "20/10/2026");
            await Pagar(10, fecha: "01/02/2026");

            var r = await _obras.ListarPagos(_obraId);
            var fechas = Dato<IEnumerable<object>>(r, "pagos").Select(p => (string)Valor(p, "fechaPago")).ToList();
            var cuadrilla = Dato<IEnumerable<object>>(r, "colaboradores").ToList();

            Assert.Equal(new[] { "20/10/2026", "05/09/2026", "01/02/2026" }, fechas);
            var colaborador = Assert.Single(cuadrilla);
            Assert.Equal(90m, (decimal)Valor(colaborador, "jornal"));
        }

        [Fact]
        public async Task Descargar_devuelveLaConstanciaConSuNombreOriginal()
        {
            await Pagar();
            var id = (await Db.Pagos.SingleAsync()).Id;

            var archivo = Assert.IsType<PhysicalFileResult>(await _obras.DescargarConstancia(id));

            Assert.Equal(("application/pdf", "constancia.pdf"), (archivo.ContentType, archivo.FileDownloadName));
        }

        [Fact]
        public async Task ObraInexistente_da404()
        {
            Assert.Equal(404, Estado(await _obras.ListarPagos(999)));
            Assert.Equal(404, Estado(await Pagar(obraId: 999)));
        }
    }
}
