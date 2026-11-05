using System.Collections;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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
    // Pruebas funcionales de HU22: comentarios del cliente en un hito, respuesta del personal y avisos por correo (HU22-T6)
    public sealed class ComentariosHitoTests : PruebaConBaseDeDatos
    {
        private readonly CorreoDePrueba _correo = new();
        private readonly int _clienteUsuarioId, _hitoId, _hitoAjenoId, _supervisorId;
        private const string CorreoCliente = "contacto@delrio.pe";
        private const string CorreoGerenteA = "gerente.a@mym.pe";
        private const string CorreoGerenteB = "gerente.b@mym.pe";

        public ComentariosHitoTests()
        {
            var roles = Db.Roles.ToDictionary(r => r.Nombre, r => r.Id);
            Usuario Cuenta(string nombres, string apellidos, string correo, string rol, string estado = "Activo", int? clienteId = null) =>
                new() { Nombres = nombres, Apellidos = apellidos, Correo = correo, RolId = roles[rol], PasswordHash = "x", Estado = estado, ClienteId = clienteId };

            var cliente = new Cliente { Nombre = "Inversiones Del Río", Documento = "20512345678", TipoDocumento = "RUC", Correo = CorreoCliente };
            var otro = new Cliente { Nombre = "Otra Empresa", Documento = "20999999999", TipoDocumento = "RUC" };
            Db.Clientes.AddRange(cliente, otro);
            Db.SaveChanges();

            var obra = new Obra { Nombre = "Los Cedros", Cliente = cliente.Nombre, ClienteId = cliente.Id };
            var obraAjena = new Obra { Nombre = "Torre Ajena", Cliente = otro.Nombre, ClienteId = otro.Id };
            Db.Obras.AddRange(obra, obraAjena);

            var clienteUsuario = Cuenta("Inversiones", "Del Río", CorreoCliente, NombresRol.Cliente, clienteId: cliente.Id);
            var supervisor = Cuenta("Marco", "Zapata", "marco@mym.pe", NombresRol.Supervisor);
            Db.Usuarios.AddRange(clienteUsuario, supervisor,
                Cuenta("Gerente", "Uno", CorreoGerenteA, NombresRol.Gerente),
                Cuenta("Gerente", "Dos", CorreoGerenteB, NombresRol.Gerente),
                Cuenta("Gerente", "Inactivo", "inactivo@mym.pe", NombresRol.Gerente, estado: "Inactivo"),
                Cuenta("Admin", "General", "admin@mym.pe", NombresRol.Administrador));
            Db.SaveChanges();
            _clienteUsuarioId = clienteUsuario.Id;
            _supervisorId = supervisor.Id;

            var hito = new Hito { ObraId = obra.Id, Nombre = "Estructura", Orden = 1, Peso = 100, Estado = "En proceso", AvancePorcentaje = 50 };
            var hitoAjeno = new Hito { ObraId = obraAjena.Id, Nombre = "Cimentación ajena", Orden = 1, Peso = 100 };
            Db.Hitos.AddRange(hito, hitoAjeno);
            Db.SaveChanges();
            _hitoId = hito.Id;
            _hitoAjenoId = hitoAjeno.Id;
        }

        private static ControllerContext ComoUsuario(int usuarioId, string nombre) => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
                    new Claim(ClaimTypes.Name, nombre)
                }, "Prueba"))
            }
        };

        private ClienteController Panel(CorreoDePrueba? correo = null) =>
            new(Db, Notificaciones.Con(Db, correo ?? _correo)) { ControllerContext = ComoUsuario(_clienteUsuarioId, "Inversiones Del Río") };

        private ObrasController Administracion(CorreoDePrueba? correo = null)
        {
            var invitacion = new InvitacionService(_correo, new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            return new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, Notificaciones.Con(Db, correo ?? _correo))
            {
                ControllerContext = ComoUsuario(_supervisorId, "Marco Zapata")
            };
        }

        private async Task<IActionResult> Comentar(string? texto, int? hitoId = null, CorreoDePrueba? correo = null) =>
            await Panel(correo).Comentar(hitoId ?? _hitoId, new ClienteController.ComentarioDto { Texto = texto });

        private async Task<ComentarioHito> ComentarioDelCliente(string texto = "¿Siguen dentro del plazo?")
        {
            var c = new ComentarioHito { HitoId = _hitoId, Texto = texto, Fecha = DateTime.UtcNow, AutorUsuarioId = _clienteUsuarioId, AutorNombre = "Inversiones Del Río" };
            Db.ComentariosHito.Add(c);
            await Db.SaveChangesAsync();
            return c;
        }

        private async Task<int> Guardados()
        {
            await using var db = OtroContexto();
            return await db.ComentariosHito.CountAsync();
        }

        private static List<object> Elementos(IActionResult r, string propiedad) =>
            ((IEnumerable)Valor(((ObjectResult)r).Value!, propiedad)).Cast<object>().ToList();

        // ---- Envío del comentario del cliente (T3) ----

        [Fact]
        public async Task Comentar_guardaElComentarioConAutorFechaYConfirmacion()
        {
            var antes = DateTime.UtcNow.AddSeconds(-5);

            var r = await Comentar("  ¿Se vació completa la losa?  ");

            Assert.Equal(200, Estado(r));
            Assert.Contains("Comentario enviado correctamente", Dato<string>(r, "mensaje"));
            await using var db = OtroContexto();
            var c = await db.ComentariosHito.SingleAsync();
            Assert.Equal("¿Se vació completa la losa?", c.Texto);
            Assert.Equal("Inversiones Del Río", c.AutorNombre);
            Assert.Equal(_clienteUsuarioId, c.AutorUsuarioId);
            Assert.False(c.EsPersonal);
            Assert.Null(c.ComentarioPadreId);
            Assert.Equal(_hitoId, c.HitoId);
            Assert.InRange(c.Fecha, antes, DateTime.UtcNow.AddSeconds(5));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public async Task Comentar_conTextoVacio_seRechazaYNoSeGuardaNiSeAvisa(string? texto)
        {
            var r = await Comentar(texto);

            Assert.Equal(400, Estado(r));
            Assert.Equal("Escriba el comentario antes de enviarlo.", (string)((ObjectResult)r).Value!);
            Assert.Equal(0, await Guardados());
            Assert.Empty(_correo.Enviados);
        }

        [Fact]
        public async Task Comentar_respetaElLimiteDe500Caracteres()
        {
            Assert.Equal(200, Estado(await Comentar(new string('a', 500))));

            var r = await Comentar(new string('a', 501));

            Assert.Equal(400, Estado(r));
            Assert.Contains("500", (string)((ObjectResult)r).Value!);
            Assert.Equal(1, await Guardados());
        }

        [Fact]
        public async Task Comentar_sinCuerpo_devuelve400()
        {
            Assert.Equal(400, Estado(await Panel().Comentar(_hitoId, null)));
            Assert.Equal(0, await Guardados());
        }

        [Fact]
        public async Task LaConversacion_seMuestraEnOrdenCronologico()
        {
            var tercero = new ComentarioHito { HitoId = _hitoId, Texto = "tercero", Fecha = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc), AutorUsuarioId = 1, AutorNombre = "A" };
            var primero = new ComentarioHito { HitoId = _hitoId, Texto = "primero", Fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), AutorUsuarioId = 1, AutorNombre = "A" };
            var segundo = new ComentarioHito { HitoId = _hitoId, Texto = "segundo", Fecha = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), AutorUsuarioId = 1, AutorNombre = "A" };
            Db.ComentariosHito.AddRange(tercero, primero, segundo);
            await Db.SaveChangesAsync();

            var delCliente = await Panel().DetalleDeHito(_hitoId);
            var delPersonal = await Administracion().ComentariosDelHito(_hitoId);

            var esperado = new[] { "primero", "segundo", "tercero" };
            Assert.Equal(esperado, Elementos(delCliente, "comentarios").Select(c => (string)Valor(c, "texto")));
            Assert.Equal(esperado, Lista(delPersonal).Select(c => (string)Valor(c, "texto")));
        }

        // ---- Aviso al Gerente de Operaciones (T4) ----

        [Fact]
        public async Task Comentar_avisaPorCorreoSoloALosGerentesActivos()
        {
            var r = await Comentar("¿Hay avance en el tercer nivel?");

            Assert.Contains("fue notificado", Dato<string>(r, "mensaje"));
            Assert.Equal(new[] { CorreoGerenteA, CorreoGerenteB }.OrderBy(x => x), _correo.Enviados.Select(e => e.Para).OrderBy(x => x));
            var correo = _correo.Enviados[0];
            Assert.Contains("Estructura", correo.Asunto);
            Assert.Contains("Los Cedros", correo.Asunto);
            Assert.Contains(WebUtility.HtmlEncode("¿Hay avance en el tercer nivel?"), correo.Cuerpo);
            Assert.Contains(WebUtility.HtmlEncode("Inversiones Del Río"), correo.Cuerpo);
        }

        [Fact]
        public async Task ElCorreo_escapaElTextoDelClienteYConservaLosSaltosDeLinea()
        {
            await Comentar("<script>alert(1)</script>\nsegunda línea");

            var cuerpo = _correo.Enviados[0].Cuerpo;
            Assert.DoesNotContain("<script>", cuerpo);
            Assert.Contains("&lt;script&gt;", cuerpo);
            Assert.Contains("<br>segunda l", cuerpo);
        }

        [Fact]
        public async Task SiFallaElCorreo_elComentarioQuedaGuardado_yLosOtrosGerentesSonAvisados()
        {
            var correo = new CorreoDePrueba();
            correo.Falla.Add(CorreoGerenteA);

            var r = await Comentar("Consulta importante", correo: correo);

            Assert.Equal(200, Estado(r));
            Assert.Equal(1, await Guardados());
            Assert.Equal(CorreoGerenteB, Assert.Single(correo.Enviados).Para);
            Assert.DoesNotContain("fue notificado", Dato<string>(r, "mensaje"));
        }

        [Fact]
        public async Task SinGerenteActivo_elComentarioSeGuardaSinEnviarCorreos()
        {
            foreach (var g in Db.Usuarios.Where(u => u.Rol.Nombre == NombresRol.Gerente)) g.Estado = "Inactivo";
            await Db.SaveChangesAsync();

            var r = await Comentar("Consulta");

            Assert.Equal(200, Estado(r));
            Assert.Equal(1, await Guardados());
            Assert.Empty(_correo.Enviados);
            Assert.DoesNotContain("fue notificado", Dato<string>(r, "mensaje"));
        }

        // ---- Respuesta del personal (T5) ----

        [Fact]
        public async Task Responder_guardaLaRespuestaVinculadaAlComentario_yElClienteLaVe()
        {
            var comentario = await ComentarioDelCliente();

            var r = await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = " Sí, vamos en plazo. " });

            Assert.Equal(200, Estado(r));
            await using (var db = OtroContexto())
            {
                var respuesta = await db.ComentariosHito.SingleAsync(c => c.EsPersonal);
                Assert.Equal("Sí, vamos en plazo.", respuesta.Texto);
                Assert.Equal("Marco Zapata", respuesta.AutorNombre);
                Assert.Equal(comentario.Id, respuesta.ComentarioPadreId);
                Assert.Equal(_hitoId, respuesta.HitoId);
            }
            var visto = Elementos(await Panel().DetalleDeHito(_hitoId), "comentarios");
            Assert.Equal(2, visto.Count);
            Assert.True((bool)Valor(visto[1], "esPersonal"));
            Assert.Equal(comentario.Id, Valor(visto[1], "respondeA"));
            Assert.Equal("Marco Zapata", Valor(visto[1], "autor"));
        }

        [Fact]
        public async Task Responder_avisaPorCorreoAlClienteQueComento()
        {
            var comentario = await ComentarioDelCliente();

            var r = await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = "Todo en orden <b>" });

            Assert.Contains("Se notificó al cliente por correo", Dato<string>(r, "mensaje"));
            var correo = Assert.Single(_correo.Enviados);
            Assert.Equal(CorreoCliente, correo.Para);
            Assert.Contains("Estructura", correo.Asunto);
            Assert.Contains("Todo en orden &lt;b&gt;", correo.Cuerpo);
            Assert.Contains("Marco Zapata", correo.Cuerpo);
        }

        [Fact]
        public async Task Responder_siFallaElCorreo_larespuestaQuedaGuardada()
        {
            var comentario = await ComentarioDelCliente();
            var correo = new CorreoDePrueba();
            correo.Falla.Add(CorreoCliente);

            var r = await Administracion(correo).ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = "Respuesta" });

            Assert.Equal(200, Estado(r));
            Assert.Contains("No se pudo enviar el correo", Dato<string>(r, "mensaje"));
            Assert.Equal(2, await Guardados());
        }

        [Fact]
        public async Task Responder_siLaCuentaYaNoExiste_avisaAlCorreoDeLaFichaDelCliente()
        {
            var huerfano = await ComentarioDelCliente();
            huerfano.AutorUsuarioId = 99999;
            await Db.SaveChangesAsync();

            await Administracion().ResponderComentario(huerfano.Id, new ObrasController.RespuestaDto { Texto = "Respuesta" });

            Assert.Equal(CorreoCliente, Assert.Single(_correo.Enviados).Para);
        }

        [Fact]
        public async Task Responder_sinCorreoDelCliente_guardaYLoAvisa()
        {
            var comentario = await ComentarioDelCliente();
            comentario.AutorUsuarioId = 99999;
            (await Db.Clientes.FirstAsync(c => c.Documento == "20512345678")).Correo = null;
            await Db.SaveChangesAsync();

            var r = await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = "Respuesta" });

            Assert.Equal(200, Estado(r));
            Assert.Contains("no tiene correo registrado", Dato<string>(r, "mensaje"));
            Assert.Empty(_correo.Enviados);
            Assert.Equal(2, await Guardados());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task Responder_conTextoVacio_seRechaza(string? texto)
        {
            var comentario = await ComentarioDelCliente();

            Assert.Equal(400, Estado(await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = texto })));
            Assert.Equal(1, await Guardados());
            Assert.Empty(_correo.Enviados);
        }

        [Fact]
        public async Task Responder_respetaElLimiteDe500Caracteres()
        {
            var comentario = await ComentarioDelCliente();

            Assert.Equal(400, Estado(await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = new string('a', 501) })));
            Assert.Equal(200, Estado(await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = new string('a', 500) })));
        }

        [Fact]
        public async Task Responder_aUnComentarioInexistente_oAUnaRespuesta_seRechaza()
        {
            Assert.IsType<NotFoundObjectResult>(await Administracion().ResponderComentario(99999, new ObrasController.RespuestaDto { Texto = "x" }));

            var comentario = await ComentarioDelCliente();
            await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = "Primera respuesta" });
            var respuestaId = (await Db.ComentariosHito.SingleAsync(c => c.EsPersonal)).Id;

            Assert.Equal(400, Estado(await Administracion().ResponderComentario(respuestaId, new ObrasController.RespuestaDto { Texto = "Respuesta a respuesta" })));
        }

        [Fact]
        public async Task ComentariosDelHito_enAdministracion_conHitoInexistente_devuelve404()
        {
            Assert.IsType<NotFoundObjectResult>(await Administracion().ComentariosDelHito(99999));
        }

        // ---- Seguridad y privacidad ----

        [Fact]
        public async Task Comentar_enElHitoDeOtroCliente_oInexistente_devuelve403ySinEfectos()
        {
            foreach (var id in new[] { _hitoAjenoId, 99999 })
            {
                var r = await Comentar("Intento", id);
                Assert.Equal(403, Estado(r));
                Assert.Equal(ClienteController.MensajeSinPermiso, (string)((ObjectResult)r).Value!);
            }
            Assert.Equal(0, await Guardados());
            Assert.Empty(_correo.Enviados);
        }

        [Fact]
        public async Task LosComentariosDeUnHitoAjeno_noSeVenDesdeOtraCuenta()
        {
            Db.ComentariosHito.Add(new ComentarioHito { HitoId = _hitoAjenoId, Texto = "secreto de otro cliente", Fecha = DateTime.UtcNow, AutorUsuarioId = 1, AutorNombre = "Otro" });
            await Db.SaveChangesAsync();

            var r = await Panel().DetalleDeHito(_hitoAjenoId);

            Assert.Equal(403, Estado(r));
            Assert.DoesNotContain("secreto", JsonSerializer.Serialize(((ObjectResult)r).Value));
        }

        [Fact]
        public async Task ElCliente_noRecibeIdentificadoresInternosDeCuenta()
        {
            var comentario = await ComentarioDelCliente();
            await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = "Respuesta" });

            var json = JsonSerializer.Serialize(((ObjectResult)await Panel().DetalleDeHito(_hitoId)).Value);

            Assert.DoesNotContain("autorUsuarioId", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("marco@mym.pe", json);
        }

        [Fact]
        public void LasRespuestas_soloSePuedenDarDesdeElPersonal_yLosComentariosDesdeElCliente()
        {
            var personal = typeof(ObrasController).GetCustomAttribute<AuthorizeAttribute>()!;
            var cliente = typeof(ClienteController).GetCustomAttribute<AuthorizeAttribute>()!;

            Assert.Equal(NombresRol.Personal, personal.Roles);
            Assert.DoesNotContain(NombresRol.Cliente, personal.Roles!.Split(','));
            Assert.Equal(NombresRol.Cliente, cliente.Roles);
            Assert.Null(typeof(ObrasController).GetMethod(nameof(ObrasController.ResponderComentario))!.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(typeof(ClienteController).GetMethod(nameof(ClienteController.Comentar))!.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        // ---- Integridad ----

        [Fact]
        public async Task UnHitoConComentarios_noSePuedeEliminar()
        {
            var hitoSinAvance = new Hito { ObraId = (await Db.Hitos.FindAsync(_hitoId))!.ObraId, Nombre = "Pendiente", Orden = 2, Peso = 0 };
            Db.Hitos.Add(hitoSinAvance);
            await Db.SaveChangesAsync();
            Db.ComentariosHito.Add(new ComentarioHito { HitoId = hitoSinAvance.Id, Texto = "Consulta", Fecha = DateTime.UtcNow, AutorUsuarioId = _clienteUsuarioId, AutorNombre = "Cliente" });
            await Db.SaveChangesAsync();

            var r = await Administracion().EliminarHito(hitoSinAvance.Id);

            Assert.Equal(400, Estado(r));
            Assert.Contains("comentarios", (string)((ObjectResult)r).Value!);
            Assert.NotNull(await Db.Hitos.FindAsync(hitoSinAvance.Id));
        }

        [Fact]
        public async Task LaTabla_admiteHilosDeRespuesta_yBorraEnCascadaAlEliminarElHito()
        {
            var comentario = await ComentarioDelCliente();
            await Administracion().ResponderComentario(comentario.Id, new ObrasController.RespuestaDto { Texto = "Respuesta" });

            Db.Hitos.Remove((await Db.Hitos.FindAsync(_hitoId))!);
            await Db.SaveChangesAsync();

            Assert.Equal(0, await Guardados());
        }
    }
}
