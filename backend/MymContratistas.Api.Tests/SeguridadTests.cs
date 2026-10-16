using System.IdentityModel.Tokens.Jwt;
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
    // Pruebas funcionales de iniciar sesión (HU01-T6), sesión y su renovación (HU02-T5)
    // y recuperación de contraseña (HU03-T5)
    public sealed class SeguridadTests : PruebaConBaseDeDatos
    {
        private class CorreoDePrueba : IEmailService
        {
            public List<(string Para, string Cuerpo)> Enviados { get; } = new();
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null)
            {
                Enviados.Add((d, c));
                return Task.CompletedTask;
            }
        }

        private const string Clave = "Correcta123";
        private readonly CorreoDePrueba _correo = new();
        private readonly PasswordHasher<Usuario> _hasher = new();
        private readonly AuthController _auth;

        public SeguridadTests()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "clave-de-pruebas-con-mas-de-32-caracteres-0123",
                ["Jwt:Issuer"] = "MymContratistas.Api",
                ["Jwt:Audience"] = "MymContratistas.Web",
                ["App:FrontendUrl"] = "http://localhost/"
            }).Build();
            _auth = new AuthController(Db, _hasher, new TokenService(config), _correo, config, NullLogger<AuthController>.Instance);
        }

        private Usuario NuevoUsuario(string correo = "maribel.minaya@mym.pe", int rolId = 1, string estado = "Activo")
        {
            var u = new Usuario { Nombres = "Maribel", Apellidos = "Minaya Arellano", Correo = correo, RolId = rolId, Estado = estado };
            u.PasswordHash = _hasher.HashPassword(u, Clave);
            Db.Usuarios.Add(u);
            Db.SaveChanges();
            return u;
        }

        private Task<IActionResult> Login(string correo, string clave) => _auth.Login(new LoginDto { Correo = correo, Password = clave });

        private void ComoUsuario(Usuario u) => _auth.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()) }, "Prueba"))
            }
        };

        private async Task<Usuario> Leer(int id)
        {
            await using var db = OtroContexto();
            return await db.Usuarios.SingleAsync(u => u.Id == id);
        }

        // ---------- HU01: iniciar sesión ----------

        [Theory]
        [InlineData(1, "Administrador")]
        [InlineData(2, "Gerente de Operaciones")]
        [InlineData(3, "Supervisor de Obra")]
        [InlineData(4, "Cliente")]
        public async Task Login_valido_devuelveTokenConRolYNombreCompleto(int rolId, string rol)
        {
            NuevoUsuario(rolId: rolId);

            var r = await Login("  Maribel.Minaya@MYM.pe ", Clave);

            Assert.Equal(200, Estado(r));
            var usuario = Dato<object>(r, "usuario");
            Assert.Equal(rol, Valor(usuario, "rol"));
            Assert.Equal("Maribel Minaya Arellano", Valor(usuario, "nombreCompleto"));
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Dato<string>(r, "token"));
            Assert.Equal(rol, jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        }

        [Fact]
        public async Task Login_invalido_mismoMensajeSiNoExisteOSiLaClaveEstaMal()
        {
            NuevoUsuario();

            var claveMal = await Login("maribel.minaya@mym.pe", "otra");
            var noExiste = await Login("nadie@mym.pe", Clave);

            Assert.Equal(401, Estado(claveMal));
            Assert.Equal(Dato<string>(claveMal, "mensaje"), Dato<string>(noExiste, "mensaje"));
            Assert.Equal(400, Estado(await Login("", "")));
        }

        [Fact]
        public async Task CincoIntentosFallidos_bloqueanLaCuenta15Minutos_inclusoConLaClaveCorrecta()
        {
            var u = NuevoUsuario();

            for (var i = 0; i < 4; i++) await Login(u.Correo, "mal");
            var quinto = await Login(u.Correo, "mal");

            Assert.Contains("bloqueada", Dato<string>(quinto, "mensaje"));
            var bloqueo = (await Leer(u.Id)).BloqueoHasta!.Value;
            Assert.InRange(bloqueo, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(15.1));

            var conClaveCorrecta = await Login(u.Correo, Clave);
            Assert.Equal(401, Estado(conClaveCorrecta));
            Assert.Contains("bloqueada", Dato<string>(conClaveCorrecta, "mensaje"));
        }

        [Fact]
        public async Task PasadoElBloqueo_puedeIngresar_yLosIntentosSeReinician()
        {
            var u = NuevoUsuario();
            u.BloqueoHasta = DateTime.UtcNow.AddMinutes(-1);
            u.IntentosFallidos = 3;
            Db.SaveChanges();

            Assert.Equal(200, Estado(await Login(u.Correo, Clave)));
            var leido = await Leer(u.Id);
            Assert.Equal((0, (DateTime?)null), (leido.IntentosFallidos, leido.BloqueoHasta));
        }

        [Fact]
        public async Task UsuarioInactivo_noPuedeIngresar()
        {
            NuevoUsuario(estado: "Inactivo");

            var r = await Login("maribel.minaya@mym.pe", Clave);

            Assert.Equal(401, Estado(r));
            Assert.Contains("desactivada", Dato<string>(r, "mensaje"));
        }

        // ---------- HU02: sesión y renovación por actividad ----------

        [Fact]
        public async Task Token_venceALos30Minutos()
        {
            NuevoUsuario();

            var r = await Login("maribel.minaya@mym.pe", Clave);

            Assert.Equal(30, TokenService.MinutosVigencia);
            Assert.InRange(Dato<DateTime>(r, "expira"), DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(30.1));
        }

        [Fact]
        public async Task Continuar_renuevaElTokenMientrasLaCuentaSigaActiva()
        {
            var u = NuevoUsuario();
            ComoUsuario(u);

            var r = await _auth.Renovar();

            Assert.Equal(200, Estado(r));
            Assert.False(string.IsNullOrEmpty(Dato<string>(r, "token")));
        }

        [Fact]
        public async Task CuentaDesactivadaDuranteLaSesion_noPuedeRenovar()
        {
            var u = NuevoUsuario();
            ComoUsuario(u);
            u.Estado = "Inactivo";
            Db.SaveChanges();

            Assert.Equal(401, Estado(await _auth.Renovar()));
            Assert.IsType<UnauthorizedResult>(await _auth.Me());
        }

        // ---------- HU03: recuperar contraseña ----------

        private static string TokenDelCorreo(string cuerpo)
        {
            var i = cuerpo.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
            return new string(cuerpo[i..].TakeWhile(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        }

        [Fact]
        public async Task Recuperar_correoRegistrado_enviaEnlace_yNoExistente_respondeIgualSinEnviar()
        {
            NuevoUsuario();

            var registrado = await _auth.RecuperarPassword(new RecuperarDto { Correo = "MARIBEL.minaya@mym.pe" });
            var inexistente = await _auth.RecuperarPassword(new RecuperarDto { Correo = "nadie@mym.pe" });

            Assert.Equal(Dato<string>(registrado, "mensaje"), Dato<string>(inexistente, "mensaje"));
            Assert.Equal("maribel.minaya@mym.pe", Assert.Single(_correo.Enviados).Para);
        }

        [Fact]
        public async Task EnlaceVigente_permiteCambiarLaClave_unaSolaVez()
        {
            var u = NuevoUsuario();
            u.BloqueoHasta = DateTime.UtcNow.AddMinutes(10);
            Db.SaveChanges();
            await _auth.RecuperarPassword(new RecuperarDto { Correo = u.Correo });
            var token = TokenDelCorreo(_correo.Enviados.Single().Cuerpo);

            Assert.IsType<OkResult>(await _auth.ValidarToken(token));
            Assert.Equal(200, Estado(await _auth.RestablecerPassword(new ResetPasswordDto { Token = token, NuevaPassword = "NuevaClave1" })));

            var leido = await Leer(u.Id);
            Assert.NotEqual(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(leido, leido.PasswordHash, "NuevaClave1"));
            Assert.Null(leido.TokenRecuperacion);
            Assert.Null(leido.BloqueoHasta);

            // El mismo enlace ya no sirve
            Assert.Equal(400, Estado(await _auth.ValidarToken(token)));
            Assert.Equal(400, Estado(await _auth.RestablecerPassword(new ResetPasswordDto { Token = token, NuevaPassword = "OtraClave99" })));
        }

        [Fact]
        public async Task EnlaceVencido_esRechazado()
        {
            var u = NuevoUsuario();
            await _auth.RecuperarPassword(new RecuperarDto { Correo = u.Correo });
            var token = TokenDelCorreo(_correo.Enviados.Single().Cuerpo);
            await Db.Usuarios.ExecuteUpdateAsync(x => x.SetProperty(y => y.TokenExpiracion, DateTime.UtcNow.AddMinutes(-1)));

            Assert.Equal(400, Estado(await _auth.ValidarToken(token)));
            Assert.Equal(400, Estado(await _auth.RestablecerPassword(new ResetPasswordDto { Token = token, NuevaPassword = "NuevaClave1" })));
        }

        [Fact]
        public async Task ClaveDeMenosDe8Caracteres_esRechazada_yElEnlaceSigueValido()
        {
            var u = NuevoUsuario();
            await _auth.RecuperarPassword(new RecuperarDto { Correo = u.Correo });
            var token = TokenDelCorreo(_correo.Enviados.Single().Cuerpo);

            Assert.Equal(400, Estado(await _auth.RestablecerPassword(new ResetPasswordDto { Token = token, NuevaPassword = "corta" })));
            Assert.IsType<OkResult>(await _auth.ValidarToken(token));
        }

        [Fact]
        public async Task Recuperar_cuentaInactiva_noEnviaEnlace()
        {
            NuevoUsuario(estado: "Inactivo");

            await _auth.RecuperarPassword(new RecuperarDto { Correo = "maribel.minaya@mym.pe" });

            Assert.Empty(_correo.Enviados);
        }
    }
}
