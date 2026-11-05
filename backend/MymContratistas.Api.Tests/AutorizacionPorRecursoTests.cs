using System.Collections;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas de HT-06: autorización por recurso y descarga protegida de archivos
    public sealed class AutorizacionPorRecursoTests : PruebaConBaseDeDatos
    {
        // ---- 1) Inventario: ningún endpoint queda abierto o con el rol equivocado sin que una prueba lo note ----

        private static readonly string[] Personal = { NombresRol.Administrador, NombresRol.Gerente, NombresRol.Supervisor };
        private static readonly string[] AdministracionYGerencia = { NombresRol.Administrador, NombresRol.Gerente };

        // Controlador -> roles que pueden usarlo (null = cualquier usuario con sesión; vacío = nadie sin autenticar...)
        private static readonly Dictionary<string, string[]?> RolesEsperados = new()
        {
            [nameof(ClienteController)] = new[] { NombresRol.Cliente },
            [nameof(ObrasController)] = Personal,
            [nameof(PersonalController)] = Personal,
            [nameof(ClientesController)] = Personal,
            [nameof(PortafolioController)] = AdministracionYGerencia,
            [nameof(ServiciosController)] = AdministracionYGerencia,
            [nameof(UsuariosController)] = new[] { NombresRol.Administrador },
            [nameof(NotificacionesController)] = null
        };

        // Excepciones por acción dentro de un controlador (solo estas se apartan de la tabla anterior)
        private static readonly Dictionary<string, string[]?> RolesPorAccion = new()
        {
            ["NotificacionesController.Registro"] = AdministracionYGerencia,
            ["AuthController.Me"] = null,
            ["AuthController.Renovar"] = null
        };

        // Las únicas acciones que se pueden usar sin iniciar sesión: el sitio público, el login y la recuperación de clave
        private static readonly string[] AnonimasPermitidas =
        {
            "AuthController.Login", "AuthController.RecuperarPassword", "AuthController.ValidarToken", "AuthController.RestablecerPassword",
            "CotizacionesController.Registrar"
        };

        private static IEnumerable<Type> Controladores() =>
            typeof(ClienteController).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        private static IEnumerable<MethodInfo> Acciones(Type controlador) =>
            controlador.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

        private static bool EsAnonima(MethodInfo m) =>
            m.GetCustomAttribute<AllowAnonymousAttribute>() != null || m.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() != null;

        // Roles efectivos: ASP.NET exige todos los [Authorize] aplicables, así que se intersecan los de clase y de método
        private static string[]? RolesEfectivos(MethodInfo m)
        {
            var conRoles = m.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>().Concat(m.GetCustomAttributes<AuthorizeAttribute>())
                .Where(a => !string.IsNullOrWhiteSpace(a.Roles)).Select(a => a.Roles!.Split(',').Select(r => r.Trim()).ToArray()).ToList();
            return conRoles.Count == 0 ? null : conRoles.Aggregate((x, y) => x.Intersect(y).ToArray());
        }

        private static string Nombre(MethodInfo m) => $"{m.DeclaringType!.Name}.{m.Name}";

        [Fact]
        public void TodaAccionExigeSesion_salvoUnaLista_cerrada_deAccionesPublicas()
        {
            var sinProteccion = new List<string>();
            var anonimas = new List<string>();
            foreach (var m in Controladores().SelectMany(Acciones))
            {
                if (EsAnonima(m)) { anonimas.Add(Nombre(m)); continue; }
                var autorizada = m.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>().Any() || m.GetCustomAttributes<AuthorizeAttribute>().Any();
                if (!autorizada) sinProteccion.Add(Nombre(m));
            }

            Assert.Empty(sinProteccion);     // una acción sin [Authorize] ni [AllowAnonymous] quedaría pública por omisión
            var anonimasSitio = anonimas.Where(a => !a.StartsWith(nameof(SitioController))).OrderBy(a => a).ToList();
            Assert.Equal(AnonimasPermitidas.OrderBy(a => a), anonimasSitio);
        }

        [Fact]
        public void ElSitioPublico_soloExponeConsultas()
        {
            Assert.All(Acciones(typeof(SitioController)), m => Assert.All(m.GetCustomAttributes<HttpMethodAttribute>(), a => Assert.Equal(new[] { "GET" }, a.HttpMethods)));
        }

        [Fact]
        public void CadaControlador_tieneSuTablaDeRoles_yNingunoMezclaPersonalConClientes()
        {
            var sinDecidir = Controladores().Select(t => t.Name)
                .Where(n => n is not nameof(SitioController) and not nameof(AuthController) and not nameof(CotizacionesController) && !RolesEsperados.ContainsKey(n)).ToList();
            Assert.Empty(sinDecidir);        // un controlador nuevo obliga a decidir quién puede usarlo y a registrarlo aquí

            foreach (var m in Controladores().SelectMany(Acciones).Where(m => !EsAnonima(m)))
            {
                var esperados = RolesPorAccion.TryGetValue(Nombre(m), out var excepcion) ? excepcion
                    : RolesEsperados.TryGetValue(m.DeclaringType!.Name, out var porControlador) ? porControlador : null;
                var reales = RolesEfectivos(m);
                Assert.True(esperados == null ? reales == null || reales.Length > 0 : reales != null && esperados.OrderBy(x => x).SequenceEqual(reales.OrderBy(x => x)),
                    $"{Nombre(m)}: roles efectivos [{string.Join(",", reales ?? Array.Empty<string>())}] distintos de los esperados [{string.Join(",", esperados ?? Array.Empty<string>())}]");
            }

            // El panel del cliente es solo para clientes y ninguna acción del personal admite al rol Cliente
            Assert.All(Acciones(typeof(ClienteController)), m => Assert.Equal(new[] { NombresRol.Cliente }, RolesEfectivos(m)));
            foreach (var tipo in new[] { typeof(ObrasController), typeof(PersonalController), typeof(ClientesController), typeof(PortafolioController), typeof(ServiciosController), typeof(UsuariosController) })
                Assert.All(Acciones(tipo), m => Assert.DoesNotContain(NombresRol.Cliente, RolesEfectivos(m) ?? Array.Empty<string>()));
        }

        [Fact]
        public void LosArchivosPrivados_nuncaSeSirvenDesdeUnaRutaPublica()
        {
            // Toda acción que entrega un archivo es una acción con sesión (excepto las imágenes de proyectos publicados del sitio)
            var entregan = Controladores().SelectMany(Acciones)
                .Where(m => m.ReturnType == typeof(Task<IActionResult>) && (m.Name.Contains("Descargar") || m.Name.Contains("Ver") || m.Name.Contains("Comprobante") || m.Name.Contains("Imagen")))
                .ToList();
            Assert.NotEmpty(entregan);
            foreach (var m in entregan.Where(m => m.DeclaringType != typeof(SitioController)))
                Assert.False(EsAnonima(m), $"{Nombre(m)} sirve archivos sin sesión");
        }

        // ---- 2) Aislamiento entre clientes ----

        private int _usuarioA, _usuarioB, _obraA, _obraB, _hitoA, _hitoB, _fotoA, _fotoB, _docA, _docB, _gastoA, _gastoB, _avisoA, _avisoB;

        private void Sembrar()
        {
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Cliente).Id;
            (int usuario, int obra, int hito, int foto, int doc, int gasto, int aviso) Cliente(string s, string documento)
            {
                var c = new Cliente { Nombre = "Cliente " + s, Documento = documento, TipoDocumento = "RUC" };
                Db.Clientes.Add(c);
                Db.SaveChanges();
                var u = new Usuario { Nombres = "Cuenta " + s, Correo = $"{s}@mail.com", RolId = rol, PasswordHash = "x", ClienteId = c.Id };
                var o = new Obra { Nombre = "OBRA-" + s, Cliente = c.Nombre, ClienteId = c.Id, Ubicacion = "UBICACION-" + s };
                Db.Usuarios.Add(u);
                Db.Obras.Add(o);
                Db.SaveChanges();
                var h = new Hito { ObraId = o.Id, Nombre = "HITO-" + s, Orden = 1, Peso = 100 };
                Db.Hitos.Add(h);
                Db.SaveChanges();
                var av = new AvanceHito { HitoId = h.Id, Porcentaje = 10, Descripcion = "AVANCE-" + s, Fecha = DateTime.UtcNow, Responsable = "RESPONSABLE-" + s };
                av.Fotos.Add(new FotoAvance { Archivo = $"foto-{s}.png", NombreOriginal = $"FOTO-{s}.png" });
                Db.AvancesHito.Add(av);
                Db.ComentariosHito.Add(new ComentarioHito { HitoId = h.Id, Texto = "COMENTARIO-" + s, AutorNombre = "AUTOR-" + s, AutorUsuarioId = u.Id, Fecha = DateTime.UtcNow });
                var d = new Documento { ObraId = o.Id, Nombre = $"DOC-{s}.pdf", Archivo = $"doc-{s}.pdf", VisibleCliente = true, FechaCarga = "01/01/2026" };
                Db.Documentos.Add(d);
                var g = new Gasto { ObraId = o.Id, Fecha = "01/01/2026", Concepto = "GASTO-" + s, Proveedor = "PROVEEDOR-" + s, PartidaNombre = "PARTIDA-" + s, Monto = 100, ComprobanteUrl = $"comp-{s}.pdf", Archivo = $"comp-{s}.pdf" };
                Db.Gastos.Add(g);
                Db.Pagos.Add(new Pago { ObraId = o.Id, ColaboradorNombre = "COLABORADOR-" + s, Monto = 50, FechaPago = "01/01/2026", Periodo = "P" });
                var n = new Notificacion { Tipo = "x", Canal = Notificacion.CanalApp, UsuarioId = u.Id, Titulo = "AVISO-" + s, Estado = Notificacion.Enviado, Fecha = DateTime.UtcNow };
                Db.Notificaciones.Add(n);
                Db.SaveChanges();
                Directory.CreateDirectory(Path.Combine(Carpeta, "Uploads", "avances"));
                File.WriteAllBytes(Path.Combine(Carpeta, "Uploads", "avances", $"foto-{s}.png"), Archivos.Png());
                return (u.Id, o.Id, h.Id, av.Fotos[0].Id, d.Id, g.Id, n.Id);
            }
            (_usuarioA, _obraA, _hitoA, _fotoA, _docA, _gastoA, _avisoA) = Cliente("A", "20000000001");
            (_usuarioB, _obraB, _hitoB, _fotoB, _docB, _gastoB, _avisoB) = Cliente("B", "20000000002");
        }

        private static ControllerContext Sesion(int usuarioId, string nombre) => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()), new Claim(ClaimTypes.Name, nombre) }, "Prueba"))
            }
        };

        private ClienteController Panel(int usuarioId) => new(Db, Notificaciones.Silenciosa(Db)) { ControllerContext = Sesion(usuarioId, "Cuenta") };

        private static string Json(IActionResult r) => JsonSerializer.Serialize(((ObjectResult)r).Value);

        // Cada acción del panel con el identificador de un recurso del OTRO cliente; todas deben responder 403
        private Dictionary<string, Func<ClienteController, Task<IActionResult>>> ConRecursosAjenos(bool sonDeB) => new()
        {
            [nameof(ClienteController.SeguimientoDeObra)] = p => p.SeguimientoDeObra(sonDeB ? _obraB : _obraA),
            [nameof(ClienteController.DetalleDeHito)] = p => p.DetalleDeHito(sonDeB ? _hitoB : _hitoA),
            [nameof(ClienteController.Comentar)] = p => p.Comentar(sonDeB ? _hitoB : _hitoA, new ClienteController.ComentarioDto { Texto = "intruso" }),
            [nameof(ClienteController.VerFotoDeAvance)] = p => p.VerFotoDeAvance(sonDeB ? _fotoB : _fotoA),
            [nameof(ClienteController.DescargarDocumento)] = p => p.DescargarDocumento(sonDeB ? _docB : _docA),
            [nameof(ClienteController.GastosDeObra)] = p => p.GastosDeObra(sonDeB ? _obraB : _obraA),
            [nameof(ClienteController.ComprobanteDeGasto)] = p => p.ComprobanteDeGasto(sonDeB ? _gastoB : _gastoA)
        };

        [Fact]
        public void LaMatrizDeAislamiento_cubreTodasLasAccionesConIdentificadorDelPanel()
        {
            Sembrar();
            var conIdentificador = Acciones(typeof(ClienteController)).Where(m => m.GetParameters().Any(p => p.Name == "id")).Select(m => m.Name).OrderBy(n => n).ToList();

            // Si se agrega un endpoint con id al panel del cliente, esta prueba falla hasta que se pruebe su aislamiento
            Assert.Equal(conIdentificador, ConRecursosAjenos(true).Keys.OrderBy(n => n));
        }

        [Fact]
        public async Task ElClienteA_noAccedeANingunRecursoDelClienteB_enNingunaAccion()
        {
            Sembrar();
            foreach (var (accion, llamar) in ConRecursosAjenos(sonDeB: true))
            {
                var r = await llamar(Panel(_usuarioA));
                Assert.True(Estado(r) == 403, $"{accion}: el cliente A obtuvo {Estado(r)} sobre un recurso del cliente B");
                Assert.Equal(ClienteController.MensajeSinPermiso, (string)((ObjectResult)r).Value!);
            }
            Assert.Empty(Db.ComentariosHito.Where(c => c.Texto == "intruso"));
        }

        [Fact]
        public async Task ElClienteB_noAccedeANingunRecursoDelClienteA_enNingunaAccion()
        {
            Sembrar();
            foreach (var (accion, llamar) in ConRecursosAjenos(sonDeB: false))
                Assert.True(Estado(await llamar(Panel(_usuarioB))) == 403, $"{accion}: el cliente B obtuvo acceso a un recurso del cliente A");
        }

        [Fact]
        public async Task LosRecursosInexistentes_respondenIgualQueLosAjenos()
        {
            Sembrar();
            var p = Panel(_usuarioA);
            foreach (var r in new[] { await p.SeguimientoDeObra(99999), await p.DetalleDeHito(99999), await p.VerFotoDeAvance(99999), await p.DescargarDocumento(99999), await p.GastosDeObra(99999), await p.ComprobanteDeGasto(99999) })
                Assert.Equal(ClienteController.MensajeSinPermiso, (string)((ObjectResult)r).Value!);
        }

        [Theory]
        [InlineData("A", "B")]
        [InlineData("B", "A")]
        public async Task NingunaRespuestaDeUnClienteContieneDatosDelOtro(string propio, string ajeno)
        {
            Sembrar();
            var (usuario, obra, hito) = propio == "A" ? (_usuarioA, _obraA, _hitoA) : (_usuarioB, _obraB, _hitoB);
            var panel = Panel(usuario);
            var respuestas = new[]
            {
                Json(await panel.MisObras()), Json(await panel.SeguimientoDeObra(obra)), Json(await panel.DetalleDeHito(hito)), Json(await panel.GastosDeObra(obra)),
                Json(await new NotificacionesController(Db) { ControllerContext = Sesion(usuario, "Cuenta") }.Listar(null, null))
            };

            var todo = string.Join("\n", respuestas);
            Assert.Contains($"OBRA-{propio}", todo);
            Assert.Contains($"AVANCE-{propio}", todo);
            Assert.Contains($"COMENTARIO-{propio}", todo);
            Assert.Contains($"GASTO-{propio}", todo);
            Assert.Contains($"AVISO-{propio}", todo);
            Assert.DoesNotContain($"-{ajeno}", todo);                       // nada de los recursos del otro cliente
            Assert.DoesNotContain("RESPONSABLE-", todo);                    // ni datos internos del equipo
            Assert.DoesNotContain("COLABORADOR-", todo);                    // ni pagos al personal
        }

        [Fact]
        public async Task LasDescargasDelPropioCliente_siFuncionan_yEntreganElArchivoCorrecto()
        {
            Sembrar();
            Directory.CreateDirectory(Path.Combine(Carpeta, "Uploads", "documentos"));
            Directory.CreateDirectory(Path.Combine(Carpeta, "Uploads", "gastos"));
            File.WriteAllBytes(Path.Combine(Carpeta, "Uploads", "documentos", "doc-A.pdf"), Archivos.Pdf());
            File.WriteAllBytes(Path.Combine(Carpeta, "Uploads", "gastos", "comp-A.pdf"), Archivos.Pdf());
            var p = Panel(_usuarioA);

            Assert.IsType<PhysicalFileResult>(await p.VerFotoDeAvance(_fotoA));
            Assert.Equal("DOC-A.pdf", Assert.IsType<PhysicalFileResult>(await p.DescargarDocumento(_docA)).FileDownloadName);
            Assert.Equal("comp-A.pdf", Assert.IsType<PhysicalFileResult>(await p.ComprobanteDeGasto(_gastoA)).FileDownloadName);
        }

        [Fact]
        public async Task UnArchivoCuyoNombreIntentaSalirDeLaCarpeta_noSeEntrega()
        {
            Sembrar();
            File.WriteAllText(Path.Combine(Carpeta, "secreto.txt"), "no debe entregarse");
            var foto = await Db.FotosAvance.FindAsync(_fotoA);
            foto!.Archivo = @"..\..\secreto.txt";
            await Db.SaveChangesAsync();

            var r = await Panel(_usuarioA).VerFotoDeAvance(_fotoA);

            Assert.IsNotType<PhysicalFileResult>(r);
        }

        [Theory]
        [InlineData("foto.png", "foto.png")]
        [InlineData(@"..\..\secreto.txt", "secreto.txt")]
        [InlineData("../../etc/passwd", "passwd")]
        [InlineData(@"C:\Windows\win.ini", "win.ini")]
        [InlineData("/etc/passwd", "passwd")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void LasRutasDeArchivos_nuncaSalenDeSuCarpeta(string? guardado, string esperado)
        {
            var carpeta = Path.Combine(Path.GetTempPath(), "uploads-prueba");

            var ruta = RutasSeguras.Archivo(carpeta, guardado);

            Assert.Equal(Path.Combine(carpeta, esperado), ruta);
            Assert.StartsWith(Path.GetFullPath(carpeta), Path.GetFullPath(ruta));
        }

        // ---- 3) Cabeceras de seguridad ----

        private static async Task<IHeaderDictionary> Cabeceras(bool conSesion)
        {
            var contexto = new DefaultHttpContext();
            if (conSesion) contexto.Request.Headers.Authorization = "Bearer abc";
            await new EncabezadosDeSeguridad(_ => Task.CompletedTask).InvokeAsync(contexto);
            return contexto.Response.Headers;
        }

        [Fact]
        public async Task TodaRespuesta_llevaNosniffYReferrerPolicy()
        {
            foreach (var conSesion in new[] { true, false })
            {
                var h = await Cabeceras(conSesion);
                Assert.Equal("nosniff", h["X-Content-Type-Options"]);
                Assert.Equal("no-referrer", h["Referrer-Policy"]);
            }
        }

        [Fact]
        public async Task LasRespuestasConSesion_noSeGuardanEnCache_peroLasPublicasNoSeAlteran()
        {
            var privada = await Cabeceras(conSesion: true);
            var publica = await Cabeceras(conSesion: false);

            Assert.Equal("no-store", privada["Cache-Control"]);
            Assert.Equal("no-cache", privada["Pragma"]);
            Assert.False(publica.ContainsKey("Cache-Control"));
        }
    }
}
