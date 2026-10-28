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
    // Pruebas funcionales del mantenimiento de personal (HU10-T6)
    public sealed class PersonalTests : PruebaConBaseDeDatos
    {
        private class SinCorreo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => Task.CompletedTask;
        }

        private readonly PersonalController _personal;
        private readonly ObrasController _obras;

        public PersonalTests()
        {
            _personal = new PersonalController(Db);
            var invitacion = new InvitacionService(new SinCorreo(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, new NotificacionHitoService(new SinCorreo(), NullLogger<NotificacionHitoService>.Instance));
        }

        private static PersonalController.TrabajadorDto Dto(string documento = "41258963", string nombres = "Rómulo", string especialidad = "Maestro de Obra") => new()
        {
            Nombres = nombres, Apellidos = "Ccahuana Ayala", TipoDocumento = "DNI", Documento = documento,
            Especialidad = especialidad, Telefono = "987 654 321", JornalDiario = 180
        };

        private async Task<int> Crear(PersonalController.TrabajadorDto? dto = null) =>
            (int)Valor(Dato<object>(await _personal.Crear(dto ?? Dto()), "trabajador"), "id");

        private async Task<int> Obra(string nombre = "Los Cedros")
        {
            var obra = new Obra { Nombre = nombre, Cliente = "Del Río" };
            Db.Obras.Add(obra);
            await Db.SaveChangesAsync();
            return obra.Id;
        }

        private async Task<int> Asignar(int obraId, int trabajadorId)
        {
            // La asignación exige SCTR y examen médico vigentes (HU14)
            if (!await Db.Vigencias.AnyAsync(v => v.TrabajadorId == trabajadorId))
            {
                var hoy = DateOnly.FromDateTime(DateTime.Now);
                foreach (var tipo in VigenciaDocumento.Tipos)
                    Db.Vigencias.Add(new VigenciaDocumento { TrabajadorId = trabajadorId, Tipo = tipo, FechaInicio = hoy.AddMonths(-1), FechaVencimiento = hoy.AddMonths(6) });
                await Db.SaveChangesAsync();
            }
            var r = await _obras.AsignarColaborador(obraId, new Colaborador { TrabajadorId = trabajadorId });
            return r.Result switch { OkObjectResult => 200, BadRequestObjectResult => 400, NotFoundObjectResult => 404, _ => 500 };
        }

        [Fact]
        public async Task Registrar_guardaLaFichaActiva()
        {
            var r = await _personal.Crear(Dto());

            Assert.Equal(201, Estado(r));
            var t = await Db.Personal.SingleAsync();
            Assert.Equal(("Rómulo Ccahuana Ayala", "Activo", 180m), (t.NombreCompleto, t.Estado, t.JornalDiario));
        }

        [Theory]
        [InlineData("nombres")]
        [InlineData("dni")]
        [InlineData("especialidad")]
        [InlineData("jornal")]
        [InlineData("telefono")]
        public async Task Registrar_conDatosInvalidos_esRechazado(string caso)
        {
            var dto = Dto();
            if (caso == "nombres") dto.Nombres = " ";
            if (caso == "dni") dto.Documento = "1234";
            if (caso == "especialidad") dto.Especialidad = "Electricista";
            if (caso == "jornal") dto.JornalDiario = 0;
            if (caso == "telefono") dto.Telefono = "llámame";

            Assert.Equal(400, Estado(await _personal.Crear(dto)));
            Assert.Empty(Db.Personal);
        }

        [Fact]
        public async Task Registrar_conDocumentoRepetido_esRechazado()
        {
            await Crear();

            Assert.Equal(400, Estado(await _personal.Crear(Dto(nombres: "Otro"))));
            Assert.Equal(1, await Db.Personal.CountAsync());
        }

        [Fact]
        public async Task Registrar_conCarneDeExtranjeria()
        {
            var dto = Dto(); dto.TipoDocumento = "CE"; dto.Documento = "abc123456";

            Assert.Equal(201, Estado(await _personal.Crear(dto)));
            Assert.Equal("ABC123456", (await Db.Personal.SingleAsync()).Documento);
        }

        [Fact]
        public async Task Buscar_porNombreDocumentoOEspecialidad_ySoloActivos()
        {
            await Crear(Dto("41258963", "Rómulo", "Maestro de Obra"));
            var elmer = await Crear(Dto("44870125", "Elmer", "Operario"));
            await _personal.CambiarEstado(elmer, new() { Activo = false });

            Assert.Single(Lista(await _personal.Listar("rómulo")));
            Assert.Single(Lista(await _personal.Listar("4487")));
            Assert.Single(Lista(await _personal.Listar("operario")));
            Assert.Equal(2, Lista(await _personal.Listar(null)).Count);
            Assert.Equal("Rómulo Ccahuana Ayala", Valor(Lista(await _personal.Listar(null, soloActivos: true)).Single(), "nombreCompleto"));
        }

        [Fact]
        public async Task Editar_actualizaLaFichaYConservaElHistorialDeObras()
        {
            var id = await Crear();
            var cedros = await Obra("Los Cedros");
            var miraflores = await Obra("Oficinas Miraflores");
            await Asignar(cedros, id);
            await Asignar(miraflores, id);

            var cambios = Dto(nombres: "Rómulo Andrés", especialidad: "Operario"); cambios.JornalDiario = 200;
            Assert.Equal(200, Estado(await _personal.Editar(id, cambios)));

            var historial = Lista(await _personal.Historial(id));
            Assert.Equal(new[] { "Oficinas Miraflores", "Los Cedros" }, historial.Select(h => (string)Valor(h, "obra")));
            Assert.Equal(200m, (await OtroContexto().Personal.SingleAsync()).JornalDiario);
        }

        [Fact]
        public async Task Registrar_vinculaAsignacionesPreviasConElMismoDocumento()
        {
            var obra = await Obra();
            Db.Cuadrilla.Add(new Colaborador { NombreCompleto = "Rómulo", Dni = "41258963", ObraId = obra, Especialidad = "Operario" });
            Db.Cuadrilla.Add(new Colaborador { NombreCompleto = "Otro", Dni = "00000000", ObraId = obra, Especialidad = "Peón" });
            await Db.SaveChangesAsync();

            var id = await Crear();

            Assert.Single(Lista(await _personal.Historial(id)));
        }

        [Fact]
        public async Task Desactivar_loExcluyeDeLaAsignacion_peroConservaFichaEHistorial()
        {
            var id = await Crear();
            var cedros = await Obra("Los Cedros");
            var nueva = await Obra("Nueva obra");
            await Asignar(cedros, id);

            Assert.Equal(200, Estado(await _personal.CambiarEstado(id, new() { Activo = false })));

            Assert.Equal(400, await Asignar(nueva, id));
            Assert.Empty(Lista(await _personal.Listar(null, soloActivos: true)));
            Assert.Single(Lista(await _personal.Historial(id)));
            Assert.Equal("Inactivo", (await OtroContexto().Personal.SingleAsync()).Estado);

            await _personal.CambiarEstado(id, new() { Activo = true });
            Assert.Equal(200, await Asignar(nueva, id));
        }

        [Fact]
        public async Task Asignar_tomaLosDatosDeLaFicha_yNoDuplicaEnLaMismaObra()
        {
            var id = await Crear();
            var obra = await Obra();

            Assert.Equal(200, await Asignar(obra, id));
            Assert.Equal(400, await Asignar(obra, id));

            var c = await Db.Cuadrilla.SingleAsync();
            Assert.Equal(("Rómulo Ccahuana Ayala", "41258963", "Maestro de Obra", 180m), (c.NombreCompleto, c.Dni, c.Especialidad, c.Jornal));
        }

        [Fact]
        public async Task OperacionesSobreColaboradorInexistente_devuelven404()
        {
            Assert.Equal(404, Estado(await _personal.Editar(999, Dto())));
            Assert.Equal(404, Estado(await _personal.CambiarEstado(999, new() { Activo = false })));
            Assert.Equal(404, Estado(await _personal.Historial(999)));
        }
    }
}
