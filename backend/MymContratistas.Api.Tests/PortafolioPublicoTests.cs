using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Controllers;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales del portafolio público (HU05-T6) y su reflejo del mantenimiento (HU08-T7)
    public sealed class PortafolioPublicoTests : PruebaConBaseDeDatos
    {
        private readonly PortafolioController _admin;
        private readonly SitioController _sitio;

        public PortafolioPublicoTests()
        {
            _admin = new PortafolioController(Db);
            _sitio = new SitioController(Db);
        }

        private async Task<int> Proyecto(string nombre, int anio, bool publicar, int imagenes = 1)
        {
            var r = await _admin.Crear(new()
            {
                Nombre = nombre, TipoObra = "Edificio multifamiliar", Ubicacion = "Chorrillos", Anio = anio, Descripcion = "Descripción de la obra."
            });
            var id = (int)Valor(Dato<object>(r, "proyecto"), "id");
            if (imagenes > 0)
                await _admin.SubirImagenes(id, Enumerable.Range(1, imagenes).Select(n => Archivos.Formulario($"foto{n}.png", Archivos.Png())).ToList());
            if (publicar) await _admin.Publicar(id);
            return id;
        }

        [Fact]
        public async Task Galeria_muestraSoloPublicados_ordenadosDelMasReciente()
        {
            await Proyecto("Antiguo", 2022, true);
            await Proyecto("Borrador", 2025, false);
            await Proyecto("Reciente", 2025, true);

            var lista = Lista(await _sitio.Proyectos());

            Assert.Equal(new[] { "Reciente", "Antiguo" }, lista.Select(p => (string)Valor(p, "nombre")));
            Assert.All(lista, p => Assert.NotNull(Valor(p, "imagenPrincipalId")));
        }

        [Fact]
        public async Task Detalle_incluyeDatosEImagenes_conLaPrincipalPrimero()
        {
            var id = await Proyecto("Las Palmeras", 2025, true, imagenes: 3);
            var tercera = await Db.ImagenesPortafolio.OrderBy(i => i.Id).Skip(2).FirstAsync();
            await _admin.MarcarPrincipal(tercera.Id);

            var r = await _sitio.Proyecto(id);

            Assert.Equal(200, Estado(r));
            Assert.Equal("Descripción de la obra.", Dato<string>(r, "descripcion"));
            var imagenes = ((System.Collections.IEnumerable)Dato<object>(r, "imagenes")).Cast<object>().ToList();
            Assert.Equal(3, imagenes.Count);
            Assert.Equal(tercera.Id, (int)Valor(imagenes[0], "id"));
        }

        [Fact]
        public async Task Borrador_noSeExponeNiSuDetalleNiSusImagenes()
        {
            var id = await Proyecto("Borrador", 2025, false);
            var imagen = await Db.ImagenesPortafolio.SingleAsync();

            Assert.Equal(404, Estado(await _sitio.Proyecto(id)));
            Assert.IsType<NotFoundResult>(await _sitio.Imagen(imagen.Id));
        }

        [Fact]
        public async Task ImagenDeProyectoPublicado_seSirveConSuTipo()
        {
            await Proyecto("Publicado", 2025, true);
            var imagen = await Db.ImagenesPortafolio.SingleAsync();

            var r = Assert.IsType<PhysicalFileResult>(await _sitio.Imagen(imagen.Id));
            Assert.Equal("image/png", r.ContentType);
        }

        [Fact]
        public async Task CambiosDelMantenimiento_seReflejanEnElSitio()
        {
            var id = await Proyecto("Las Palmeras", 2025, true);
            Assert.Single(Lista(await _sitio.Proyectos()));

            await _admin.Editar(id, new() { Nombre = "Las Palmeras II", TipoObra = "Remodelación", Ubicacion = "Surco", Anio = 2024, Descripcion = "Nueva" });
            Assert.Equal("Las Palmeras II", Valor(Lista(await _sitio.Proyectos()).Single(), "nombre"));

            await _admin.Despublicar(id);
            Assert.Empty(Lista(await _sitio.Proyectos()));

            await _admin.Publicar(id);
            await _admin.Eliminar(id);
            Assert.Empty(Lista(await _sitio.Proyectos()));
            Assert.Equal(404, Estado(await _sitio.Proyecto(id)));
        }
    }
}
