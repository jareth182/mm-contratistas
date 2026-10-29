using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales de HU19: presupuestado vs. gastado (HU19-T7)
    public sealed class ControlEconomicoTests : PruebaConBaseDeDatos
    {
        private class SinCorreo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => Task.CompletedTask;
        }

        private readonly ObrasController _obras;
        private readonly Obra _obra;

        public ControlEconomicoTests()
        {
            var invitacion = new InvitacionService(new SinCorreo(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, new NotificacionHitoService(new SinCorreo(), NullLogger<NotificacionHitoService>.Instance));
            _obra = new Obra { Nombre = "Los Cedros", Cliente = "Del Río" };
            Db.Obras.Add(_obra);
            Db.SaveChanges();
        }

        private Partida Partida(string nombre, decimal monto)
        {
            var p = new Partida { ObraId = _obra.Id, Nombre = nombre, MontoAsignado = monto };
            Db.Partidas.Add(p);
            Db.SaveChanges();
            return p;
        }

        private void Gasto(string partida, decimal monto, string estado = "Registrado", string fecha = "10/09/2026")
        {
            Db.Gastos.Add(new Gasto { ObraId = _obra.Id, PartidaNombre = partida, Monto = monto, Estado = estado, Fecha = fecha, Concepto = "Material", Proveedor = "Andina" });
            Db.SaveChanges();
        }

        private void Pago(decimal monto, string estado = "Registrado", string fecha = "12/09/2026", string partida = "Mano de obra")
        {
            Db.Pagos.Add(new Pago { ObraId = _obra.Id, PartidaNombre = partida, Monto = monto, Estado = estado, FechaPago = fecha, ColaboradorNombre = "Juan", Periodo = "01/09/2026 - 06/09/2026" });
            Db.SaveChanges();
        }

        private async Task<(List<object> partidas, object totales)> Consultar()
        {
            var r = await _obras.ControlEconomico(_obra.Id);
            return (Dato<IEnumerable<object>>(r, "partidas").ToList(), Dato<object>(r, "totales"));
        }

        [Fact]
        public async Task CalculaGastadoSaldoYPorcentajePorPartida()
        {
            Partida("Concreto", 1000);
            Gasto("Concreto", 250);
            Gasto("Concreto", 100);

            var (partidas, _) = await Consultar();

            var p = Assert.Single(partidas);
            Assert.Equal((1000m, 350m, 650m, 35m, "Normal"),
                ((decimal)Valor(p, "presupuestado"), (decimal)Valor(p, "gastado"), (decimal)Valor(p, "saldo"), (decimal)Valor(p, "ejecutado"), (string)Valor(p, "estado")));
        }

        [Fact]
        public async Task LosPagosAlPersonalSumanEnLaPartidaManoDeObra()
        {
            Partida("Mano de obra", 2000);
            Gasto("Mano de obra", 300);
            Pago(500);

            var (partidas, _) = await Consultar();

            Assert.Equal(800m, (decimal)Valor(Assert.Single(partidas), "gastado"));
        }

        [Fact]
        public async Task LosAnuladosNoCuentanEnLosTotales()
        {
            Partida("Mano de obra", 2000);
            Gasto("Mano de obra", 300);
            Gasto("Mano de obra", 999, "Anulado");
            Pago(500);
            Pago(888, "Anulado");

            var (partidas, totales) = await Consultar();

            Assert.Equal(800m, (decimal)Valor(Assert.Single(partidas), "gastado"));
            Assert.Equal(800m, (decimal)Valor(totales, "gastoAcumulado"));
        }

        [Fact]
        public async Task FilaDeTotales_sumaPresupuestoGastoYSaldoGeneral()
        {
            Partida("Concreto", 1000);
            Partida("Acabados", 3000);
            Gasto("Concreto", 400);
            Gasto("Acabados", 600);

            var (_, totales) = await Consultar();

            Assert.Equal((4000m, 1000m, 3000m, 25m),
                ((decimal)Valor(totales, "presupuestoTotal"), (decimal)Valor(totales, "gastoAcumulado"), (decimal)Valor(totales, "saldoGeneral"), (decimal)Valor(totales, "ejecutado")));
        }

        [Theory]
        [InlineData(0, "Normal")]
        [InlineData(899.99, "Normal")]
        [InlineData(900, "Alerta")]
        [InlineData(1000, "Alerta")]
        [InlineData(1000.01, "Excedida")]
        [InlineData(1500, "Excedida")]
        public async Task AlertasVisuales_segunElPorcentajeEjecutado(double gastado, string esperado)
        {
            Partida("Concreto", 1000);
            if (gastado > 0) Gasto("Concreto", (decimal)gastado);

            var (partidas, _) = await Consultar();

            Assert.Equal(esperado, (string)Valor(Assert.Single(partidas), "estado"));
        }

        [Fact]
        public async Task PartidaExcedida_tieneSaldoNegativo()
        {
            Partida("Concreto", 1000);
            Gasto("Concreto", 1200);

            var (partidas, totales) = await Consultar();

            var p = Assert.Single(partidas);
            Assert.Equal((-200m, 120m), ((decimal)Valor(p, "saldo"), (decimal)Valor(p, "ejecutado")));
            Assert.Equal(-200m, (decimal)Valor(totales, "saldoGeneral"));
        }

        [Fact]
        public async Task SinPartidas_devuelveTotalesEnCero()
        {
            var (partidas, totales) = await Consultar();

            Assert.Empty(partidas);
            Assert.Equal((0m, 0m, 0m, 0m),
                ((decimal)Valor(totales, "presupuestoTotal"), (decimal)Valor(totales, "gastoAcumulado"), (decimal)Valor(totales, "saldoGeneral"), (decimal)Valor(totales, "ejecutado")));
        }

        [Fact]
        public async Task SoloCuentanLosMovimientosDeLaObra()
        {
            Partida("Concreto", 1000);
            var otra = new Obra { Nombre = "Otra", Cliente = "X" };
            Db.Obras.Add(otra);
            Db.Gastos.Add(new Gasto { Obra = otra, PartidaNombre = "Concreto", Monto = 700, Fecha = "01/01/2026" });
            Db.SaveChanges();

            var (partidas, _) = await Consultar();

            Assert.Equal(0m, (decimal)Valor(Assert.Single(partidas), "gastado"));
        }

        [Fact]
        public async Task Detalle_listaGastosYPagosDelMasRecienteAlMasAntiguo_conLosAnuladosMarcados()
        {
            var p = Partida("Mano de obra", 2000);
            Gasto("Mano de obra", 100, fecha: "05/09/2026");
            Pago(500, fecha: "20/09/2026");
            Gasto("Mano de obra", 70, "Anulado", fecha: "10/09/2026");
            Gasto("Concreto", 9999);

            var r = await _obras.DetallePartida(p.Id);
            var movimientos = Dato<IEnumerable<object>>(r, "movimientos").ToList();
            var resumen = Dato<object>(r, "partida");

            Assert.Equal(new[] { "Pago", "Gasto", "Gasto" }, movimientos.Select(m => (string)Valor(m, "tipo")));
            Assert.Equal(new[] { "Registrado", "Anulado", "Registrado" }, movimientos.Select(m => (string)Valor(m, "estado")));
            Assert.Equal(600m, (decimal)Valor(resumen, "gastado"));
        }

        [Fact]
        public async Task Detalle_conPartidaOObraInexistente_da404()
        {
            Assert.Equal(404, Estado(await _obras.DetallePartida(999)));
            Assert.Equal(404, Estado(await _obras.ControlEconomico(999)));
        }
    }
}
