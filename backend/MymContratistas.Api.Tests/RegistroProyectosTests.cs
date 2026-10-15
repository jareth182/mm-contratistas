using Xunit;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales de HU11: registro de proyectos y clientes (HU11-T7)
    public sealed class RegistroProyectosTests : IDisposable
    {
        private class CorreoFalso : IEmailService
        {
            public List<(string Para, string Asunto)> Enviados { get; } = new();
            public bool Falla { get; set; }

            public Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, IReadOnlyList<AdjuntoCorreo>? adjuntos = null)
            {
                if (Falla) throw new InvalidOperationException("SMTP caído");
                Enviados.Add((destinatario, asunto));
                return Task.CompletedTask;
            }
        }

        private readonly SqliteConnection _conexion;
        private readonly ApplicationDbContext _db;
        private readonly CorreoFalso _correo = new();
        private readonly ClientesController _clientes;
        private readonly ObrasController _obras;

        public RegistroProyectosTests()
        {
            _conexion = new SqliteConnection("DataSource=:memory:");
            _conexion.Open();
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);
            _db.Database.EnsureCreated(); // incluye los roles sembrados

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:FrontendUrl"] = "http://localhost/" }).Build();
            var invitacion = new InvitacionService(_correo, config, NullLogger<InvitacionService>.Instance);

            _clientes = new ClientesController(_db);
            _obras = new ObrasController(_db, new PasswordHasher<Usuario>(), invitacion);
        }

        public void Dispose()
        {
            _db.Dispose();
            _conexion.Dispose();
        }

        // Lee una propiedad del objeto anónimo que devuelven los controladores
        private static T Dato<T>(IActionResult resultado, string propiedad)
        {
            var valor = (resultado as ObjectResult)?.Value ?? throw new InvalidOperationException("Sin cuerpo en la respuesta");
            var info = valor.GetType().GetProperty(propiedad, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"La respuesta no tiene «{propiedad}»");
            return (T)info.GetValue(valor)!;
        }

        private static int Estado(IActionResult r) => ((ObjectResult)r).StatusCode ?? 200;

        private async Task<Cliente> NuevoCliente(string nombre = "Inversiones Del Río S.A.C.", string documento = "20512345678",
            string? correo = "compras@delrio.pe", string tipo = "RUC")
        {
            var cliente = new Cliente { Nombre = nombre, TipoDocumento = tipo, Documento = documento, Correo = correo };
            _db.Clientes.Add(cliente);
            await _db.SaveChangesAsync();
            return cliente;
        }

        private static ObrasController.ObraDto DatosObra(int clienteId) => new()
        {
            Nombre = "Edificio multifamiliar Los Cedros",
            ClienteId = clienteId,
            Ubicacion = "Santiago de Surco, Lima",
            FechaInicio = new DateOnly(2026, 3, 2),
            FechaFinEstimada = new DateOnly(2026, 11, 28)
        };

        // ---------- Clientes (HU11-T3 / HU11-T4) ----------

        [Fact]
        public async Task RegistrarCliente_conDatosValidos_loGuarda()
        {
            var r = await _clientes.Crear(new() { Nombre = "  Familia Benavides ", TipoDocumento = "dni", Documento = "40219974", Correo = "Benavides@Mail.com" });

            Assert.Equal(201, Estado(r));
            var guardado = await _db.Clientes.SingleAsync();
            Assert.Equal("Familia Benavides", guardado.Nombre);
            Assert.Equal("DNI", guardado.TipoDocumento);
            Assert.Equal("benavides@mail.com", guardado.Correo);
        }

        [Theory]
        [InlineData("DNI", "1234567")]      // 7 dígitos
        [InlineData("DNI", "12345678A")]    // con letra
        [InlineData("RUC", "2051234567")]   // 10 dígitos
        public async Task RegistrarCliente_conDocumentoInvalido_esRechazado(string tipo, string documento)
        {
            var r = await _clientes.Crear(new() { Nombre = "Cliente", TipoDocumento = tipo, Documento = documento });

            Assert.Equal(400, Estado(r));
            Assert.Empty(_db.Clientes);
        }

        [Fact]
        public async Task RegistrarCliente_sinNombreOCorreoInvalido_esRechazado()
        {
            Assert.Equal(400, Estado(await _clientes.Crear(new() { Nombre = " ", TipoDocumento = "DNI", Documento = "40219974" })));
            Assert.Equal(400, Estado(await _clientes.Crear(new() { Nombre = "Ana", TipoDocumento = "DNI", Documento = "40219974", Correo = "no-es-correo" })));
            Assert.Empty(_db.Clientes);
        }

        [Fact]
        public async Task RegistrarCliente_conDocumentoRepetido_devuelveConflicto()
        {
            await NuevoCliente(documento: "20512345678");

            var r = await _clientes.Crear(new() { Nombre = "Otra empresa", TipoDocumento = "RUC", Documento = "20512345678" });

            Assert.Equal(409, Estado(r));
            Assert.Equal(1, await _db.Clientes.CountAsync());
        }

        [Fact]
        public async Task BuscarCliente_porNombreOPorDocumento()
        {
            await NuevoCliente("Inversiones Del Río S.A.C.", "20512345678");
            await NuevoCliente("Familia Benavides", "40219974", null, "DNI");

            static int Cantidad(IActionResult r) => ((IEnumerable<object>)((OkObjectResult)r).Value!).Count();

            Assert.Equal(1, Cantidad(await _clientes.Buscar("DEL RÍO")));   // sin distinguir mayúsculas
            Assert.Equal(1, Cantidad(await _clientes.Buscar("4021")));      // por inicio de documento
            Assert.Equal(2, Cantidad(await _clientes.Buscar(null)));        // sin filtro lista todos
            Assert.Equal(0, Cantidad(await _clientes.Buscar("zzz")));
        }

        // ---------- Proyecto / expediente (HU11-T1 / HU11-T5) ----------

        [Fact]
        public async Task RegistrarProyecto_creaExpedienteEnEjecucionVinculadoAlCliente()
        {
            var cliente = await NuevoCliente();

            var r = await _obras.PostObra(DatosObra(cliente.Id));

            Assert.Equal(201, Estado(r));
            var obra = await _db.Obras.SingleAsync();
            Assert.Equal("En Ejecución", obra.Estado);
            Assert.Equal(0, obra.AvancePorcentaje);
            Assert.Equal(cliente.Id, obra.ClienteId);
            Assert.Equal(cliente.Nombre, obra.Cliente);
            Assert.Equal(new DateOnly(2026, 3, 2), obra.FechaInicio);
            Assert.Null(obra.FechaCierre);
        }

        [Fact]
        public async Task RegistrarProyecto_habilitaElPanelCreandoLaCuentaDelCliente()
        {
            var cliente = await NuevoCliente(correo: "compras@delrio.pe");

            var r = await _obras.PostObra(DatosObra(cliente.Id));

            Assert.True(Dato<bool>(r, "panelHabilitado"));
            var cuenta = await _db.Usuarios.Include(u => u.Rol).SingleAsync(u => u.ClienteId == cliente.Id);
            Assert.Equal("compras@delrio.pe", cuenta.Correo);
            Assert.Equal(NombresRol.Cliente, cuenta.Rol.Nombre);
            Assert.NotNull(cuenta.TokenRecuperacion); // enlace para definir contraseña
            Assert.Single(_correo.Enviados);
            Assert.Equal("compras@delrio.pe", _correo.Enviados[0].Para);
        }

        [Fact]
        public async Task RegistrarSegundoProyecto_noDuplicaLaCuentaNiReenviaInvitacion()
        {
            var cliente = await NuevoCliente();

            await _obras.PostObra(DatosObra(cliente.Id));
            var r = await _obras.PostObra(DatosObra(cliente.Id));

            Assert.True(Dato<bool>(r, "panelHabilitado"));
            Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.ClienteId == cliente.Id));
            Assert.Single(_correo.Enviados);
            Assert.Equal(2, await _db.Obras.CountAsync());
        }

        [Fact]
        public async Task RegistrarProyecto_conCuentaClienteExistente_laVinculaSinCrearOtra()
        {
            var cliente = await NuevoCliente(correo: "contacto@delrio.pe");
            var rol = await _db.Roles.SingleAsync(x => x.Nombre == NombresRol.Cliente);
            _db.Usuarios.Add(new Usuario { Nombres = "Del Río", Correo = "contacto@delrio.pe", RolId = rol.Id, PasswordHash = "x" });
            await _db.SaveChangesAsync();

            var r = await _obras.PostObra(DatosObra(cliente.Id));

            Assert.True(Dato<bool>(r, "panelHabilitado"));
            Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Correo == "contacto@delrio.pe"));
            Assert.NotNull((await _db.Usuarios.SingleAsync(u => u.Correo == "contacto@delrio.pe")).ClienteId);
            Assert.Empty(_correo.Enviados);
        }

        [Fact]
        public async Task RegistrarProyecto_clienteSinCorreo_guardaLaObraPeroNoHabilitaElPanel()
        {
            var cliente = await NuevoCliente(correo: null);

            var r = await _obras.PostObra(DatosObra(cliente.Id));

            Assert.Equal(201, Estado(r));
            Assert.False(Dato<bool>(r, "panelHabilitado"));
            Assert.Equal(1, await _db.Obras.CountAsync());
            Assert.Empty(_db.Usuarios);
        }

        [Fact]
        public async Task RegistrarProyecto_correoDePersonal_noSeVinculaALaFicha()
        {
            var cliente = await NuevoCliente(correo: "marco.zapata@mym.pe");
            var gerente = await _db.Roles.SingleAsync(x => x.Nombre == NombresRol.Gerente);
            _db.Usuarios.Add(new Usuario { Nombres = "Marco", Correo = "marco.zapata@mym.pe", RolId = gerente.Id, PasswordHash = "x" });
            await _db.SaveChangesAsync();

            var r = await _obras.PostObra(DatosObra(cliente.Id));

            Assert.False(Dato<bool>(r, "panelHabilitado"));
            Assert.Null((await _db.Usuarios.SingleAsync()).ClienteId);
        }

        [Fact]
        public async Task RegistrarProyecto_siFallaElCorreo_elProyectoYLaCuentaSeConservan()
        {
            var cliente = await NuevoCliente();
            _correo.Falla = true;

            var r = await _obras.PostObra(DatosObra(cliente.Id));

            Assert.Equal(201, Estado(r));
            Assert.False(Dato<bool>(r, "correoEnviado"));
            Assert.Equal(1, await _db.Obras.CountAsync());
            Assert.Equal(1, await _db.Usuarios.CountAsync());
        }

        [Fact]
        public async Task RegistrarProyecto_conDatosInvalidos_esRechazadoYNoGuardaNada()
        {
            var cliente = await NuevoCliente();

            var sinNombre = DatosObra(cliente.Id); sinNombre.Nombre = "  ";
            var clienteInexistente = DatosObra(999);
            var sinInicio = DatosObra(cliente.Id); sinInicio.FechaInicio = null;
            var finAntes = DatosObra(cliente.Id); finAntes.FechaFinEstimada = new DateOnly(2026, 1, 1);

            foreach (var dto in new[] { sinNombre, clienteInexistente, sinInicio, finAntes })
                Assert.Equal(400, Estado(await _obras.PostObra(dto)));

            Assert.Empty(_db.Obras);
            Assert.Empty(_db.Usuarios);
            Assert.Empty(_correo.Enviados);
        }

        // ---------- Finalizar (HU11-T6) ----------

        private async Task<Obra> ObraEnEjecucion()
        {
            var cliente = await NuevoCliente();
            await _obras.PostObra(DatosObra(cliente.Id));
            return await _db.Obras.SingleAsync();
        }

        [Fact]
        public async Task Finalizar_conFechaReal_cambiaElEstadoYGuardaElCierre()
        {
            var obra = await ObraEnEjecucion();

            var r = await _obras.Finalizar(obra.Id, new() { FechaCierre = new DateOnly(2026, 9, 30) });

            Assert.Equal(200, Estado(r));
            var guardada = await _db.Obras.AsNoTracking().SingleAsync();
            Assert.Equal("Finalizado", guardada.Estado);
            Assert.Equal(new DateOnly(2026, 9, 30), guardada.FechaCierre);
        }

        [Fact]
        public async Task Finalizar_sinFecha_usaLaFechaDeHoy()
        {
            var obra = await ObraEnEjecucion();

            await _obras.Finalizar(obra.Id, null);

            Assert.Equal(DateOnly.FromDateTime(DateTime.Now), (await _db.Obras.AsNoTracking().SingleAsync()).FechaCierre);
        }

        [Fact]
        public async Task Finalizar_conFechaFuturaOAnteriorAlInicio_esRechazado()
        {
            var obra = await ObraEnEjecucion();

            var futura = await _obras.Finalizar(obra.Id, new() { FechaCierre = DateOnly.FromDateTime(DateTime.Now).AddDays(5) });
            var anterior = await _obras.Finalizar(obra.Id, new() { FechaCierre = new DateOnly(2026, 3, 1) });

            Assert.Equal(400, Estado(futura));
            Assert.Equal(400, Estado(anterior));
            Assert.Equal("En Ejecución", (await _db.Obras.AsNoTracking().SingleAsync()).Estado);
        }

        [Fact]
        public async Task Finalizar_obraYaFinalizadaOInexistente()
        {
            var obra = await ObraEnEjecucion();
            await _obras.Finalizar(obra.Id, new() { FechaCierre = new DateOnly(2026, 9, 30) });

            Assert.Equal(409, Estado(await _obras.Finalizar(obra.Id, new() { FechaCierre = new DateOnly(2026, 10, 1) })));
            Assert.Equal(404, Estado(await _obras.Finalizar(999, null)));
            Assert.Equal(new DateOnly(2026, 9, 30), (await _db.Obras.AsNoTracking().SingleAsync()).FechaCierre);
        }

        // ---------- Panel de seguimiento del cliente (HU11-T5 con HU12) ----------

        private ClienteController PanelDe(Usuario usuario)
        {
            var identidad = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.NombreCompleto),
                new Claim(ClaimTypes.Role, NombresRol.Cliente)
            }, "Prueba");
            return new ClienteController(_db)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) } }
            };
        }

        private static int ObrasVisibles(IActionResult r) => ((System.Collections.IEnumerable)((OkObjectResult)r).Value!).Cast<object>().Count();

        [Fact]
        public async Task PanelCliente_muestraLasObrasDeSuFichaAunqueCambieElNombreDeLaCuenta()
        {
            var cliente = await NuevoCliente();
            await _obras.PostObra(DatosObra(cliente.Id));
            var cuenta = await _db.Usuarios.SingleAsync(u => u.ClienteId == cliente.Id);
            cuenta.Nombres = "Otro nombre visible";
            await _db.SaveChangesAsync();

            Assert.Equal(1, ObrasVisibles(await PanelDe(cuenta).MisObras()));
        }

        [Fact]
        public async Task PanelCliente_noMuestraObrasDeOtroClienteConElMismoNombre()
        {
            var propio = await NuevoCliente("Constructora Lima", "20111111111", "a@lima.pe");
            var ajeno = await NuevoCliente("Constructora Lima", "20222222222", "b@lima.pe");
            await _obras.PostObra(DatosObra(propio.Id));
            await _obras.PostObra(DatosObra(ajeno.Id));
            var cuenta = await _db.Usuarios.SingleAsync(u => u.ClienteId == propio.Id);

            Assert.Equal(1, ObrasVisibles(await PanelDe(cuenta).MisObras()));
        }

        [Fact]
        public async Task PanelCliente_obrasAnterioresSinFicha_seReconocenPorNombre()
        {
            _db.Obras.Add(new Obra { Nombre = "Casa de playa Asia", Cliente = "Familia Benavides" });
            var rol = await _db.Roles.SingleAsync(x => x.Nombre == NombresRol.Cliente);
            var cuenta = new Usuario { Nombres = "Familia", Apellidos = "Benavides", Correo = "fb@mail.com", RolId = rol.Id, PasswordHash = "x" };
            _db.Usuarios.Add(cuenta);
            await _db.SaveChangesAsync();

            Assert.Equal(1, ObrasVisibles(await PanelDe(cuenta).MisObras()));
        }
    }
}
