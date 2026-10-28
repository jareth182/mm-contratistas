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
    // Pruebas funcionales de HU16: presupuesto por partidas (HU16-T6)
    public sealed class PresupuestoPartidasTests : PruebaConBaseDeDatos
    {
        private class SinCorreo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => Task.CompletedTask;
        }

        private readonly ObrasController _obras;
        private readonly int _obraId;

        public PresupuestoPartidasTests()
        {
            var invitacion = new InvitacionService(new SinCorreo(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, new NotificacionHitoService(new SinCorreo(), NullLogger<NotificacionHitoService>.Instance));
            var obra = new Obra { Nombre = "Los Cedros", Cliente = "Del Río" };
            Db.Obras.Add(obra);
            Db.SaveChanges();
            _obraId = obra.Id;
        }

        private static ObrasController.PartidaDto Dto(string? nombre = "Movimiento de tierras", decimal monto = 48000, string? descripcion = "Excavación y nivelación") =>
            new() { Nombre = nombre, MontoAsignado = monto, Descripcion = descripcion };

        private async Task<(int estado, int id)> Agregar(ObrasController.PartidaDto dto, int? obraId = null)
        {
            var r = await _obras.AgregarPartida(obraId ?? _obraId, dto);
            return (Estado(r), Estado(r) == 200 ? Dato<int>(r, "id") : 0);
        }

        private void Gasto(string partida, decimal monto, string estado = "Registrado", int? obraId = null)
        {
            Db.Gastos.Add(new Gasto { ObraId = obraId ?? _obraId, PartidaNombre = partida, Monto = monto, Estado = estado, Concepto = "Material" });
            Db.SaveChanges();
        }

        private async Task<List<Partida>> Guardadas()
        {
            await using var db = OtroContexto();
            return await db.Partidas.Where(p => p.ObraId == _obraId).OrderBy(p => p.Id).ToListAsync();
        }

        [Fact]
        public async Task Agregar_guardaLaPartida_sinEspaciosSobrantes()
        {
            var (estado, _) = await Agregar(Dto("  Movimiento de tierras ", 48000.5m, " Excavación "));

            Assert.Equal(200, estado);
            var p = (await Guardadas()).Single();
            Assert.Equal(("Movimiento de tierras", "Excavación", 48000.5m), (p.Nombre, p.Descripcion, p.MontoAsignado));
        }

        [Fact]
        public async Task LaDescripcionEsOpcional()
        {
            Assert.Equal(200, (await Agregar(Dto(descripcion: null))).estado);
            Assert.Equal("", (await Guardadas()).Single().Descripcion);
        }

        [Theory]
        [InlineData(null, 100)]
        [InlineData("", 100)]
        [InlineData("   ", 100)]
        [InlineData("Partida", 0)]
        [InlineData("Partida", -50)]
        public async Task DatosInvalidos_sonRechazados(string? nombre, double monto)
        {
            Assert.Equal(400, (await Agregar(Dto(nombre, (decimal)monto))).estado);
            Assert.Empty(await Guardadas());
        }

        [Fact]
        public async Task LimitesDeTamano()
        {
            Assert.Equal(400, (await Agregar(Dto(new string('a', 101)))).estado);
            Assert.Equal(400, (await Agregar(Dto(descripcion: new string('d', 301)))).estado);
            Assert.Equal(400, (await Agregar(Dto(monto: ObrasController.MontoMaximoPartida + 1))).estado);
            Assert.Equal(200, (await Agregar(Dto(monto: ObrasController.MontoMaximoPartida))).estado);
        }

        [Fact]
        public async Task NombreRepetidoEnLaObra_esRechazado_perosePermiteEnOtraObra()
        {
            await Agregar(Dto("Acabados"));
            var otra = new Obra { Nombre = "Otra", Cliente = "X" };
            Db.Obras.Add(otra);
            await Db.SaveChangesAsync();

            Assert.Equal(400, (await Agregar(Dto("  ACABADOS "))).estado);
            Assert.Equal(200, (await Agregar(Dto("Acabados"), otra.Id)).estado);
            Assert.Equal(404, (await Agregar(Dto(), 999)).estado);
        }

        [Fact]
        public async Task Presupuesto_sumaElMontoTotal_yLoRecalculaAlCambiarPartidas()
        {
            var (_, tierras) = await Agregar(Dto("Movimiento de tierras", 48000));
            await Agregar(Dto("Concreto y acero", 186000.25m));
            Assert.Equal(234000.25m, Dato<decimal>(await _obras.Presupuesto(_obraId), "montoTotal"));

            await _obras.EditarPartida(tierras, Dto("Movimiento de tierras", 50000));
            Assert.Equal(236000.25m, Dato<decimal>(await _obras.Presupuesto(_obraId), "montoTotal"));

            await _obras.EliminarPartida(tierras);
            Assert.Equal(186000.25m, Dato<decimal>(await _obras.Presupuesto(_obraId), "montoTotal"));
            Assert.IsType<NotFoundObjectResult>(await _obras.Presupuesto(999));
        }

        [Fact]
        public async Task Presupuesto_informaLosGastosAsociados_sinContarLosAnuladosEnLoGastado()
        {
            await Agregar(Dto("Movimiento de tierras", 48000));
            Gasto("Movimiento de tierras", 5000);
            Gasto("Movimiento de tierras", 1200, "Anulado");
            await Agregar(Dto("Acabados", 94000));

            var partidas = ((System.Collections.IEnumerable)Valor(((ObjectResult)await _obras.Presupuesto(_obraId)).Value!, "partidas")).Cast<object>().ToList();

            Assert.Equal((2, 5000m), ((int)Valor(partidas[0], "gastosAsociados"), (decimal)Valor(partidas[0], "gastado")));
            Assert.Equal((0, 0m), ((int)Valor(partidas[1], "gastosAsociados"), (decimal)Valor(partidas[1], "gastado")));
        }

        [Fact]
        public async Task Editar_actualizaLosDatos()
        {
            var (_, id) = await Agregar(Dto());

            var r = await _obras.EditarPartida(id, Dto("Excavación masiva", 52000, "Con retroexcavadora"));

            Assert.Equal(200, Estado(r));
            var p = (await Guardadas()).Single();
            Assert.Equal(("Excavación masiva", "Con retroexcavadora", 52000m), (p.Nombre, p.Descripcion, p.MontoAsignado));
        }

        [Fact]
        public async Task Editar_validaIgualQueAlAgregar_yPermiteConservarSuPropioNombre()
        {
            var (_, a) = await Agregar(Dto("Acabados", 1000));
            await Agregar(Dto("Instalaciones", 2000));

            Assert.Equal(400, Estado(await _obras.EditarPartida(a, Dto("instalaciones", 1000))));
            Assert.Equal(400, Estado(await _obras.EditarPartida(a, Dto("Acabados", 0))));
            Assert.Equal(400, Estado(await _obras.EditarPartida(a, Dto(" ", 1000))));
            Assert.Equal(200, Estado(await _obras.EditarPartida(a, Dto("Acabados", 1500))));
            Assert.IsType<NotFoundObjectResult>(await _obras.EditarPartida(999, Dto()));
        }

        [Fact]
        public async Task CambiarElNombre_arrastraLosGastosDeLaPartida()
        {
            var (_, id) = await Agregar(Dto("Movimiento de tierras", 48000));
            Gasto("Movimiento de tierras", 5000);
            Gasto("Otra partida", 300);

            await _obras.EditarPartida(id, Dto("Excavación", 48000));

            await using var db = OtroContexto();
            Assert.Equal(new[] { "Excavación", "Otra partida" }, db.Gastos.OrderBy(g => g.Id).Select(g => g.PartidaNombre));
        }

        [Fact]
        public async Task ElMontoNoPuedeQuedarPorDebajoDeLoYaGastado_peroLosAnuladosNoCuentan()
        {
            var (_, id) = await Agregar(Dto("Movimiento de tierras", 48000));
            Gasto("Movimiento de tierras", 30000);
            Gasto("Movimiento de tierras", 15000, "Anulado");

            Assert.Equal(400, Estado(await _obras.EditarPartida(id, Dto(monto: 29999))));
            Assert.Equal(200, Estado(await _obras.EditarPartida(id, Dto(monto: 30000))));
        }

        [Fact]
        public async Task Eliminar_sinGastos_funciona()
        {
            var (_, id) = await Agregar(Dto());

            Assert.IsType<OkResult>(await _obras.EliminarPartida(id));

            Assert.Empty(await Guardadas());
            Assert.IsType<NotFoundObjectResult>(await _obras.EliminarPartida(id));
        }

        [Theory]
        [InlineData("Registrado")]
        [InlineData("Anulado")]
        public async Task Eliminar_conGastosAsociados_esRechazado(string estadoDelGasto)
        {
            var (_, id) = await Agregar(Dto("Movimiento de tierras"));
            Gasto("Movimiento de tierras", 1200, estadoDelGasto);

            var r = await _obras.EliminarPartida(id);

            var mal = Assert.IsType<BadRequestObjectResult>(r);
            Assert.Contains("gasto", (string)mal.Value!);
            Assert.Single(await Guardadas());
        }

        [Fact]
        public async Task LosGastosDeOtraObraConElMismoNombre_noImpidenEliminar()
        {
            var otra = new Obra { Nombre = "Otra", Cliente = "X" };
            Db.Obras.Add(otra);
            await Db.SaveChangesAsync();
            var (_, id) = await Agregar(Dto("Acabados"));
            Gasto("Acabados", 500, obraId: otra.Id);

            Assert.IsType<OkResult>(await _obras.EliminarPartida(id));
        }
    }
}
