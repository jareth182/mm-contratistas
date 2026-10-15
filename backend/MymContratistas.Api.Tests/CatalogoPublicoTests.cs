using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales del catálogo público de servicios (HU06-T6)
    public sealed class CatalogoPublicoTests : PruebaConBaseDeDatos
    {
        private readonly ServiciosController _servicios;
        private readonly PortafolioController _portafolio;
        private readonly SitioController _sitio;

        public CatalogoPublicoTests()
        {
            _servicios = new ServiciosController(Db);
            _portafolio = new PortafolioController(Db);
            _sitio = new SitioController(Db);
        }

        private async Task<int> Servicio(string nombre, string estado = Models.Servicio.Publicado) =>
            (int)Valor(Dato<object>(await _servicios.Crear(new()
            {
                Nombre = nombre, Descripcion = "Breve", Beneficios = "Rápido\nSeguro", Caracteristicas = "Concreto armado", Estado = estado
            }), "servicio"), "id");

        private async Task<int> Proyecto(string nombre, int? servicioId, bool publicar, int anio = 2025)
        {
            var id = (int)Valor(Dato<object>(await _portafolio.Crear(new()
            {
                Nombre = nombre, TipoObra = "Edificio multifamiliar", Ubicacion = "Surco", Anio = anio, Descripcion = "Obra", ServicioId = servicioId
            }), "proyecto"), "id");
            await _portafolio.SubirImagenes(id, new() { Archivos.Formulario("a.png", Archivos.Png()) });
            if (publicar) await _portafolio.Publicar(id);
            return id;
        }

        [Fact]
        public async Task Detalle_devuelveBeneficiosYCaracteristicasComoListas()
        {
            var id = await Servicio("Construcción");

            var r = await _sitio.DetalleServicio(id);

            Assert.Equal(200, Estado(r));
            Assert.Equal(new[] { "Rápido", "Seguro" }, Dato<string[]>(r, "beneficios"));
            Assert.Equal(new[] { "Concreto armado" }, Dato<string[]>(r, "caracteristicas"));
        }

        [Fact]
        public async Task Detalle_deServicioOcultoOInexistente_noSeExpone()
        {
            var oculto = await Servicio("Interno", Models.Servicio.Oculto);

            Assert.Equal(404, Estado(await _sitio.DetalleServicio(oculto)));
            Assert.Equal(404, Estado(await _sitio.DetalleServicio(999)));
        }

        [Fact]
        public async Task ProyectosRelacionados_soloPublicadosDelMismoServicio()
        {
            var construccion = await Servicio("Construcción");
            var remodelacion = await Servicio("Remodelación");
            await Proyecto("Palmeras", construccion, publicar: true, anio: 2024);
            await Proyecto("Cedros", construccion, publicar: true, anio: 2025);
            await Proyecto("Borrador", construccion, publicar: false);
            await Proyecto("Oficinas", remodelacion, publicar: true);
            await Proyecto("Sin servicio", null, publicar: true);

            var r = await _sitio.DetalleServicio(construccion);

            var nombres = ((System.Collections.IEnumerable)Dato<object>(r, "proyectos")).Cast<object>().Select(p => (string)Valor(p, "nombre"));
            Assert.Equal(new[] { "Cedros", "Palmeras" }, nombres);
        }

        [Fact]
        public async Task DetalleDeProyecto_enlazaSuServicioSoloSiEstaPublicado()
        {
            var servicio = await Servicio("Construcción");
            var proyecto = await Proyecto("Palmeras", servicio, publicar: true);

            var enlace = Dato<object>(await _sitio.Proyecto(proyecto), "servicio");
            Assert.Equal(servicio, (int)Valor(enlace, "id"));

            await _servicios.Editar(servicio, new() { Nombre = "Construcción", Descripcion = "Breve", Estado = Models.Servicio.Oculto });
            Assert.Null(Dato<object?>(await _sitio.Proyecto(proyecto), "servicio"));
        }

        [Fact]
        public async Task Portafolio_rechazaServicioInexistente()
        {
            var r = await _portafolio.Crear(new() { Nombre = "X", TipoObra = "Remodelación", Anio = 2024, ServicioId = 999 });

            Assert.Equal(400, Estado(r));
            Assert.Empty(Db.Portafolio);
        }

        [Fact]
        public async Task EliminarServicio_dejaLosProyectosSinServicio()
        {
            var servicio = await Servicio("Construcción");
            var proyecto = await Proyecto("Palmeras", servicio, publicar: true);

            await _servicios.Eliminar(servicio);

            await using var leer = OtroContexto();
            var p = await leer.Portafolio.SingleAsync();
            Assert.Null(p.ServicioId);
            Assert.Equal(ProyectoPortafolio.Publicado, p.Estado);
            Assert.Equal(proyecto, p.Id);
        }
    }
}
