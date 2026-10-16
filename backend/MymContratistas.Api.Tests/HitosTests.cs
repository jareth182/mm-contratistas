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
    // Pruebas funcionales de la definición de hitos (HU12-T7)
    public sealed class HitosTests : PruebaConBaseDeDatos
    {
        private class SinCorreo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => Task.CompletedTask;
        }

        private readonly ObrasController _obras;
        private readonly int _obraId;

        public HitosTests()
        {
            var invitacion = new InvitacionService(new SinCorreo(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion);
            var obra = new Obra { Nombre = "Los Cedros", Cliente = "Inversiones Del Río" };
            Db.Obras.Add(obra);
            Db.SaveChanges();
            _obraId = obra.Id;
        }

        private static ObrasController.HitoDto Dto(string nombre, int peso, DateOnly? fecha = null) =>
            new() { Nombre = nombre, Peso = peso, FechaPlanificada = fecha ?? new DateOnly(2026, 5, 1) };

        // Devuelve el código HTTP y, si se creó, el hito
        private async Task<(int estado, Hito? hito)> Agregar(string nombre, int peso)
        {
            var r = await _obras.PostHito(_obraId, Dto(nombre, peso));
            return r.Result switch
            {
                OkObjectResult ok => (200, (Hito)ok.Value!),
                BadRequestObjectResult => (400, null),
                NotFoundObjectResult => (404, null),
                _ => (500, null)
            };
        }

        private async Task<List<Hito>> HitosGuardados()
        {
            await using var db = OtroContexto();
            return await db.Hitos.Where(h => h.ObraId == _obraId).OrderBy(h => h.Orden).ToListAsync();
        }

        [Fact]
        public async Task Agregar_quedaPendiente_conOrdenCorrelativo()
        {
            await Agregar("Excavación", 10);
            var (estado, hito) = await Agregar("Cimentación", 20);

            Assert.Equal(200, estado);
            Assert.Equal(("Pendiente", 0, 2), (hito!.Estado, hito.AvancePorcentaje, hito.Orden));
            Assert.Equal(new DateOnly(2026, 5, 1), hito.FechaPlanificada);
        }

        [Fact]
        public async Task SumaDePesos_noPuedeSuperar100()
        {
            await Agregar("Excavación", 60);
            await Agregar("Estructura", 30);

            var (estado, _) = await Agregar("Acabados", 20);
            var (exacto, _) = await Agregar("Entrega", 10);

            Assert.Equal(400, estado);
            Assert.Equal(200, exacto);
            Assert.Equal(100, (await HitosGuardados()).Sum(h => h.Peso));
        }

        [Theory]
        [InlineData("", 10)]
        [InlineData("Excavación", 0)]
        [InlineData("Excavación", 101)]
        public async Task DatosInvalidos_sonRechazados(string nombre, int peso)
        {
            Assert.Equal(400, (await Agregar(nombre, peso)).estado);
            Assert.Empty(await HitosGuardados());
        }

        [Fact]
        public async Task NombreRepetidoEnLaObra_esRechazado()
        {
            await Agregar("Excavación", 10);

            Assert.Equal(400, (await Agregar("EXCAVACIÓN", 10)).estado);
            Assert.IsType<NotFoundObjectResult>((await _obras.PostHito(999, Dto("X", 10))).Result);
        }

        [Fact]
        public async Task EditarPendiente_actualiza_yRespetaLaSumaDePesos()
        {
            var (_, a) = await Agregar("Excavación", 40);
            await Agregar("Estructura", 50);

            Assert.IsType<OkObjectResult>(await _obras.EditarHito(a!.Id, Dto("Excavación masiva", 50, new DateOnly(2026, 6, 1))));
            Assert.IsType<BadRequestObjectResult>(await _obras.EditarHito(a.Id, Dto("Excavación masiva", 51)));

            var guardado = (await HitosGuardados()).First();
            Assert.Equal(("Excavación masiva", 50, new DateOnly(2026, 6, 1)), (guardado.Nombre, guardado.Peso, guardado.FechaPlanificada!.Value));
        }

        [Fact]
        public async Task HitoConAvance_noSePuedeEditarNiEliminar()
        {
            var (_, hito) = await Agregar("Excavación", 40);
            await _obras.ActualizarAvanceHito(hito!.Id, 30, "Avance", null);

            Assert.IsType<BadRequestObjectResult>(await _obras.EditarHito(hito.Id, Dto("Otro", 40)));
            Assert.IsType<BadRequestObjectResult>(await _obras.EliminarHito(hito.Id));
            Assert.Single(await HitosGuardados());
        }

        [Fact]
        public async Task EliminarPendiente_reordenaLosRestantes()
        {
            await Agregar("Excavación", 10);
            var (_, cimentacion) = await Agregar("Cimentación", 20);
            await Agregar("Estructura", 30);

            Assert.IsType<OkResult>(await _obras.EliminarHito(cimentacion!.Id));

            var hitos = await HitosGuardados();
            Assert.Equal(new[] { ("Excavación", 1), ("Estructura", 2) }, hitos.Select(h => (h.Nombre, h.Orden)));
        }

        [Fact]
        public async Task CompletarHito_registraLaFechaReal()
        {
            var (_, hito) = await Agregar("Excavación", 10);

            await _obras.ActualizarAvanceHito(hito!.Id, 100, null, null);

            var guardado = (await HitosGuardados()).Single();
            Assert.Equal(("Completado", 100, DateOnly.FromDateTime(DateTime.Now)), (guardado.Estado, guardado.AvancePorcentaje, guardado.FechaReal!.Value));
        }

        [Fact]
        public async Task EdicionDeHitos_seReflejaEnElPanelDelCliente()
        {
            var rol = await Db.Roles.SingleAsync(r => r.Nombre == NombresRol.Cliente);
            var cliente = new Usuario { Nombres = "Inversiones Del Río", Correo = "c@delrio.pe", RolId = rol.Id, PasswordHash = "x" };
            Db.Usuarios.Add(cliente);
            await Db.SaveChangesAsync();
            var (_, hito) = await Agregar("Excavación", 10);
            await _obras.EditarHito(hito!.Id, Dto("Excavación y nivelación", 15));

            var panel = new ClienteController(Db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, cliente.Id.ToString()),
                            new Claim(ClaimTypes.Name, cliente.NombreCompleto)
                        }, "Prueba"))
                    }
                }
            };

            var obra = Lista(await panel.MisObras()).Single();
            var hitos = ((System.Collections.IEnumerable)Valor(obra, "Hitos")).Cast<object>().ToList();
            Assert.Equal("Excavación y nivelación", Valor(hitos.Single(), "Nombre"));
            Assert.Equal(15, Valor(hitos.Single(), "Peso"));
        }
    }
}
