using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales de HU23: vigencias de SCTR y examen médico, renovación y estado documental (HU23-T6)
    public sealed class VigenciasPersonalTests : PruebaConBaseDeDatos
    {
        private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Now);
        private readonly PersonalController _personal;
        private readonly int _id;

        public VigenciasPersonalTests()
        {
            _personal = new PersonalController(Db);
            _id = NuevoColaborador("Rómulo", "41258963");
        }

        private string CarpetaVigencias => Path.Combine(Carpeta, "Uploads", "vigencias");

        private int NuevoColaborador(string nombres, string documento)
        {
            var t = new Trabajador { Nombres = nombres, Apellidos = "Ccahuana", Documento = documento, Especialidad = "Operario", JornalDiario = 160 };
            Db.Personal.Add(t);
            Db.SaveChanges();
            return t.Id;
        }

        private static Microsoft.AspNetCore.Http.IFormFile Pdf(string nombre = "sctr-2026.pdf") => Archivos.Formulario(nombre, Archivos.Pdf());

        private Task<IActionResult> Registrar(string? tipo, DateOnly? inicio, DateOnly? vencimiento, Microsoft.AspNetCore.Http.IFormFile? archivo, int? id = null) =>
            _personal.RegistrarVigencia(id ?? _id, new PersonalController.VigenciaDto { Tipo = tipo, FechaInicio = inicio, FechaVencimiento = vencimiento, Archivo = archivo });

        private void Vigencia(int trabajadorId, string tipo, int diasParaVencer, int? diasDeDuracion = 365)
        {
            var vence = Hoy.AddDays(diasParaVencer);
            Db.Vigencias.Add(new VigenciaDocumento { TrabajadorId = trabajadorId, Tipo = tipo, FechaInicio = vence.AddDays(-(diasDeDuracion ?? 365)), FechaVencimiento = vence });
            Db.SaveChanges();
        }

        private async Task<int> Guardadas()
        {
            await using var db = OtroContexto();
            return await db.Vigencias.CountAsync();
        }

        private int ArchivosGuardados => Directory.Exists(CarpetaVigencias) ? Directory.GetFiles(CarpetaVigencias).Length : 0;

        private static string Mensaje(IActionResult r) => Dato<string>(r, "mensaje");

        // ---- Registrar vigencia (T3) ----

        [Fact]
        public async Task Registrar_guardaLasFechasYElDocumento()
        {
            var r = await Registrar("SCTR", Hoy, Hoy.AddYears(1), Pdf("sctr-ramos.pdf"));

            Assert.Equal(201, Estado(r));
            Assert.Equal("SCTR registrado correctamente.", Mensaje(r));
            await using var db = OtroContexto();
            var v = await db.Vigencias.SingleAsync();
            Assert.Equal(_id, v.TrabajadorId);
            Assert.Equal("SCTR", v.Tipo);
            Assert.Equal(Hoy, v.FechaInicio);
            Assert.Equal(Hoy.AddYears(1), v.FechaVencimiento);
            Assert.Equal("sctr-ramos.pdf", v.ArchivoNombre);
            Assert.True(File.Exists(Path.Combine(CarpetaVigencias, v.Archivo)));
            Assert.NotEqual("sctr-ramos.pdf", v.Archivo);
        }

        [Theory]
        [InlineData("sctr", "SCTR")]
        [InlineData(" examen médico ", "Examen Médico")]
        public async Task Registrar_aceptaElTipoSinImportarMayusculas(string escrito, string esperado)
        {
            Assert.Equal(201, Estado(await Registrar(escrito, Hoy, Hoy.AddYears(1), Pdf())));
            Assert.Equal(esperado, (await Db.Vigencias.SingleAsync()).Tipo);
        }

        [Theory]
        [InlineData("constancia.pdf")]
        [InlineData("foto.jpg")]
        [InlineData("foto.png")]
        public async Task Registrar_aceptaPdfJpgYPng(string nombre)
        {
            var contenido = nombre.EndsWith("pdf") ? Archivos.Pdf() : nombre.EndsWith("png") ? Archivos.Png() : Archivos.Jpg();

            var r = await Registrar("SCTR", Hoy, Hoy.AddYears(1), Archivos.Formulario(nombre, contenido));

            Assert.Equal(201, Estado(r));
        }

        [Fact]
        public async Task Registrar_sinDocumento_seRechazaYNoSeGuardaNada()
        {
            var r = await Registrar("SCTR", Hoy, Hoy.AddYears(1), null);

            Assert.Equal(400, Estado(r));
            Assert.Contains("Adjunte el documento", Mensaje(r));
            Assert.Equal(0, await Guardadas());
        }

        [Fact]
        public async Task Registrar_conUnArchivoInvalido_seRechazaSinGuardarlo()
        {
            var ejecutable = await Registrar("SCTR", Hoy, Hoy.AddYears(1), Archivos.Formulario("virus.exe", new byte[] { 1, 2, 3, 4 }));
            var falsoPdf = await Registrar("SCTR", Hoy, Hoy.AddYears(1), Archivos.Formulario("falso.pdf", new byte[] { 1, 2, 3, 4, 5 }));
            var enorme = new byte[11 * 1024 * 1024];
            Archivos.Pdf().CopyTo(enorme, 0);
            var grande = await Registrar("SCTR", Hoy, Hoy.AddYears(1), Archivos.Formulario("grande.pdf", enorme));
            var nombreLargo = await Registrar("SCTR", Hoy, Hoy.AddYears(1), Archivos.Formulario(new string('a', 148) + ".pdf", Archivos.Pdf()));

            Assert.Equal(new[] { 400, 400, 400, 400 }, new[] { ejecutable, falsoPdf, grande, nombreLargo }.Select(Estado));
            Assert.Contains("PDF, JPG o PNG", Mensaje(ejecutable));
            Assert.Contains("10 MB", Mensaje(grande));
            Assert.Equal(0, await Guardadas());
            Assert.Equal(0, ArchivosGuardados);
        }

        [Fact]
        public async Task Registrar_validaTipoFechasYColaborador()
        {
            Assert.Equal(400, Estado(await Registrar("Licencia", Hoy, Hoy.AddDays(30), Pdf())));
            Assert.Equal(400, Estado(await Registrar(null, Hoy, Hoy.AddDays(30), Pdf())));
            Assert.Equal(400, Estado(await Registrar("SCTR", null, Hoy.AddDays(30), Pdf())));
            Assert.Equal(400, Estado(await Registrar("SCTR", Hoy, null, Pdf())));
            Assert.Equal(400, Estado(await Registrar("SCTR", Hoy, Hoy, Pdf())));
            Assert.Equal(400, Estado(await Registrar("SCTR", Hoy, Hoy.AddDays(-1), Pdf())));
            Assert.Equal(404, Estado(await Registrar("SCTR", Hoy, Hoy.AddDays(30), Pdf(), id: 999)));
            Assert.Equal(0, await Guardadas());
            Assert.Equal(0, ArchivosGuardados);
        }

        // ---- Estado documental (T4) ----

        [Theory]
        [InlineData(-1, "Vencido")]
        [InlineData(0, "Por vencer")]
        [InlineData(1, "Por vencer")]
        [InlineData(30, "Por vencer")]
        [InlineData(31, "Vigente")]
        [InlineData(400, "Vigente")]
        public void ElEstado_seCalculaSegunLosDiasQueFaltan(int dias, string esperado)
        {
            Assert.Equal(esperado, VigenciaDocumento.EstadoAl(Hoy.AddDays(dias), Hoy));
        }

        [Fact]
        public async Task ElListado_indicaElEstadoDocumentalDeCadaColaborador()
        {
            var vigente = NuevoColaborador("Ana", "10000001");
            Vigencia(vigente, VigenciaDocumento.Sctr, 200);
            Vigencia(vigente, VigenciaDocumento.ExamenMedico, 200);
            var porVencer = NuevoColaborador("Bruno", "10000002");
            Vigencia(porVencer, VigenciaDocumento.Sctr, 14);
            Vigencia(porVencer, VigenciaDocumento.ExamenMedico, 200);
            var vencido = NuevoColaborador("Carla", "10000003");
            Vigencia(vencido, VigenciaDocumento.Sctr, -23);
            Vigencia(vencido, VigenciaDocumento.ExamenMedico, 14);
            var incompleto = NuevoColaborador("Diego", "10000004");
            Vigencia(incompleto, VigenciaDocumento.Sctr, 200);

            var lista = Lista(await _personal.Listar(null, false)).ToDictionary(t => (int)Valor(t, "id"));

            Assert.Equal("Vigente", Valor(lista[vigente], "estadoDocumental"));
            Assert.Equal("Por vencer", Valor(lista[porVencer], "estadoDocumental"));
            Assert.Equal("Vencido", Valor(lista[vencido], "estadoDocumental"));
            Assert.Equal("Sin registrar", Valor(lista[incompleto], "estadoDocumental"));
            Assert.Equal("Sin registrar", Valor(lista[_id], "estadoDocumental"));
            Assert.Equal("Vencido", Valor(Valor(lista[vencido], "sctr"), "estado"));
            Assert.Equal(-23, Valor(Valor(lista[vencido], "sctr"), "diasRestantes"));
            Assert.Equal("Por vencer", Valor(Valor(lista[vencido], "examenMedico"), "estado"));
            Assert.Equal(Hoy.AddDays(14), Valor(Valor(lista[vencido], "examenMedico"), "vencimiento"));
            Assert.Null(Valor(Valor(lista[incompleto], "examenMedico"), "vencimiento"));
        }

        [Fact]
        public async Task ElListado_conservaLaBusqueda_yElEstadoDocumental()
        {
            Vigencia(_id, VigenciaDocumento.Sctr, 200);
            Vigencia(_id, VigenciaDocumento.ExamenMedico, 200);
            NuevoColaborador("Otro", "10000009");

            var lista = Lista(await _personal.Listar("41258963", false));

            Assert.Equal("Vigente", Valor(Assert.Single(lista), "estadoDocumental"));
        }

        [Fact]
        public async Task ElEstadoDelListado_usaLaVigenciaMasReciente_noLasAnteriores()
        {
            Vigencia(_id, VigenciaDocumento.Sctr, -400);      // histórica, vencida
            Vigencia(_id, VigenciaDocumento.Sctr, 150);       // vigente
            Vigencia(_id, VigenciaDocumento.ExamenMedico, 150);

            var ficha = ((ObjectResult)await _personal.Obtener(_id)).Value!;

            Assert.Equal("Vigente", Valor(ficha, "estadoDocumental"));
        }

        [Fact]
        public async Task Obtener_devuelveLaFichaConSuEstado_oNoEncontrado()
        {
            Vigencia(_id, VigenciaDocumento.Sctr, -5);

            var r = await _personal.Obtener(_id);

            Assert.Equal(200, Estado(r));
            Assert.Equal("Vencido", Valor(Valor(((ObjectResult)r).Value!, "sctr"), "estado"));
            Assert.Equal("Rómulo Ccahuana", Valor(((ObjectResult)r).Value!, "nombreCompleto"));
            Assert.Equal(404, Estado(await _personal.Obtener(999)));
        }

        // ---- Renovación conservando el historial (T5) ----

        [Fact]
        public async Task Renovar_agregaUnaVigenciaNuevaYConservaLaAnterior()
        {
            Assert.Equal(201, Estado(await Registrar("SCTR", Hoy.AddMonths(-11), Hoy.AddDays(14), Pdf("anterior.pdf"))));

            var r = await Registrar("SCTR", Hoy, Hoy.AddYears(1), Pdf("renovacion.pdf"));

            Assert.Equal(201, Estado(r));
            Assert.Equal("SCTR renovado correctamente. La vigencia anterior quedó en el historial.", Mensaje(r));
            Assert.Equal(2, await Guardadas());
            Assert.Equal(2, ArchivosGuardados);
            var historial = Lista(await _personal.Vigencias(_id));
            Assert.Equal(new[] { "renovacion.pdf", "anterior.pdf" }, historial.Select(v => (string)Valor(v, "archivo")));
            Assert.Equal(new[] { true, false }, historial.Select(v => (bool)Valor(v, "actual")));
        }

        [Fact]
        public async Task Renovar_unSctrVencido_dejaAlColaboradorVigente()
        {
            Vigencia(_id, VigenciaDocumento.Sctr, -5);
            Vigencia(_id, VigenciaDocumento.ExamenMedico, 100);
            Assert.Equal("Vencido", Valor(Lista(await _personal.Listar(null, false)).Single(), "estadoDocumental"));

            await Registrar("SCTR", Hoy, Hoy.AddYears(1), Pdf());

            Assert.Equal("Vigente", Valor(Lista(await _personal.Listar(null, false)).Single(), "estadoDocumental"));
        }

        [Fact]
        public async Task Renovar_queNoExtiendeLaCobertura_seRechazaSinGuardarElArchivo()
        {
            await Registrar("SCTR", Hoy, Hoy.AddYears(1), Pdf());
            var antes = ArchivosGuardados;

            var igual = await Registrar("SCTR", Hoy.AddDays(1), Hoy.AddYears(1), Pdf());
            var anterior = await Registrar("SCTR", Hoy.AddDays(-400), Hoy.AddDays(30), Pdf());

            Assert.Equal(400, Estado(igual));
            Assert.Equal(400, Estado(anterior));
            Assert.Contains($"{Hoy.AddYears(1):dd/MM/yyyy}", Mensaje(igual));
            Assert.Equal(1, await Guardadas());
            Assert.Equal(antes, ArchivosGuardados);
        }

        [Fact]
        public async Task Renovar_unDocumento_noAfectaAlOtro()
        {
            await Registrar("SCTR", Hoy, Hoy.AddYears(1), Pdf());

            var r = await Registrar("Examen Médico", Hoy, Hoy.AddDays(60), Pdf("emo.pdf"));

            Assert.Equal(201, Estado(r));
            Assert.Equal("Examen Médico registrado correctamente.", Mensaje(r));
            Assert.All(Lista(await _personal.Vigencias(_id)), v => Assert.True((bool)Valor(v, "actual")));
        }

        [Fact]
        public async Task LosDocumentosAnteriores_siguenDisponiblesParaDescarga()
        {
            await Registrar("SCTR", Hoy.AddMonths(-11), Hoy.AddDays(14), Pdf("anterior.pdf"));
            await Registrar("SCTR", Hoy, Hoy.AddYears(1), Pdf("nuevo.pdf"));

            foreach (var v in Lista(await _personal.Vigencias(_id)))
            {
                var r = await _personal.DescargarDocumentoVigencia(_id, (int)Valor(v, "id"));
                Assert.Equal((string)Valor(v, "archivo"), Assert.IsType<PhysicalFileResult>(r).FileDownloadName);
            }
        }

        // ---- Historial y descarga ----

        [Fact]
        public async Task Vigencias_conUnaSinDocumento_indicaArchivoNulo_ySuDescargaEs404()
        {
            Vigencia(_id, VigenciaDocumento.Sctr, 100);      // registrada antes de HU23, sin archivo
            var id = Db.Vigencias.Single().Id;

            var item = Lista(await _personal.Vigencias(_id)).Single();

            Assert.Null(Valor(item, "archivo"));
            Assert.IsType<NotFoundObjectResult>(await _personal.DescargarDocumentoVigencia(_id, id));
        }

        [Fact]
        public async Task Descargar_deOtroColaboradorOInexistente_devuelve404()
        {
            await Registrar("SCTR", Hoy, Hoy.AddYears(1), Pdf());
            var vigenciaId = Db.Vigencias.Single().Id;
            var otro = NuevoColaborador("Otro", "10000009");

            Assert.IsType<NotFoundObjectResult>(await _personal.DescargarDocumentoVigencia(otro, vigenciaId));
            Assert.IsType<NotFoundObjectResult>(await _personal.DescargarDocumentoVigencia(_id, 99999));
        }

        [Fact]
        public async Task Descargar_sinElArchivoFisico_devuelve404_yNuncaSaleDeLaCarpeta()
        {
            await Registrar("SCTR", Hoy, Hoy.AddYears(1), Pdf());
            var vigencia = Db.Vigencias.Single();
            File.WriteAllText(Path.Combine(Carpeta, "secreto.txt"), "no debe entregarse");
            vigencia.Archivo = @"..\..\secreto.txt";
            vigencia.ArchivoNombre = "secreto.txt";
            await Db.SaveChangesAsync();

            Assert.IsType<NotFoundObjectResult>(await _personal.DescargarDocumentoVigencia(_id, vigencia.Id));
        }

        [Fact]
        public void Seguridad_soloElPersonalAdministraLaDocumentacion()
        {
            var autorizacion = typeof(PersonalController).GetCustomAttribute<AuthorizeAttribute>()!;

            Assert.Equal(NombresRol.Personal, autorizacion.Roles);
            Assert.DoesNotContain(NombresRol.Cliente, autorizacion.Roles!.Split(','));
            Assert.DoesNotContain(typeof(PersonalController).GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);
        }
    }
}
