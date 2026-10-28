using System.Security.Claims;
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
    // Pruebas funcionales de HU13: registro de avance de un hito y recálculo del avance de la obra (HU13-T7)
    public sealed class AvancesHitoTests : PruebaConBaseDeDatos
    {
        private class CorreoFalso : IEmailService
        {
            public List<(string Para, string Asunto, string Cuerpo)> Enviados { get; } = new();
            public bool Falla { get; set; }

            public Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, IReadOnlyList<AdjuntoCorreo>? adjuntos = null)
            {
                if (Falla) throw new InvalidOperationException("SMTP caído");
                Enviados.Add((destinatario, asunto, cuerpoHtml));
                return Task.CompletedTask;
            }
        }

        private readonly CorreoFalso _correo = new();
        private readonly ObrasController _obras;
        private readonly int _obraId;
        private readonly int _excavacionId;   // peso 40
        private readonly int _estructuraId;   // peso 60

        public AvancesHitoTests()
        {
            var invitacion = new InvitacionService(_correo, new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, new NotificacionHitoService(_correo, NullLogger<NotificacionHitoService>.Instance))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Emily Quispe") }, "Prueba"))
                    }
                }
            };

            var cliente = new Cliente { Nombre = "Inversiones Del Río", Documento = "20512345678", TipoDocumento = "RUC", Correo = "contacto@delrio.pe" };
            Db.Clientes.Add(cliente);
            Db.SaveChanges();
            var obra = new Obra { Nombre = "Los Cedros", Cliente = cliente.Nombre, ClienteId = cliente.Id };
            obra.Hitos.Add(new Hito { Nombre = "Excavación", Orden = 1, Peso = 40 });
            obra.Hitos.Add(new Hito { Nombre = "Estructura", Orden = 2, Peso = 60 });
            Db.Obras.Add(obra);
            Db.SaveChanges();
            _obraId = obra.Id;
            _excavacionId = obra.Hitos.Single(h => h.Orden == 1).Id;
            _estructuraId = obra.Hitos.Single(h => h.Orden == 2).Id;
        }

        private static IFormFile Foto(string nombre = "avance.png") => Archivos.Formulario(nombre, Archivos.Png());

        private async Task<Hito> Hito(int id)
        {
            await using var db = OtroContexto();
            return await db.Hitos.SingleAsync(h => h.Id == id);
        }

        private async Task<Obra> Obra()
        {
            await using var db = OtroContexto();
            return await db.Obras.SingleAsync(o => o.Id == _obraId);
        }

        private async Task<int> CantidadAvances(int hitoId)
        {
            await using var db = OtroContexto();
            return await db.AvancesHito.CountAsync(a => a.HitoId == hitoId);
        }

        [Fact]
        public async Task RegistrarAvance_guardaElHistorial_conFechaYResponsable()
        {
            var r = await _obras.RegistrarAvance(_excavacionId, 30, "Trazo y replanteo", new List<IFormFile> { Foto() });

            Assert.Equal(200, Estado(r));
            var hito = await Hito(_excavacionId);
            Assert.Equal((30, "En proceso", "Trazo y replanteo"), (hito.AvancePorcentaje, hito.Estado, hito.DescripcionAvance));

            await using var db = OtroContexto();
            var avance = await db.AvancesHito.Include(a => a.Fotos).SingleAsync();
            Assert.Equal((30, "Trazo y replanteo", "Emily Quispe"), (avance.Porcentaje, avance.Descripcion, avance.Responsable));
            Assert.True((DateTime.UtcNow - avance.Fecha).TotalMinutes < 1);
            Assert.Single(avance.Fotos);
            Assert.True(File.Exists(Path.Combine(Carpeta, "Uploads", "avances", avance.Fotos[0].Archivo)));
        }

        [Fact]
        public async Task RegistrarAvance_recalculaElAvanceDeLaObraSegunElPeso()
        {
            await _obras.RegistrarAvance(_excavacionId, 50, "Mitad de la excavación", null);
            Assert.Equal(20, (await Obra()).AvancePorcentaje);          // 50 % de un hito de peso 40

            await _obras.RegistrarAvance(_excavacionId, 100, "Excavación terminada", null);
            Assert.Equal(40, (await Obra()).AvancePorcentaje);

            var r = await _obras.RegistrarAvance(_estructuraId, 50, "Primer vaciado", null);
            Assert.Equal(70, (await Obra()).AvancePorcentaje);          // 40 + 50 % de 60
            Assert.Equal(70, Dato<int>(r, "avanceObra"));
        }

        [Fact]
        public async Task AlLlegarA100_elHitoQuedaCompletado_conFechaRealYAvisaAlCliente()
        {
            var r = await _obras.RegistrarAvance(_excavacionId, 100, "Excavación terminada", null);

            var hito = await Hito(_excavacionId);
            Assert.Equal(("Completado", 100, DateOnly.FromDateTime(DateTime.Now)), (hito.Estado, hito.AvancePorcentaje, hito.FechaReal!.Value));
            var correo = Assert.Single(_correo.Enviados);
            Assert.Equal("contacto@delrio.pe", correo.Para);
            Assert.Contains("Los Cedros", correo.Asunto);
            Assert.Contains("se notificó al cliente", Dato<string>(r, "mensaje"));
        }

        [Fact]
        public async Task SinCompletar_noSeEnviaCorreo()
        {
            await _obras.RegistrarAvance(_excavacionId, 99, "Casi listo", null);

            Assert.Empty(_correo.Enviados);
        }

        [Fact]
        public async Task SiFallaElCorreo_elAvanceIgualQuedaRegistrado()
        {
            _correo.Falla = true;

            var r = await _obras.RegistrarAvance(_excavacionId, 100, "Excavación terminada", null);

            Assert.Equal(200, Estado(r));
            Assert.Equal("Completado", (await Hito(_excavacionId)).Estado);
            Assert.Contains("no se pudo enviar el correo", Dato<string>(r, "mensaje"));
        }

        [Fact]
        public async Task ClienteSinCorreo_noRompeElRegistro()
        {
            Db.Clientes.Single().Correo = null;
            await Db.SaveChangesAsync();

            var r = await _obras.RegistrarAvance(_excavacionId, 100, "Excavación terminada", null);

            Assert.Equal(200, Estado(r));
            Assert.Empty(_correo.Enviados);
            Assert.Contains("no tiene correo", Dato<string>(r, "mensaje"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(101)]
        public async Task PorcentajeFueraDeRango_esRechazado(int porcentaje)
        {
            Assert.Equal(400, Estado(await _obras.RegistrarAvance(_excavacionId, porcentaje, "Trabajo", null)));
            Assert.Equal(0, await CantidadAvances(_excavacionId));
        }

        [Fact]
        public async Task ElPorcentajeDebeSerMayorAlAvanceActual()
        {
            await _obras.RegistrarAvance(_excavacionId, 40, "Primer tramo", null);

            Assert.Equal(400, Estado(await _obras.RegistrarAvance(_excavacionId, 40, "Mismo avance", null)));
            Assert.Equal(400, Estado(await _obras.RegistrarAvance(_excavacionId, 30, "Retroceso", null)));
            Assert.Equal(1, await CantidadAvances(_excavacionId));
            Assert.Equal(40, (await Hito(_excavacionId)).AvancePorcentaje);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task LaDescripcionEsObligatoria(string? descripcion)
        {
            Assert.Equal(400, Estado(await _obras.RegistrarAvance(_excavacionId, 30, descripcion, null)));
            Assert.Equal(0, await CantidadAvances(_excavacionId));
        }

        [Fact]
        public async Task HitoCompletado_noAdmiteMasAvances()
        {
            await _obras.RegistrarAvance(_excavacionId, 100, "Terminado", null);

            Assert.Equal(400, Estado(await _obras.RegistrarAvance(_excavacionId, 100, "Otra vez", null)));
            Assert.Equal(1, await CantidadAvances(_excavacionId));
        }

        [Fact]
        public async Task HitoInexistente_devuelve404()
        {
            Assert.Equal(404, Estado(await _obras.RegistrarAvance(999, 30, "Trabajo", null)));
        }

        [Fact]
        public async Task FotografiaInvalida_rechazaTodoElAvance()
        {
            var falsa = Archivos.Formulario("plano.png", Archivos.Pdf());            // extensión PNG, contenido PDF
            var documento = Archivos.Formulario("informe.pdf", Archivos.Pdf());
            var grande = Archivos.Formulario("grande.png", Archivos.Png((int)ObrasController.TamanoMaximoFoto));

            foreach (var archivo in new[] { falsa, documento, grande })
                Assert.Equal(400, Estado(await _obras.RegistrarAvance(_excavacionId, 30, "Trabajo", new List<IFormFile> { Foto(), archivo })));

            Assert.Equal(0, await CantidadAvances(_excavacionId));
            Assert.Equal(0, (await Hito(_excavacionId)).AvancePorcentaje);
            Assert.False(Directory.Exists(Path.Combine(Carpeta, "Uploads", "avances")));
        }

        [Fact]
        public async Task MaximoDeCincoFotografias()
        {
            var seis = Enumerable.Range(1, ObrasController.MaximoFotosPorAvance + 1).Select(i => Foto($"f{i}.png")).ToList();
            var cinco = seis.Take(ObrasController.MaximoFotosPorAvance).ToList();

            Assert.Equal(400, Estado(await _obras.RegistrarAvance(_excavacionId, 30, "Trabajo", seis)));
            Assert.Equal(200, Estado(await _obras.RegistrarAvance(_excavacionId, 30, "Trabajo", cinco)));
        }

        [Fact]
        public async Task Historial_listaDelMasRecienteAlMasAntiguo_conSusFotos()
        {
            await _obras.RegistrarAvance(_excavacionId, 20, "Primero", new List<IFormFile> { Foto("a.png"), Archivos.Formulario("b.jpg", Archivos.Jpg()) });
            await _obras.RegistrarAvance(_excavacionId, 60, "Segundo", null);

            var r = await _obras.HistorialAvances(_excavacionId);

            var historial = Lista(r);
            Assert.Equal(new[] { "Segundo", "Primero" }, historial.Select(a => (string)Valor(a, "descripcion")));
            Assert.Equal(new[] { 60, 20 }, historial.Select(a => (int)Valor(a, "porcentaje")));
            Assert.Equal(2, ((System.Collections.IEnumerable)Valor(historial[1], "fotos")).Cast<object>().Count());
            Assert.IsType<NotFoundObjectResult>(await _obras.HistorialAvances(999));
        }

        [Fact]
        public async Task Fotografia_sePuedeConsultarDespuesDeGuardarla()
        {
            await _obras.RegistrarAvance(_excavacionId, 20, "Con foto", new List<IFormFile> { Foto() });
            var fotoId = (await Db.FotosAvance.SingleAsync()).Id;

            var r = await _obras.VerFotoAvance(fotoId);

            Assert.Equal("image/png", Assert.IsType<PhysicalFileResult>(r).ContentType);
            Assert.IsType<NotFoundObjectResult>(await _obras.VerFotoAvance(999));
        }

        [Fact]
        public async Task HitoConAvance_yaNoSePuedeEliminar_yElHistorialNoSePierde()
        {
            await _obras.RegistrarAvance(_excavacionId, 10, "Primer tramo", null);

            Assert.IsType<BadRequestObjectResult>(await _obras.EliminarHito(_excavacionId));
            Assert.Equal(1, await CantidadAvances(_excavacionId));
        }
    }
}
