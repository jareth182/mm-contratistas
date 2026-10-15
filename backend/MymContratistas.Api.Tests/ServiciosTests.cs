using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales del mantenimiento del catálogo de servicios (HU09-T6)
    public sealed class ServiciosTests : PruebaConBaseDeDatos
    {
        private readonly ServiciosController _admin;
        private readonly SitioController _sitio;

        public ServiciosTests()
        {
            _admin = new ServiciosController(Db);
            _sitio = new SitioController(Db);
        }

        private static ServiciosController.ServicioDto Dto(string nombre, int? orden = null, string estado = Servicio.Publicado) => new()
        {
            Nombre = nombre, Descripcion = $"Descripción de {nombre}", Beneficios = " Rápido \n\n Seguro ", Caracteristicas = "A\nB", Orden = orden, Estado = estado
        };

        private async Task<int> Crear(string nombre, int? orden = null, string estado = Servicio.Publicado) =>
            (int)Valor(Dato<object>(await _admin.Crear(Dto(nombre, orden, estado)), "servicio"), "id");

        private async Task<string[]> OrdenEnBase()
        {
            await using var leer = OtroContexto();
            return await leer.Servicios.OrderBy(s => s.Orden).Select(s => s.Nombre).ToArrayAsync();
        }

        private async Task<string[]> CatalogoPublico() =>
            Lista(await _sitio.Servicios()).Select(s => (string)Valor(s, "nombre")).ToArray();

        [Fact]
        public async Task Crear_guardaYLimpiaLasListas()
        {
            var r = await _admin.Crear(Dto("Construcción"));

            Assert.Equal(201, Estado(r));
            var s = await Db.Servicios.SingleAsync();
            Assert.Equal("Rápido\nSeguro", s.Beneficios);
            Assert.Equal(1, s.Orden);
        }

        [Fact]
        public async Task Crear_conDatosInvalidosONombreRepetido_esRechazado()
        {
            await Crear("Construcción");

            Assert.Equal(400, Estado(await _admin.Crear(new() { Nombre = " ", Descripcion = "x" })));
            Assert.Equal(400, Estado(await _admin.Crear(new() { Nombre = "Otro", Descripcion = " " })));
            Assert.Equal(400, Estado(await _admin.Crear(Dto("Otro", orden: 0))));
            Assert.Equal(400, Estado(await _admin.Crear(Dto("Otro", estado: "Borrado"))));
            Assert.Equal(400, Estado(await _admin.Crear(Dto("CONSTRUCCIÓN"))));
            Assert.Equal(1, await Db.Servicios.CountAsync());
        }

        [Fact]
        public async Task Crear_enUnaPosicion_reacomodaLosDemas()
        {
            await Crear("A");
            await Crear("B");
            await Crear("C", orden: 1);

            Assert.Equal(new[] { "C", "A", "B" }, await OrdenEnBase());
        }

        [Fact]
        public async Task Editar_cambiarOrden_mueveElServicioYMantieneCorrelativo()
        {
            var a = await Crear("A");
            await Crear("B");
            await Crear("C");

            Assert.Equal(200, Estado(await _admin.Editar(a, Dto("A", orden: 3))));
            Assert.Equal(new[] { "B", "C", "A" }, await OrdenEnBase());

            await using var leer = OtroContexto();
            Assert.Equal(new[] { 1, 2, 3 }, await leer.Servicios.OrderBy(s => s.Orden).Select(s => s.Orden).ToArrayAsync());
        }

        [Fact]
        public async Task Editar_inexistenteOConNombreDeOtro_esRechazado()
        {
            await Crear("A");
            var b = await Crear("B");

            Assert.Equal(404, Estado(await _admin.Editar(999, Dto("X"))));
            Assert.Equal(400, Estado(await _admin.Editar(b, Dto("a"))));
        }

        [Fact]
        public async Task Eliminar_quitaElServicioYCierraElHuecoDelOrden()
        {
            await Crear("A");
            var b = await Crear("B");
            await Crear("C");

            Assert.Equal(200, Estado(await _admin.Eliminar(b)));

            Assert.Equal(new[] { "A", "C" }, await OrdenEnBase());
            await using var leer = OtroContexto();
            Assert.Equal(new[] { 1, 2 }, await leer.Servicios.OrderBy(s => s.Orden).Select(s => s.Orden).ToArrayAsync());
            Assert.Equal(404, Estado(await _admin.Eliminar(b)));
        }

        [Fact]
        public async Task CatalogoPublico_reflejaAltasCambiosOcultosYBajas()
        {
            var a = await Crear("A");
            var b = await Crear("B");
            await Crear("Oculto", estado: Servicio.Oculto);
            Assert.Equal(new[] { "A", "B" }, await CatalogoPublico());

            await _admin.Editar(b, Dto("B renombrado", orden: 1));
            Assert.Equal(new[] { "B renombrado", "A" }, await CatalogoPublico());

            await _admin.Editar(a, Dto("A", estado: Servicio.Oculto));
            Assert.Equal(new[] { "B renombrado" }, await CatalogoPublico());

            await _admin.Eliminar(b);
            Assert.Empty(await CatalogoPublico());
        }

        [Fact]
        public void Sembrado_creaElCatalogoInicialUnaSolaVez()
        {
            DbSeeder.SembrarServicios(Db);
            DbSeeder.SembrarServicios(Db);

            Assert.Equal(4, Db.Servicios.Count());
            Assert.Equal(new[] { 1, 2, 3, 4 }, Db.Servicios.OrderBy(s => s.Orden).Select(s => s.Orden).ToArray());
        }
    }
}
