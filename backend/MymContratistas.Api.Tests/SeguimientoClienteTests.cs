using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales y de seguridad de HU20: seguimiento de obra para el cliente (HU20-T7)
    public sealed class SeguimientoClienteTests : PruebaConBaseDeDatos
    {
        private readonly int _usuarioId, _obraId, _hitoId, _fotoId;
        private readonly int _usuarioAjenoId, _obraAjenaId, _hitoAjenoId, _fotoAjenaId, _docAjenoId;

        public SeguimientoClienteTests()
        {
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Cliente);

            (int usuario, int obra, int hito, int foto) CrearCliente(string nombre, string documento, string obraNombre)
            {
                var cliente = new Cliente { Nombre = nombre, Documento = documento, TipoDocumento = "RUC" };
                Db.Clientes.Add(cliente);
                Db.SaveChanges();
                var obra = new Obra
                {
                    Nombre = obraNombre, Cliente = nombre, ClienteId = cliente.Id, Ubicacion = "Lima", AvancePorcentaje = 45,
                    FechaInicio = new DateOnly(2026, 3, 2), FechaFinEstimada = new DateOnly(2026, 11, 28)
                };
                Db.Obras.Add(obra);
                var usuario = new Usuario { Nombres = nombre, Correo = $"{documento}@cliente.pe", RolId = rol.Id, PasswordHash = "x", ClienteId = cliente.Id };
                Db.Usuarios.Add(usuario);
                Db.SaveChanges();

                var hito = new Hito { ObraId = obra.Id, Nombre = "Estructura " + nombre, Orden = 1, Peso = 100, Estado = "En proceso", AvancePorcentaje = 55 };
                Db.Hitos.Add(hito);
                Db.SaveChanges();

                var avance = new AvanceHito { HitoId = hito.Id, Porcentaje = 55, Descripcion = "Vaciado de losa " + nombre, Fecha = DateTime.UtcNow, Responsable = "Marco Zapata" };
                var archivo = Guid.NewGuid().ToString("N") + ".png";
                avance.Fotos.Add(new FotoAvance { Archivo = archivo, NombreOriginal = "losa.png" });
                Db.AvancesHito.Add(avance);
                Db.SaveChanges();

                Directory.CreateDirectory(Path.Combine(Carpeta, "Uploads", "avances"));
                File.WriteAllBytes(Path.Combine(Carpeta, "Uploads", "avances", archivo), Archivos.Png());
                return (usuario.Id, obra.Id, hito.Id, avance.Fotos[0].Id);
            }

            (_usuarioId, _obraId, _hitoId, _fotoId) = CrearCliente("Inversiones Del Río", "20512345678", "Los Cedros");
            (_usuarioAjenoId, _obraAjenaId, _hitoAjenoId, _fotoAjenaId) = CrearCliente("Otra Empresa", "20999999999", "Torre Ajena");

            var doc = new Documento { ObraId = _obraAjenaId, Nombre = "contrato-ajeno.pdf", Archivo = "x.pdf", VisibleCliente = true, FechaCarga = "01/01/2026" };
            Db.Documentos.Add(doc);
            Db.SaveChanges();
            _docAjenoId = doc.Id;
        }

        private ClienteController Panel(int? usuarioId = null, string nombre = "Inversiones Del Río") => new(Db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, (usuarioId ?? _usuarioId).ToString()),
                        new Claim(ClaimTypes.Name, nombre)
                    }, "Prueba"))
                }
            }
        };

        private static void AssertSinPermiso(IActionResult r)
        {
            Assert.Equal(403, Estado(r));
            Assert.Equal(ClienteController.MensajeSinPermiso, (string)((ObjectResult)r).Value!);
        }

        private static List<object> Elementos(object origen, string propiedad) =>
            ((IEnumerable)Valor(origen, propiedad)).Cast<object>().ToList();

        // ---- Mis Proyectos (T1) ----

        [Fact]
        public async Task MisProyectos_muestraNombreAvanceYFechas_soloDeSusObras()
        {
            var obra = Assert.Single(Lista(await Panel().MisObras()));

            Assert.Equal("Los Cedros", Valor(obra, "Nombre"));
            Assert.Equal(45, Valor(obra, "AvancePorcentaje"));
            Assert.Equal(new DateOnly(2026, 3, 2), Valor(obra, "FechaInicio"));
            Assert.Equal(new DateOnly(2026, 11, 28), Valor(obra, "FechaFinEstimada"));
        }

        // ---- Seguimiento: avance general y hitos (T2, T3) ----

        [Fact]
        public async Task Seguimiento_devuelveAvanceGeneral_yHitosOrdenadosConEstadoYFechas()
        {
            Db.Hitos.Add(new Hito
            {
                ObraId = _obraId, Nombre = "Cimentación", Orden = 0, Peso = 0, Estado = "Completado", AvancePorcentaje = 100,
                FechaPlanificada = new DateOnly(2026, 5, 5), FechaReal = new DateOnly(2026, 5, 9)
            });
            await Db.SaveChangesAsync();

            var r = await Panel().SeguimientoDeObra(_obraId);

            Assert.Equal(200, Estado(r));
            var obra = ((ObjectResult)r).Value!;
            Assert.Equal(45, Valor(obra, "AvancePorcentaje"));
            var hitos = Elementos(obra, "Hitos");
            Assert.Equal(new[] { "Cimentación", "Estructura Inversiones Del Río" }, hitos.Select(h => (string)Valor(h, "Nombre")));
            Assert.Equal("Completado", Valor(hitos[0], "Estado"));
            Assert.Equal(new DateOnly(2026, 5, 5), Valor(hitos[0], "FechaPlanificada"));
            Assert.Equal(new DateOnly(2026, 5, 9), Valor(hitos[0], "FechaReal"));
            Assert.Equal("En proceso", Valor(hitos[1], "Estado"));
        }

        [Fact]
        public async Task Seguimiento_reconoceObrasAnterioresSinFichaPorElNombreDelCliente()
        {
            var anterior = new Obra { Nombre = "Obra anterior", Cliente = "Cliente Antiguo" };
            Db.Obras.Add(anterior);
            await Db.SaveChangesAsync();

            Assert.Equal(200, Estado(await Panel(nombre: "Cliente Antiguo").SeguimientoDeObra(anterior.Id)));
        }

        // ---- Detalle de hito: descripción y fotografías (T4) ----

        [Fact]
        public async Task DetalleDeHito_devuelveDescripcionYFotografiasDeCadaAvance()
        {
            var r = await Panel().DetalleDeHito(_hitoId);

            Assert.Equal(200, Estado(r));
            var avance = Assert.Single(Elementos(((ObjectResult)r).Value!, "avances"));
            Assert.Equal("Vaciado de losa Inversiones Del Río", Valor(avance, "Descripcion"));
            Assert.Equal(55, Valor(avance, "Porcentaje"));
            Assert.Equal(_fotoId, Valor(Assert.Single(Elementos(avance, "Fotos")), "Id"));
        }

        [Fact]
        public async Task DetalleDeHito_noExponeDatosInternos()
        {
            var r = await Panel().DetalleDeHito(_hitoId);
            var avance = Assert.Single(Elementos(((ObjectResult)r).Value!, "avances"));

            Assert.Null(avance.GetType().GetProperty("Responsable"));
            Assert.Null(Assert.Single(Elementos(avance, "Fotos")).GetType().GetProperty("Archivo"));
        }

        [Fact]
        public async Task Foto_delPropioProyecto_seDescarga()
        {
            var archivo = Assert.IsType<PhysicalFileResult>(await Panel().VerFotoDeAvance(_fotoId));

            Assert.Equal("image/png", archivo.ContentType);
        }

        // ---- Documentos (T5) ----

        [Fact]
        public async Task Seguimiento_soloListaLosDocumentosVisiblesParaElCliente()
        {
            Db.Documentos.AddRange(
                new Documento { ObraId = _obraId, Nombre = "plano.pdf", Archivo = "a.pdf", VisibleCliente = true, FechaCarga = "01/01/2026" },
                new Documento { ObraId = _obraId, Nombre = "costos-internos.pdf", Archivo = "b.pdf", VisibleCliente = false, FechaCarga = "01/01/2026" });
            await Db.SaveChangesAsync();

            var obra = ((ObjectResult)await Panel().SeguimientoDeObra(_obraId)).Value!;

            Assert.Equal("plano.pdf", Valor(Assert.Single(Elementos(obra, "Documentos")), "Nombre"));
        }

        [Fact]
        public async Task Documento_deUsoInternoEnElPropioProyecto_nuncaSeEntrega()
        {
            var interno = new Documento { ObraId = _obraId, Nombre = "interno.pdf", Archivo = "c.pdf", VisibleCliente = false, FechaCarga = "01/01/2026" };
            Db.Documentos.Add(interno);
            await Db.SaveChangesAsync();

            Assert.IsType<NotFoundObjectResult>(await Panel().DescargarDocumento(interno.Id));
        }

        // ---- Restricción de acceso por recurso (T6, T7) ----

        [Fact]
        public async Task Seguimiento_deUnProyectoAjeno_devuelve403ConMensajeDePermisos()
        {
            AssertSinPermiso(await Panel().SeguimientoDeObra(_obraAjenaId));
        }

        [Fact]
        public async Task Seguimiento_deUnProyectoInexistente_respondeIgualQueUnoAjeno()
        {
            // Con un 404 un cliente podría descubrir qué identificadores existen
            AssertSinPermiso(await Panel().SeguimientoDeObra(99999));
        }

        [Fact]
        public async Task DetalleDeHito_deUnProyectoAjeno_oInexistente_devuelve403()
        {
            AssertSinPermiso(await Panel().DetalleDeHito(_hitoAjenoId));
            AssertSinPermiso(await Panel().DetalleDeHito(99999));
        }

        [Fact]
        public async Task Foto_deUnProyectoAjeno_oInexistente_devuelve403()
        {
            AssertSinPermiso(await Panel().VerFotoDeAvance(_fotoAjenaId));
            AssertSinPermiso(await Panel().VerFotoDeAvance(99999));
        }

        [Fact]
        public async Task Documento_deUnProyectoAjeno_devuelve403()
        {
            AssertSinPermiso(await Panel().DescargarDocumento(_docAjenoId));
        }

        [Fact]
        public async Task CadaClienteVeSoloLoSuyo()
        {
            var ajeno = Panel(_usuarioAjenoId, "Otra Empresa");

            Assert.Equal("Torre Ajena", Valor(Assert.Single(Lista(await ajeno.MisObras())), "Nombre"));
            Assert.Equal(200, Estado(await ajeno.SeguimientoDeObra(_obraAjenaId)));
            AssertSinPermiso(await ajeno.SeguimientoDeObra(_obraId));
        }

        [Fact]
        public async Task UnaCuentaSinFichaNiObras_noVeNada()
        {
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Cliente);
            var huerfano = new Usuario { Nombres = "Sin Ficha", Correo = "sin@ficha.pe", RolId = rol.Id, PasswordHash = "x" };
            Db.Usuarios.Add(huerfano);
            await Db.SaveChangesAsync();
            var panel = Panel(huerfano.Id, "Sin Ficha");

            Assert.Empty(Lista(await panel.MisObras()));
            AssertSinPermiso(await panel.SeguimientoDeObra(_obraId));
            AssertSinPermiso(await panel.DetalleDeHito(_hitoId));
            AssertSinPermiso(await panel.VerFotoDeAvance(_fotoId));
        }

        [Fact]
        public void ElPanelDelCliente_exigeSesionConRolCliente()
        {
            var autorizacion = typeof(ClienteController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(autorizacion);
            Assert.Equal(NombresRol.Cliente, autorizacion!.Roles);
            Assert.DoesNotContain(typeof(ClienteController).GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);
        }
    }
}
