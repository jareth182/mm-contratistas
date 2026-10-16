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
    // Pruebas funcionales de la administración de usuarios y roles (HU04-T7)
    public sealed class UsuariosTests : PruebaConBaseDeDatos
    {
        private class CorreoDePrueba : IEmailService
        {
            public List<string> Para { get; } = new();
            public bool Falla { get; set; }
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null)
            {
                if (Falla) throw new InvalidOperationException("SMTP caído");
                Para.Add(d);
                return Task.CompletedTask;
            }
        }

        private readonly CorreoDePrueba _correo = new();
        private readonly UsuariosController _usuarios;
        private readonly Usuario _admin;

        public UsuariosTests()
        {
            var invitacion = new InvitacionService(_correo, new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _usuarios = new UsuariosController(Db, new PasswordHasher<Usuario>(), invitacion);

            _admin = new Usuario { Nombres = "Maribel", Apellidos = "Minaya", Correo = "admin@mym.pe", RolId = 1, PasswordHash = "x" };
            Db.Usuarios.Add(_admin);
            Db.SaveChanges();

            // El administrador que opera es el de la sesión
            _usuarios.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _admin.Id.ToString()) }, "Prueba"))
                }
            };
        }

        private async Task<int> Crear(string correo = "marco.zapata@mym.pe", int rolId = 2) =>
            (int)Valor(Dato<object>(await _usuarios.Crear(new() { Nombres = "Marco", Apellidos = "Zapata", Correo = correo, RolId = rolId }), "usuario"), "id");

        private async Task<Usuario> Leer(int id)
        {
            await using var db = OtroContexto();
            return await db.Usuarios.Include(u => u.Rol).SingleAsync(u => u.Id == id);
        }

        [Fact]
        public async Task Roles_sonLosCuatroDelNegocio()
        {
            var roles = Lista(await _usuarios.Roles()).Select(r => (string)Valor(r, "nombre"));

            Assert.Equal(new[] { "Administrador", "Gerente de Operaciones", "Supervisor de Obra", "Cliente" }, roles);
        }

        [Fact]
        public async Task Crear_registraConRol_yEnviaEnlaceParaDefinirContrasena()
        {
            var r = await _usuarios.Crear(new() { Nombres = " Marco ", Apellidos = "Zapata", Correo = "Marco.Zapata@MYM.pe", RolId = 2 });

            Assert.Equal(201, Estado(r));
            Assert.True(Dato<bool>(r, "correoEnviado"));
            var u = await Leer((int)Valor(Dato<object>(r, "usuario"), "id"));
            Assert.Equal(("Marco", "marco.zapata@mym.pe", "Gerente de Operaciones", "Activo"), (u.Nombres, u.Correo, u.Rol.Nombre, u.Estado));
            Assert.NotNull(u.TokenRecuperacion);
            Assert.Equal("marco.zapata@mym.pe", Assert.Single(_correo.Para));
        }

        [Fact]
        public async Task Crear_siFallaElCorreo_igualRegistraYAvisa()
        {
            _correo.Falla = true;

            var r = await _usuarios.Crear(new() { Nombres = "Marco", Correo = "marco@mym.pe", RolId = 2 });

            Assert.Equal(201, Estado(r));
            Assert.False(Dato<bool>(r, "correoEnviado"));
        }

        [Fact]
        public async Task Crear_conDatosInvalidos_esRechazado()
        {
            Assert.Equal(400, Estado(await _usuarios.Crear(new() { Nombres = " ", Correo = "a@mym.pe", RolId = 2 })));
            Assert.Equal(400, Estado(await _usuarios.Crear(new() { Nombres = "A", Correo = "no-es-correo", RolId = 2 })));
            Assert.Equal(400, Estado(await _usuarios.Crear(new() { Nombres = "A", Correo = "a@mym.pe", RolId = 99 })));
            Assert.Equal(409, Estado(await _usuarios.Crear(new() { Nombres = "A", Correo = "ADMIN@mym.pe", RolId = 2 })));
            Assert.Equal(1, await Db.Usuarios.CountAsync());
        }

        [Fact]
        public async Task Editar_cambiaNombresYRol_peroNoElCorreo()
        {
            var id = await Crear();

            var r = await _usuarios.Editar(id, new() { Nombres = "Marco Antonio", Apellidos = "Zapata Neyra", Correo = "otro@mym.pe", RolId = 3 });

            Assert.Equal(200, Estado(r));
            var u = await Leer(id);
            Assert.Equal(("Marco Antonio", "Zapata Neyra", "Supervisor de Obra", "marco.zapata@mym.pe"), (u.Nombres, u.Apellidos, u.Rol.Nombre, u.Correo));
            Assert.Equal(404, Estado(await _usuarios.Editar(999, new() { Nombres = "X", RolId = 2 })));
        }

        [Fact]
        public async Task NoSePuedeQuitarElRolAlUnicoAdministradorActivo()
        {
            var r = await _usuarios.Editar(_admin.Id, new() { Nombres = "Maribel", RolId = 2 });

            Assert.Equal(400, Estado(r));
            Assert.Equal("Administrador", (await Leer(_admin.Id)).Rol.Nombre);

            await Crear("otro.admin@mym.pe", rolId: 1);
            Assert.Equal(200, Estado(await _usuarios.Editar(_admin.Id, new() { Nombres = "Maribel", RolId = 2 })));
        }

        [Fact]
        public async Task Desactivar_conservaElRegistro_eInvalidaElEnlacePendiente()
        {
            var id = await Crear();

            var r = await _usuarios.CambiarEstado(id, new() { Activo = false });

            Assert.Equal(200, Estado(r));
            var u = await Leer(id);
            Assert.Equal("Inactivo", u.Estado);
            Assert.Null(u.TokenRecuperacion);

            Assert.Equal(200, Estado(await _usuarios.CambiarEstado(id, new() { Activo = true })));
            Assert.Equal("Activo", (await Leer(id)).Estado);
        }

        [Fact]
        public async Task NoPuedeDesactivarseASiMismo_peroSiAOtroAdministrador()
        {
            Assert.Equal(400, Estado(await _usuarios.CambiarEstado(_admin.Id, new() { Activo = false })));

            var otroAdmin = await Crear("otro.admin@mym.pe", rolId: 1);
            await _usuarios.CambiarEstado(otroAdmin, new() { Activo = false });
            Assert.Equal("Inactivo", (await Leer(otroAdmin)).Estado);
            Assert.Equal("Activo", (await Leer(_admin.Id)).Estado);
        }

        [Fact]
        public async Task Listar_muestraNombreCorreoRolYEstado()
        {
            var id = await Crear();
            await _usuarios.CambiarEstado(id, new() { Activo = false });

            var marco = Lista(await _usuarios.Listar()).Single(u => (int)Valor(u, "id") == id);

            Assert.Equal(("Marco Zapata", "marco.zapata@mym.pe", "Gerente de Operaciones", "Inactivo"),
                ((string)Valor(marco, "nombreCompleto"), (string)Valor(marco, "correo"), (string)Valor(marco, "rol"), (string)Valor(marco, "estado")));
        }
    }
}
