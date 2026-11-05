using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas de HT-05: servicio unificado de notificaciones, registro de envíos, duplicados y avisos en la app
    public sealed class NotificacionesTests : PruebaConBaseDeDatos
    {
        private readonly CorreoDePrueba _correo = new();
        private readonly NotificacionService _servicio;
        private readonly int _gerenteA, _gerenteB, _clienteUsuario, _supervisor;
        private readonly Obra _obra;
        private readonly Hito _hito;

        public NotificacionesTests()
        {
            _servicio = Notificaciones.Nucleo(Db, _correo);
            var roles = Db.Roles.ToDictionary(r => r.Nombre, r => r.Id);
            var cliente = new Cliente { Nombre = "Del Río", Documento = "20512345678", TipoDocumento = "RUC", Correo = "cliente@delrio.pe" };
            Db.Clientes.Add(cliente);
            Db.SaveChanges();
            Usuario Cuenta(string correo, string rol, int? clienteId = null) =>
                new() { Nombres = correo, Correo = correo, RolId = roles[rol], PasswordHash = "x", ClienteId = clienteId };
            var a = Cuenta("gerente.a@mym.pe", NombresRol.Gerente);
            var b = Cuenta("gerente.b@mym.pe", NombresRol.Gerente);
            var c = Cuenta("cliente@delrio.pe", NombresRol.Cliente, cliente.Id);
            var s = Cuenta("supervisor@mym.pe", NombresRol.Supervisor);
            Db.Usuarios.AddRange(a, b, c, s);
            _obra = new Obra { Nombre = "Los Cedros", Cliente = cliente.Nombre, ClienteId = cliente.Id };
            Db.Obras.Add(_obra);
            Db.SaveChanges();
            _hito = new Hito { ObraId = _obra.Id, Nombre = "Estructura", Orden = 1, Peso = 100 };
            Db.Hitos.Add(_hito);
            Db.SaveChanges();
            (_gerenteA, _gerenteB, _clienteUsuario, _supervisor) = (a.Id, b.Id, c.Id, s.Id);
        }

        private static SolicitudNotificacion Aviso(string? clave = "evento:1", string tipo = Notificacion.TipoComentarioCliente) =>
            new(tipo, "Nuevo comentario", "Texto del aviso", 1, 2, clave);

        private async Task<List<Notificacion>> Registros(Func<Notificacion, bool>? filtro = null)
        {
            await using var db = OtroContexto();
            return (await db.Notificaciones.OrderBy(n => n.Id).ToListAsync()).Where(n => filtro?.Invoke(n) ?? true).ToList();
        }

        private static ControllerContext ComoUsuario(int id) => new()
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "Prueba")) }
        };

        private NotificacionesController Controlador(int usuarioId) => new(Db) { ControllerContext = ComoUsuario(usuarioId) };

        private static List<object> Elementos(IActionResult r, string propiedad = "items") =>
            ((IEnumerable)Valor(((ObjectResult)r).Value!, propiedad)).Cast<object>().ToList();

        private static object Info(IActionResult r) => Valor(((ObjectResult)r).Value!, "paginacion");

        // ---- Registro de envíos ----

        [Fact]
        public async Task Correo_seRegistraConDestinatarioYResultado_sinGuardarElCuerpo()
        {
            var r = await _servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", _gerenteA, "<p>contenido confidencial token=abc123</p>");

            Assert.Equal(ResultadoNotificacion.Enviado, r);
            Assert.Single(_correo.Enviados);
            var n = Assert.Single(await Registros());
            Assert.Equal(Notificacion.CanalCorreo, n.Canal);
            Assert.Equal(Notificacion.Enviado, n.Estado);
            Assert.Equal("gerente.a@mym.pe", n.Destinatario);
            Assert.Equal("Nuevo comentario", n.Titulo);
            Assert.Equal(1, n.Intentos);
            Assert.NotNull(n.EnviadoEn);
            Assert.DoesNotContain("abc123", string.Join("|", n.Titulo, n.Mensaje, n.Destinatario, n.Error));
        }

        [Fact]
        public async Task Correo_sinDestinatario_noSeEnvia_niSeRegistra()
        {
            Assert.Equal(ResultadoNotificacion.SinDestino, await _servicio.EnviarCorreoAsync(Aviso(), "   ", null, "x"));
            Assert.Equal(ResultadoNotificacion.SinDestino, await _servicio.EnviarCorreoAsync(Aviso(), null, null, "x"));
            Assert.Empty(_correo.Enviados);
            Assert.Empty(await Registros());
        }

        // ---- Control de duplicados ----

        [Fact]
        public async Task ElMismoAviso_alMismoDestinatario_noSeEnviaDosVeces()
        {
            var primero = await _servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", _gerenteA, "x");
            var segundo = await _servicio.EnviarCorreoAsync(Aviso(), "GERENTE.A@mym.pe", _gerenteA, "x");

            Assert.Equal(ResultadoNotificacion.Enviado, primero);
            Assert.Equal(ResultadoNotificacion.Duplicado, segundo);
            Assert.Single(_correo.Enviados);
            Assert.Single(await Registros());
        }

        [Fact]
        public async Task ElMismoEvento_siEnviaACadaDestinatarioDistinto_yPorCadaCanal()
        {
            await _servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", _gerenteA, "x");
            await _servicio.EnviarCorreoAsync(Aviso(), "gerente.b@mym.pe", _gerenteB, "x");
            var app = await _servicio.AvisarEnAppAsync(Aviso(), _gerenteA);

            Assert.Equal(ResultadoNotificacion.Enviado, app);
            Assert.Equal(2, _correo.Enviados.Count);
            Assert.Equal(3, (await Registros()).Count);
        }

        [Fact]
        public async Task UnEventoDistinto_oSinClave_sinControlDeDuplicados()
        {
            await _servicio.EnviarCorreoAsync(Aviso("evento:1"), "gerente.a@mym.pe", null, "x");
            await _servicio.EnviarCorreoAsync(Aviso("evento:2"), "gerente.a@mym.pe", null, "x");
            await _servicio.EnviarCorreoAsync(Aviso(null), "gerente.a@mym.pe", null, "x");
            await _servicio.EnviarCorreoAsync(Aviso(null), "gerente.a@mym.pe", null, "x");

            Assert.Equal(4, _correo.Enviados.Count);
        }

        [Fact]
        public async Task LaMismaClave_enOtroTipoDeAviso_noSeConfunde()
        {
            await _servicio.EnviarCorreoAsync(Aviso("1", Notificacion.TipoComentarioCliente), "gerente.a@mym.pe", null, "x");
            var r = await _servicio.EnviarCorreoAsync(Aviso("1", Notificacion.TipoRespuestaPersonal), "gerente.a@mym.pe", null, "x");

            Assert.Equal(ResultadoNotificacion.Enviado, r);
        }

        [Fact]
        public void LaBaseDeDatos_rechazaDuplicadosAunqueFalleLaComprobacionDelServicio()
        {
            var clave = "ComentarioCliente|evento:1|Correo|gerente.a@mym.pe";
            Db.Notificaciones.Add(new Notificacion { Tipo = "x", Canal = "Correo", Titulo = "t", ClaveDeduplicacion = clave, Fecha = DateTime.UtcNow });
            Db.SaveChanges();
            Db.Notificaciones.Add(new Notificacion { Tipo = "x", Canal = "Correo", Titulo = "t", ClaveDeduplicacion = clave, Fecha = DateTime.UtcNow });

            Assert.Throws<DbUpdateException>(() => Db.SaveChanges());
        }

        // ---- Fallos y reintentos ----

        [Fact]
        public async Task SiElCorreoFalla_queDaRegistradoComoFallido_yElReintentoReutilizaElRegistro()
        {
            _correo.Falla.Add("gerente.a@mym.pe");

            var fallido = await _servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", null, "x");

            Assert.Equal(ResultadoNotificacion.Fallido, fallido);
            var n = Assert.Single(await Registros());
            Assert.Equal(Notificacion.Fallido, n.Estado);
            Assert.Contains("SMTP", n.Error);

            _correo.Falla.Clear();
            var reintento = await _servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", null, "x");

            Assert.Equal(ResultadoNotificacion.Enviado, reintento);
            n = Assert.Single(await Registros());
            Assert.Equal(Notificacion.Enviado, n.Estado);
            Assert.Equal(2, n.Intentos);
            Assert.Null(n.Error);
        }

        [Fact]
        public async Task UnEnvioPendienteReciente_bloquea_peroUnoInterrumpido_seReintenta()
        {
            var clave = "ComentarioCliente|evento:1|Correo|gerente.a@mym.pe";
            Db.Notificaciones.Add(new Notificacion { Tipo = "ComentarioCliente", Canal = "Correo", Destinatario = "gerente.a@mym.pe", Titulo = "t", ClaveDeduplicacion = clave, Estado = Notificacion.Pendiente, Intentos = 1, Fecha = DateTime.UtcNow });
            await Db.SaveChangesAsync();

            Assert.Equal(ResultadoNotificacion.Duplicado, await _servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", null, "x"));

            (await Db.Notificaciones.SingleAsync()).Fecha = DateTime.UtcNow - NotificacionService.PendienteCaducaEn - TimeSpan.FromMinutes(1);
            await Db.SaveChangesAsync();

            Assert.Equal(ResultadoNotificacion.Enviado, await _servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", null, "x"));
            Assert.Single(_correo.Enviados);
        }

        [Fact]
        public async Task Mensajes_deErrorMuyLargos_seRecortan()
        {
            var correoLargo = new CorreoQueFallaConMensajeLargo();
            var servicio = Notificaciones.Nucleo(Db, correoLargo);

            await servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", null, "x");

            Assert.Equal(300, (await Db.Notificaciones.SingleAsync()).Error!.Length);
        }

        private sealed class CorreoQueFallaConMensajeLargo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => throw new InvalidOperationException(new string('x', 1000));
        }

        // ---- Avisos dentro de la aplicación ----

        [Fact]
        public async Task AvisoEnApp_quedaSinLeer_conSusDatos_yNoSeRepitePorUsuario()
        {
            Assert.Equal(ResultadoNotificacion.Enviado, await _servicio.AvisarEnAppAsync(Aviso(), _gerenteA, "gerente.a@mym.pe"));
            Assert.Equal(ResultadoNotificacion.Duplicado, await _servicio.AvisarEnAppAsync(Aviso(), _gerenteA, "gerente.a@mym.pe"));
            Assert.Equal(ResultadoNotificacion.Enviado, await _servicio.AvisarEnAppAsync(Aviso(), _gerenteB));

            var avisos = await Registros(n => n.Canal == Notificacion.CanalApp);
            Assert.Equal(2, avisos.Count);
            var n = avisos[0];
            Assert.False(n.Leida);
            Assert.Equal(Notificacion.Enviado, n.Estado);
            Assert.Equal(_gerenteA, n.UsuarioId);
            Assert.Equal("Texto del aviso", n.Mensaje);
            Assert.Equal((1, 2), (n.ObraId, n.HitoId));
        }

        // ---- Integración con los avisos del seguimiento ----

        [Fact]
        public async Task ElComentarioDelCliente_avisaPorCorreoYEnLaApp_soloALosGerentes()
        {
            var comentario = new ComentarioHito { Id = 15, HitoId = _hito.Id, Texto = "¿Siguen en plazo?", AutorNombre = "Del Río", Fecha = DateTime.UtcNow };
            var gerentes = new[] { new DestinatarioNotificacion(_gerenteA, "gerente.a@mym.pe"), new DestinatarioNotificacion(_gerenteB, "gerente.b@mym.pe") };
            var notificaciones = Notificaciones.Con(Db, _correo);

            var entregado = await notificaciones.ComentarioDelClienteAsync(_obra, _hito, comentario, gerentes);

            Assert.True(entregado);
            Assert.Equal(2, _correo.Enviados.Count);
            var registros = await Registros();
            Assert.Equal(2, registros.Count(n => n.Canal == Notificacion.CanalCorreo));
            Assert.Equal(new[] { _gerenteA, _gerenteB }, registros.Where(n => n.Canal == Notificacion.CanalApp).Select(n => n.UsuarioId!.Value).OrderBy(x => x));
            Assert.All(registros.Where(n => n.Canal == Notificacion.CanalApp), n => Assert.Equal((_obra.Id, _hito.Id), (n.ObraId, n.HitoId)));
            Assert.DoesNotContain(registros, n => n.UsuarioId == _supervisor || n.UsuarioId == _clienteUsuario);
        }

        [Fact]
        public async Task ReenviarElMismoComentario_noDuplicaCorreosNiAvisos()
        {
            var comentario = new ComentarioHito { Id = 15, HitoId = _hito.Id, Texto = "Hola", AutorNombre = "Del Río", Fecha = DateTime.UtcNow };
            var gerentes = new[] { new DestinatarioNotificacion(_gerenteA, "gerente.a@mym.pe") };
            var notificaciones = Notificaciones.Con(Db, _correo);

            await notificaciones.ComentarioDelClienteAsync(_obra, _hito, comentario, gerentes);
            var segundo = await notificaciones.ComentarioDelClienteAsync(_obra, _hito, comentario, gerentes);

            Assert.True(segundo);
            Assert.Single(_correo.Enviados);
            Assert.Equal(2, (await Registros()).Count);
        }

        [Fact]
        public async Task LaRespuestaDelPersonal_avisaAlClienteEnLaApp_yPorCorreo()
        {
            var respuesta = new ComentarioHito { Id = 16, HitoId = _hito.Id, Texto = "Vamos en plazo", AutorNombre = "Marco Zapata", EsPersonal = true, Fecha = DateTime.UtcNow };

            var entregado = await Notificaciones.Con(Db, _correo).RespuestaAlClienteAsync(_obra, _hito, respuesta, new DestinatarioNotificacion(_clienteUsuario, "cliente@delrio.pe"));

            Assert.True(entregado);
            Assert.Equal("cliente@delrio.pe", Assert.Single(_correo.Enviados).Para);
            var app = Assert.Single(await Registros(n => n.Canal == Notificacion.CanalApp));
            Assert.Equal(_clienteUsuario, app.UsuarioId);
            Assert.Equal(Notificacion.TipoRespuestaPersonal, app.Tipo);
            Assert.Contains("Vamos en plazo", app.Mensaje);
        }

        [Fact]
        public async Task ElHitoCompletado_avisaALasCuentasDelCliente_yNoSeRepite()
        {
            var notificaciones = Notificaciones.Con(Db, _correo);

            Assert.True(await notificaciones.HitoCompletadoAsync(_obra, _hito, "cliente@delrio.pe"));
            Assert.True(await notificaciones.HitoCompletadoAsync(_obra, _hito, "cliente@delrio.pe"));

            Assert.Single(_correo.Enviados);
            var app = Assert.Single(await Registros(n => n.Canal == Notificacion.CanalApp));
            Assert.Equal(_clienteUsuario, app.UsuarioId);
            Assert.Equal(Notificacion.TipoHitoCompletado, app.Tipo);
        }

        [Fact]
        public async Task ElHitoCompletado_sinCorreoDelCliente_noEsEntregado()
        {
            Assert.False(await Notificaciones.Con(Db, _correo).HitoCompletadoAsync(_obra, _hito, null));
        }

        [Fact]
        public async Task LasAlertasDocumentales_unReintentoSoloLlegaALosQueFaltaban()
        {
            var alertas = new List<AlertaDocumental> { new(1, "Saúl Prueba", "SCTR", DateOnly.FromDateTime(DateTime.Now).AddDays(-3), -3, "Vencido") };
            var hoy = DateOnly.FromDateTime(DateTime.Now);
            var gerentes = new[] { new DestinatarioNotificacion(_gerenteA, "gerente.a@mym.pe"), new DestinatarioNotificacion(_gerenteB, "gerente.b@mym.pe") };
            var notificaciones = Notificaciones.Con(Db, _correo);
            _correo.Falla.Add("gerente.a@mym.pe");

            var primero = await notificaciones.AlertasDocumentalesAsync(gerentes, alertas, hoy);
            _correo.Falla.Clear();
            var segundo = await notificaciones.AlertasDocumentalesAsync(gerentes, alertas, hoy);
            var tercero = await notificaciones.AlertasDocumentalesAsync(gerentes, alertas, hoy);

            Assert.Equal(1, primero);
            Assert.Equal(2, segundo);
            Assert.Equal(2, tercero);
            Assert.Equal(new[] { "gerente.a@mym.pe", "gerente.b@mym.pe" }, _correo.Enviados.Select(e => e.Para).OrderBy(x => x));   // un correo por gerente, ni uno más
            Assert.Equal(2, (await Registros(n => n.Canal == Notificacion.CanalApp)).Count);
        }

        // ---- Campana: consulta y lectura de los avisos propios ----

        private async Task Sembrar(int usuarioId, int cantidad, int leidas = 0)
        {
            for (var i = 0; i < cantidad; i++)
                Db.Notificaciones.Add(new Notificacion
                {
                    Tipo = Notificacion.TipoComentarioCliente, Canal = Notificacion.CanalApp, UsuarioId = usuarioId, Titulo = $"Aviso {i}",
                    Estado = Notificacion.Enviado, Fecha = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i), Leida = i < leidas, Intentos = 1
                });
            await Db.SaveChangesAsync();
        }

        [Fact]
        public async Task Listar_devuelveSoloLosAvisosPropios_delMasRecienteAlMasAntiguo_conElTotalSinLeer()
        {
            await Sembrar(_gerenteA, 5, leidas: 2);
            await Sembrar(_gerenteB, 7);
            Db.Notificaciones.Add(new Notificacion { Tipo = "x", Canal = Notificacion.CanalCorreo, UsuarioId = _gerenteA, Titulo = "correo", Fecha = DateTime.UtcNow });
            await Db.SaveChangesAsync();

            var r = await Controlador(_gerenteA).Listar(null, null);

            Assert.Equal(new[] { "Aviso 4", "Aviso 3", "Aviso 2", "Aviso 1", "Aviso 0" }, Elementos(r).Select(a => (string)Valor(a, "titulo")));
            Assert.Equal(3, Valor(((ObjectResult)r).Value!, "noLeidas"));
        }

        [Fact]
        public async Task Listar_paginaConElTamanoPedido_yAjustaLosValoresFueraDeRango()
        {
            await Sembrar(_gerenteA, 25);
            var c = Controlador(_gerenteA);

            var p1 = await c.Listar(1, 10);
            var p3 = await c.Listar(3, 10);
            var enorme = await c.Listar(1, 5000);
            var fuera = await c.Listar(99, 10);
            var invalida = await c.Listar(-4, 0);

            Assert.Equal(10, Elementos(p1).Count);
            Assert.Equal((1, 10, 25, 3), (Valor(Info(p1), "pagina"), Valor(Info(p1), "tamano"), Valor(Info(p1), "total"), Valor(Info(p1), "totalPaginas")));
            Assert.Equal(5, Elementos(p3).Count);
            Assert.Equal(25, Elementos(enorme).Count);
            Assert.Equal(100, Valor(Info(enorme), "tamano"));
            Assert.Equal(3, Valor(Info(fuera), "pagina"));
            Assert.Equal(5, Elementos(fuera).Count);
            Assert.Equal((1, 1), (Valor(Info(invalida), "pagina"), Valor(Info(invalida), "tamano")));
        }

        [Fact]
        public async Task Listar_soloNoLeidas_yListaVacia()
        {
            await Sembrar(_gerenteA, 6, leidas: 4);

            Assert.Equal(2, Elementos(await Controlador(_gerenteA).Listar(null, null, soloNoLeidas: true)).Count);
            var vacia = await Controlador(_supervisor).Listar(null, null);
            Assert.Empty(Elementos(vacia));
            Assert.Equal(1, Valor(Info(vacia), "totalPaginas"));
            Assert.Equal(0, Valor(((ObjectResult)vacia).Value!, "noLeidas"));
        }

        [Fact]
        public async Task MarcarLeida_actualizaElAvisoPropio_yElContador()
        {
            await Sembrar(_gerenteA, 3);
            var id = (await Db.Notificaciones.OrderBy(n => n.Id).FirstAsync()).Id;

            var r = await Controlador(_gerenteA).MarcarLeida(id);

            Assert.Equal(2, Valor(((ObjectResult)r).Value!, "noLeidas"));
            var aviso = await Db.Notificaciones.AsNoTracking().SingleAsync(n => n.Id == id);
            Assert.True(aviso.Leida);
            Assert.NotNull(aviso.LeidaEn);
            Assert.Equal(2, Valor(((ObjectResult)await Controlador(_gerenteA).MarcarLeida(id)).Value!, "noLeidas"));   // repetir no cambia nada
        }

        [Fact]
        public async Task MarcarLeida_deUnAvisoAjeno_oInexistente_devuelve404_yNoLoModifica()
        {
            await Sembrar(_gerenteA, 1);
            var id = (await Db.Notificaciones.FirstAsync()).Id;

            Assert.IsType<NotFoundObjectResult>(await Controlador(_gerenteB).MarcarLeida(id));
            Assert.IsType<NotFoundObjectResult>(await Controlador(_gerenteA).MarcarLeida(99999));
            Assert.False((await Db.Notificaciones.AsNoTracking().SingleAsync()).Leida);
        }

        [Fact]
        public async Task MarcarTodasLeidas_soloAfectaAlUsuario()
        {
            await Sembrar(_gerenteA, 4);
            await Sembrar(_gerenteB, 3);

            var r = await Controlador(_gerenteA).MarcarTodasLeidas();

            Assert.Equal(4, Valor(((ObjectResult)r).Value!, "marcadas"));
            Assert.Equal(0, await Db.Notificaciones.AsNoTracking().CountAsync(n => n.UsuarioId == _gerenteA && !n.Leida));
            Assert.Equal(3, await Db.Notificaciones.AsNoTracking().CountAsync(n => n.UsuarioId == _gerenteB && !n.Leida));
        }

        // ---- Registro de envíos (auditoría) ----

        private async Task SembrarRegistro()
        {
            var baseFecha = new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc);
            Db.Notificaciones.AddRange(
                new Notificacion { Tipo = Notificacion.TipoComentarioCliente, Canal = "Correo", Destinatario = "a@mym.pe", Titulo = "uno", Estado = Notificacion.Enviado, Fecha = baseFecha },
                new Notificacion { Tipo = Notificacion.TipoAlertaDocumental, Canal = "Correo", Destinatario = "b@mym.pe", Titulo = "dos", Estado = Notificacion.Fallido, Error = "SMTP", Fecha = baseFecha.AddDays(2) },
                new Notificacion { Tipo = Notificacion.TipoAlertaDocumental, Canal = "App", UsuarioId = _gerenteA, Titulo = "tres", Estado = Notificacion.Enviado, Fecha = baseFecha.AddDays(4) });
            await Db.SaveChangesAsync();
        }

        [Fact]
        public async Task Registro_filtraPorCanalEstadoTipo_yPagina()
        {
            await SembrarRegistro();
            var c = Controlador(_gerenteA);

            Assert.Equal(3, Elementos(await c.Registro(null, null, null, null, null, null, null)).Count);
            Assert.Equal(new[] { "tres" }, Elementos(await c.Registro("App", null, null, null, null, null, null)).Select(x => (string)Valor(x, "titulo")));
            Assert.Equal(new[] { "dos" }, Elementos(await c.Registro(null, "Fallido", null, null, null, null, null)).Select(x => (string)Valor(x, "titulo")));
            Assert.Equal(2, Elementos(await c.Registro(null, null, Notificacion.TipoAlertaDocumental, null, null, null, null)).Count);
            var pagina = await c.Registro(null, null, null, null, null, 2, 2);
            Assert.Equal(new[] { "uno" }, Elementos(pagina).Select(x => (string)Valor(x, "titulo")));
            Assert.Equal(2, Valor(Info(pagina), "totalPaginas"));
        }

        [Fact]
        public async Task Registro_filtraPorRangoDeFechas_yRechazaFormatosInvalidos()
        {
            await SembrarRegistro();
            var c = Controlador(_gerenteA);

            var dia = await c.Registro(null, null, null, "2026-11-03", "2026-11-03", null, null);

            Assert.Equal(new[] { "dos" }, Elementos(dia).Select(x => (string)Valor(x, "titulo")));
            Assert.Equal(400, Estado(await c.Registro(null, null, null, "03/11/2026", null, null, null)));
            Assert.Equal(400, Estado(await c.Registro(null, null, null, null, "ayer", null, null)));
        }

        [Fact]
        public async Task Registro_incluyeElErrorPeroNuncaElCuerpoDeLosCorreos()
        {
            await _servicio.EnviarCorreoAsync(Aviso(), "gerente.a@mym.pe", null, "<p>secreto-del-cuerpo</p>");

            var json = System.Text.Json.JsonSerializer.Serialize(((ObjectResult)await Controlador(_gerenteA).Registro(null, null, null, null, null, null, null)).Value);

            Assert.DoesNotContain("secreto-del-cuerpo", json);
            Assert.Contains("gerente.a@mym.pe", json);
        }

        [Fact]
        public void Seguridad_laCampanaEsPersonal_yElRegistroSoloParaAdministracionYGerencia()
        {
            Assert.NotNull(typeof(NotificacionesController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(typeof(NotificacionesController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
            var registro = typeof(NotificacionesController).GetMethod(nameof(NotificacionesController.Registro))!.GetCustomAttribute<AuthorizeAttribute>()!;

            Assert.Equal(new[] { NombresRol.Administrador, NombresRol.Gerente }, registro.Roles!.Split(','));
            Assert.DoesNotContain(typeof(NotificacionesController).GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);
        }

        // ---- Paginación (HT-07) ----

        [Theory]
        [InlineData(0, 1, 20, 1, 1, 20)]
        [InlineData(0, null, null, 1, 1, 20)]
        [InlineData(45, 3, 20, 3, 3, 20)]
        [InlineData(45, 4, 20, 3, 3, 20)]
        [InlineData(45, 0, 20, 1, 3, 20)]
        [InlineData(45, null, 1000, 1, 1, 100)]
        [InlineData(100, 1, 100, 1, 1, 100)]
        [InlineData(101, 2, 100, 2, 2, 100)]
        [InlineData(5, 1, -3, 1, 5, 1)]
        public void Paginacion_calculaLaPaginaYElTamano(int total, int? pagina, int? tamano, int paginaEsperada, int totalPaginas, int tamanoEsperado)
        {
            var (p, info) = Paginacion.Calcular(total, pagina, tamano);

            Assert.Equal(paginaEsperada, p);
            Assert.Equal((paginaEsperada, tamanoEsperado, total, totalPaginas), (Valor(info, "pagina"), Valor(info, "tamano"), Valor(info, "total"), Valor(info, "totalPaginas")));
        }

        [Fact]
        public void Paginacion_enMemoria_devuelveLaPaginaPedida()
        {
            var datos = Enumerable.Range(1, 45).ToList();

            Assert.Equal(Enumerable.Range(21, 20), Paginacion.Paginar(datos, 2, 20).Items);
            Assert.Equal(Enumerable.Range(41, 5), Paginacion.Paginar(datos, 3, 20).Items);
            Assert.Equal(Enumerable.Range(41, 5), Paginacion.Paginar(datos, 50, 20).Items);
            Assert.Empty(Paginacion.Paginar(new List<int>(), 1, 20).Items);
        }
    }
}
