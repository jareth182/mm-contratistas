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
    // Pruebas funcionales de HU15: carga, visibilidad y eliminación de documentos del proyecto (HU15-T6)
    public sealed class DocumentosProyectoTests : PruebaConBaseDeDatos
    {
        private class SinCorreo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => Task.CompletedTask;
        }

        private readonly ObrasController _obras;
        private readonly int _obraId;
        private readonly int _clienteId;
        private readonly int _usuarioClienteId;

        public DocumentosProyectoTests()
        {
            var invitacion = new InvitacionService(new SinCorreo(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, new NotificacionHitoService(new SinCorreo(), NullLogger<NotificacionHitoService>.Instance));

            var cliente = new Cliente { Nombre = "Inversiones Del Río", Documento = "20512345678", TipoDocumento = "RUC", Correo = "contacto@delrio.pe" };
            Db.Clientes.Add(cliente);
            Db.SaveChanges();
            _clienteId = cliente.Id;
            var obra = new Obra { Nombre = "Los Cedros", Cliente = cliente.Nombre, ClienteId = cliente.Id };
            Db.Obras.Add(obra);
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Cliente);
            var usuario = new Usuario { Nombres = "Inversiones Del Río", Correo = "contacto@delrio.pe", RolId = rol.Id, PasswordHash = "x", ClienteId = cliente.Id };
            Db.Usuarios.Add(usuario);
            Db.SaveChanges();
            _obraId = obra.Id;
            _usuarioClienteId = usuario.Id;
        }

        private static IFormFile Pdf(string nombre = "contrato.pdf") => Archivos.Formulario(nombre, Archivos.Pdf());

        private async Task<(int estado, Documento? doc, string? mensaje)> Subir(IFormFile? archivo, string? clasificacion = "Contrato", bool visible = false, int? obraId = null)
        {
            var r = await _obras.SubirDocumento(obraId ?? _obraId, clasificacion, visible, archivo);
            return r switch
            {
                OkObjectResult ok => (200, (Documento)ok.Value!, null),
                BadRequestObjectResult mal => (400, null, (string)mal.Value!),
                NotFoundObjectResult => (404, null, null),
                _ => (500, null, null)
            };
        }

        private string CarpetaDocumentos => Path.Combine(Carpeta, "Uploads", "documentos");

        private ClienteController PanelDelCliente(int? usuarioId = null) => new(Db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, (usuarioId ?? _usuarioClienteId).ToString()),
                        new Claim(ClaimTypes.Name, "Inversiones Del Río")
                    }, "Prueba"))
                }
            }
        };

        private async Task<int> DocumentosGuardados()
        {
            await using var db = OtroContexto();
            return await db.Documentos.CountAsync();
        }

        [Theory]
        [InlineData("contrato.pdf", "Contrato")]
        [InlineData("plano.PNG", "Plano")]
        [InlineData("foto.jpeg", "Otro")]
        public async Task Subir_guardaElArchivo_yElRegistro(string nombre, string clasificacion)
        {
            var bytes = nombre.EndsWith("pdf") ? Archivos.Pdf() : nombre.EndsWith("PNG") ? Archivos.Png() : Archivos.Jpg();

            var (estado, doc, _) = await Subir(Archivos.Formulario(nombre, bytes), clasificacion, visible: true);

            Assert.Equal(200, estado);
            Assert.Equal((nombre, clasificacion, true, DateTime.Now.ToString("dd/MM/yyyy"), _obraId), (doc!.Nombre, doc.Clasificacion, doc.VisibleCliente, doc.FechaCarga, doc.ObraId));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(CarpetaDocumentos, doc.Archivo)));
        }

        [Fact]
        public async Task LaClasificacionSeNormalizaSinImportarMayusculas()
        {
            var (_, doc, _) = await Subir(Pdf(), "permiso");

            Assert.Equal("Permiso", doc!.Clasificacion);
        }

        [Fact]
        public async Task DosArchivosConElMismoNombre_noSeSobrescriben()
        {
            var (_, a, _) = await Subir(Archivos.Formulario("plano.pdf", Archivos.Pdf()));
            var (_, b, _) = await Subir(Archivos.Formulario("plano.pdf", System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 otro")));

            Assert.NotEqual(a!.Archivo, b!.Archivo);
            Assert.Equal(Archivos.Pdf(), await File.ReadAllBytesAsync(Path.Combine(CarpetaDocumentos, a.Archivo)));
            Assert.Equal(2, await DocumentosGuardados());
        }

        [Fact]
        public async Task UnNombreConRuta_noPuedeEscribirFueraDeLaCarpeta()
        {
            var (estado, doc, _) = await Subir(Archivos.Formulario(@"..\..\afuera.pdf", Archivos.Pdf()));

            Assert.Equal(200, estado);
            Assert.Equal("afuera.pdf", doc!.Nombre);
            Assert.StartsWith(CarpetaDocumentos, Path.GetFullPath(Path.Combine(CarpetaDocumentos, doc.Archivo)));
            Assert.False(File.Exists(Path.Combine(Carpeta, "afuera.pdf")));
        }

        [Fact]
        public async Task ArchivosInvalidos_sonRechazados_ynoDejanRastro()
        {
            var exe = Archivos.Formulario("virus.exe", Archivos.Pdf());
            var pdfFalso = Archivos.Formulario("falso.pdf", Archivos.Png());
            var pngFalso = Archivos.Formulario("falso.png", Archivos.Pdf());
            var grande = Archivos.Formulario("grande.pdf", Archivos.Pdf().Concat(new byte[10 * 1024 * 1024]).ToArray());

            foreach (var archivo in new[] { exe, pdfFalso, pngFalso, grande })
                Assert.Equal(400, (await Subir(archivo)).estado);
            Assert.Equal(400, (await Subir(null)).estado);

            Assert.Equal(0, await DocumentosGuardados());
            Assert.False(Directory.Exists(CarpetaDocumentos));
        }

        [Fact]
        public async Task ElLimiteDe10MbSeAceptaExacto()
        {
            var justo = Archivos.Pdf().Concat(new byte[10 * 1024 * 1024 - Archivos.Pdf().Length]).ToArray();

            Assert.Equal(200, (await Subir(Archivos.Formulario("justo.pdf", justo))).estado);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Factura")]
        public async Task LaClasificacionDebeSerUnaDeLasCinco(string? clasificacion)
        {
            Assert.Equal(400, (await Subir(Pdf(), clasificacion)).estado);
            Assert.Equal(0, await DocumentosGuardados());
        }

        [Fact]
        public async Task ObraInexistente_devuelve404()
        {
            Assert.Equal(404, (await Subir(Pdf(), obraId: 999)).estado);
        }

        [Fact]
        public async Task Descargar_devuelveElArchivoConSuNombreOriginal()
        {
            var (_, doc, _) = await Subir(Pdf("contrato final.pdf"));

            var r = await _obras.DescargarDocumento(doc!.Id);

            var archivo = Assert.IsType<PhysicalFileResult>(r);
            Assert.Equal(("application/pdf", "contrato final.pdf"), (archivo.ContentType, archivo.FileDownloadName));
            Assert.IsType<NotFoundObjectResult>(await _obras.DescargarDocumento(999));
        }

        [Fact]
        public async Task Eliminar_borraElRegistro_yElArchivo()
        {
            var (_, doc, _) = await Subir(Pdf());
            var ruta = Path.Combine(CarpetaDocumentos, doc!.Archivo);
            Assert.True(File.Exists(ruta));

            Assert.IsType<OkResult>(await _obras.EliminarDocumento(doc.Id));

            Assert.Equal(0, await DocumentosGuardados());
            Assert.False(File.Exists(ruta));
            Assert.IsType<NotFoundObjectResult>(await _obras.EliminarDocumento(doc.Id));
        }

        [Fact]
        public async Task DocumentoAnterior_seDescargaDesdeSuRutaOriginal_yAlEliminarNoSeBorraElArchivoCompartido()
        {
            Directory.CreateDirectory(Path.Combine(Carpeta, "Uploads"));
            var ruta = Path.Combine(Carpeta, "Uploads", "antiguo.pdf");
            await File.WriteAllBytesAsync(ruta, Archivos.Pdf());
            var legado = new Documento { Nombre = "antiguo.pdf", Clasificacion = "Otro", ObraId = _obraId, FechaCarga = "01/09/2026" };
            Db.Documentos.Add(legado);
            await Db.SaveChangesAsync();

            Assert.IsType<PhysicalFileResult>(await _obras.DescargarDocumento(legado.Id));
            Assert.IsType<OkResult>(await _obras.EliminarDocumento(legado.Id));
            Assert.True(File.Exists(ruta));
        }

        [Fact]
        public async Task Visibilidad_sePuedeCambiarDespuesDeSubir()
        {
            var (_, doc, _) = await Subir(Pdf(), visible: false);

            Assert.IsType<OkObjectResult>(await _obras.CambiarVisibilidadDocumento(doc!.Id, new() { VisibleCliente = true }));
            Assert.True((await Db.Documentos.AsNoTracking().SingleAsync()).VisibleCliente);
            Assert.IsType<NotFoundObjectResult>(await _obras.CambiarVisibilidadDocumento(999, new() { VisibleCliente = true }));
        }

        [Fact]
        public async Task ElPanelDelCliente_soloMuestraLosDocumentosVisibles()
        {
            await Subir(Pdf("contrato.pdf"), "Contrato", visible: true);
            await Subir(Pdf("interno.pdf"), "Otro", visible: false);

            var obra = Lista(await PanelDelCliente().MisObras()).Single();

            var documentos = ((System.Collections.IEnumerable)Valor(obra, "Documentos")).Cast<object>().ToList();
            Assert.Equal("contrato.pdf", Valor(documentos.Single(), "Nombre"));
            Assert.Equal("Contrato", Valor(documentos.Single(), "Clasificacion"));
        }

        [Fact]
        public async Task ElCliente_descargaSusDocumentosVisibles_peroNoLosInternosNiLosAjenos()
        {
            var (_, visible, _) = await Subir(Pdf("contrato.pdf"), visible: true);
            var (_, interno, _) = await Subir(Pdf("interno.pdf"), visible: false);

            Assert.IsType<PhysicalFileResult>(await PanelDelCliente().DescargarDocumento(visible!.Id));
            Assert.IsType<NotFoundObjectResult>(await PanelDelCliente().DescargarDocumento(interno!.Id));

            var otro = new Cliente { Nombre = "Otra Empresa", Documento = "20999999999", TipoDocumento = "RUC" };
            Db.Clientes.Add(otro);
            await Db.SaveChangesAsync();
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Cliente);
            var ajeno = new Usuario { Nombres = "Otra Empresa", Correo = "otra@empresa.pe", RolId = rol.Id, PasswordHash = "x", ClienteId = otro.Id };
            Db.Usuarios.Add(ajeno);
            await Db.SaveChangesAsync();

            Assert.IsType<NotFoundObjectResult>(await PanelDelCliente(ajeno.Id).DescargarDocumento(visible.Id));
            Assert.Empty(Lista(await PanelDelCliente(ajeno.Id).MisObras()));
        }
    }
}
