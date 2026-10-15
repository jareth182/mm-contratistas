using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales del mantenimiento del portafolio (HU08-T7)
    public sealed class PortafolioTests : PruebaConBaseDeDatos
    {
        private readonly PortafolioController _portafolio;

        public PortafolioTests()
        {
            _portafolio = new PortafolioController(Db);
        }

        private string CarpetaImagenes => Path.Combine(Carpeta, "Uploads", "portafolio");

        private static PortafolioController.ProyectoDto Completo() => new()
        {
            Nombre = "Condominio Las Palmeras",
            TipoObra = "Edificio multifamiliar",
            Ubicacion = "Chorrillos, Lima",
            Anio = 2025,
            Descripcion = "Edificio de cinco niveles con veinte departamentos."
        };

        private async Task<int> Crear(PortafolioController.ProyectoDto? dto = null)
        {
            var r = await _portafolio.Crear(dto ?? Completo());
            return (int)Valor(Dato<object>(r, "proyecto"), "id");
        }

        private Task<IActionResult> Subir(int id, params (string nombre, byte[] contenido)[] archivos) =>
            _portafolio.SubirImagenes(id, archivos.Select(a => Archivos.Formulario(a.nombre, a.contenido)).ToList());

        [Fact]
        public async Task Crear_guardaComoBorrador()
        {
            var r = await _portafolio.Crear(Completo());

            Assert.Equal(201, Estado(r));
            var p = await Db.Portafolio.SingleAsync();
            Assert.Equal(ProyectoPortafolio.Borrador, p.Estado);
            Assert.Equal("Condominio Las Palmeras", p.Nombre);
        }

        [Theory]
        [InlineData("", "Edificio multifamiliar", 2025)]
        [InlineData("Casa", "", 2025)]
        [InlineData("Casa", "Remodelación", 1980)]
        [InlineData("Casa", "Remodelación", 2100)]
        public async Task Crear_conDatosInvalidos_esRechazado(string nombre, string tipo, int anio)
        {
            var r = await _portafolio.Crear(new() { Nombre = nombre, TipoObra = tipo, Anio = anio });

            Assert.Equal(400, Estado(r));
            Assert.Empty(Db.Portafolio);
        }

        [Fact]
        public async Task Editar_actualizaYMantieneElEstado()
        {
            var id = await Crear();
            var cambios = Completo(); cambios.Nombre = "Las Palmeras II"; cambios.Anio = 2024;

            var r = await _portafolio.Editar(id, cambios);

            Assert.Equal(200, Estado(r));
            await using var leer = OtroContexto();
            var p = await leer.Portafolio.SingleAsync();
            Assert.Equal(("Las Palmeras II", 2024, ProyectoPortafolio.Borrador), (p.Nombre, p.Anio, p.Estado));
            Assert.Equal(404, Estado(await _portafolio.Editar(999, cambios)));
        }

        [Fact]
        public async Task SubirImagenes_guardaJpgYPng_yLaPrimeraEsPrincipal()
        {
            var id = await Crear();

            var r = await Subir(id, ("fachada.png", Archivos.Png()), ("interior.jpg", Archivos.Jpg()));

            Assert.Equal(200, Estado(r));
            var imagenes = await Db.ImagenesPortafolio.OrderBy(i => i.Id).ToListAsync();
            Assert.Equal(new[] { true, false }, imagenes.Select(i => i.EsPrincipal));
            Assert.All(imagenes, i => Assert.True(File.Exists(Path.Combine(CarpetaImagenes, i.Archivo))));
            Assert.Equal("fachada.png", imagenes[0].NombreOriginal);
        }

        [Theory]
        [InlineData("plano.gif")]      // extensión no permitida
        [InlineData("falso.png")]      // extensión correcta, contenido que no es imagen
        [InlineData("foto.jpg")]       // PNG renombrado a .jpg
        public async Task SubirImagenes_formatoInvalido_esRechazado(string nombre)
        {
            var id = await Crear();
            var contenido = nombre == "falso.png" ? System.Text.Encoding.ASCII.GetBytes("no soy imagen") : Archivos.Png();

            var r = await Subir(id, (nombre, contenido));

            Assert.Equal(400, Estado(r));
            Assert.Empty(Db.ImagenesPortafolio);
        }

        [Fact]
        public async Task SubirImagenes_masDe5MB_esRechazado_yNoGuardaNingunaDelLote()
        {
            var id = await Crear();

            var r = await Subir(id, ("ok.png", Archivos.Png()), ("grande.png", Archivos.Png(5 * 1024 * 1024)));

            Assert.Equal(400, Estado(r));
            Assert.Empty(Db.ImagenesPortafolio);
            Assert.False(Directory.Exists(CarpetaImagenes) && Directory.EnumerateFiles(CarpetaImagenes).Any());
        }

        [Fact]
        public async Task MarcarPrincipal_dejaUnaSolaPrincipal()
        {
            var id = await Crear();
            await Subir(id, ("a.png", Archivos.Png()), ("b.png", Archivos.Png()));
            var segunda = await Db.ImagenesPortafolio.OrderBy(i => i.Id).Skip(1).FirstAsync();

            Assert.Equal(200, Estado(await _portafolio.MarcarPrincipal(segunda.Id)));

            await using var leer = OtroContexto();
            Assert.Equal(segunda.Id, (await leer.ImagenesPortafolio.SingleAsync(i => i.EsPrincipal)).Id);
        }

        // ---------- HU08-T5: publicar con datos mínimos ----------

        [Fact]
        public async Task Publicar_conDatosCompletosEImagen_quedaPublicado()
        {
            var id = await Crear();
            await Subir(id, ("a.png", Archivos.Png()));

            var r = await _portafolio.Publicar(id);

            Assert.Equal(200, Estado(r));
            Assert.Equal(ProyectoPortafolio.Publicado, (await OtroContexto().Portafolio.SingleAsync()).Estado);
        }

        [Fact]
        public async Task Publicar_sinDatosMinimos_indicaQueFalta()
        {
            var id = await Crear(new() { Nombre = "Casa", TipoObra = "Remodelación", Anio = 2024 });

            var r = await _portafolio.Publicar(id);

            Assert.Equal(400, Estado(r));
            var mensaje = Dato<string>(r, "mensaje");
            Assert.Contains("ubicación", mensaje);
            Assert.Contains("descripción", mensaje);
            Assert.Contains("imagen principal", mensaje);
            Assert.Equal(ProyectoPortafolio.Borrador, (await Db.Portafolio.SingleAsync()).Estado);
        }

        [Fact]
        public async Task Despublicar_vuelveABorrador()
        {
            var id = await Crear();
            await Subir(id, ("a.png", Archivos.Png()));
            await _portafolio.Publicar(id);

            Assert.Equal(200, Estado(await _portafolio.Despublicar(id)));
            Assert.Equal(ProyectoPortafolio.Borrador, (await OtroContexto().Portafolio.SingleAsync()).Estado);
        }

        [Fact]
        public async Task EliminarImagenPrincipal_pasaLaPrincipalAOtra_yLaUltimaDevuelveABorrador()
        {
            var id = await Crear();
            await Subir(id, ("a.png", Archivos.Png()), ("b.png", Archivos.Png()));
            await _portafolio.Publicar(id);
            var imagenes = await Db.ImagenesPortafolio.AsNoTracking().OrderBy(i => i.Id).ToListAsync();

            await _portafolio.EliminarImagen(imagenes[0].Id);
            await using (var leer = OtroContexto())
            {
                Assert.True((await leer.ImagenesPortafolio.SingleAsync()).EsPrincipal);
                Assert.Equal(ProyectoPortafolio.Publicado, (await leer.Portafolio.SingleAsync()).Estado);
            }

            await _portafolio.EliminarImagen(imagenes[1].Id);
            await using (var leer = OtroContexto())
                Assert.Equal(ProyectoPortafolio.Borrador, (await leer.Portafolio.SingleAsync()).Estado);
            Assert.Empty(Directory.EnumerateFiles(CarpetaImagenes));
        }

        // ---------- HU08-T6: eliminar ----------

        [Fact]
        public async Task Eliminar_borraElProyectoSusImagenesYArchivos()
        {
            var id = await Crear();
            var otro = await Crear();
            await Subir(id, ("a.png", Archivos.Png()), ("b.jpg", Archivos.Jpg()));
            await Subir(otro, ("c.png", Archivos.Png()));

            var r = await _portafolio.Eliminar(id);

            Assert.Equal(200, Estado(r));
            await using var leer = OtroContexto();
            Assert.Equal(otro, (await leer.Portafolio.SingleAsync()).Id);
            Assert.Equal(otro, (await leer.ImagenesPortafolio.SingleAsync()).ProyectoPortafolioId);
            Assert.Single(Directory.EnumerateFiles(CarpetaImagenes));
            Assert.Equal(404, Estado(await _portafolio.Eliminar(id)));
        }
    }
}
