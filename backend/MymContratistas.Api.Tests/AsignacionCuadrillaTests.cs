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
    // Pruebas funcionales de HU14: asignación de cuadrilla con SCTR y examen médico vigentes (HU14-T6)
    public sealed class AsignacionCuadrillaTests : PruebaConBaseDeDatos
    {
        private class SinCorreo : IEmailService
        {
            public Task EnviarAsync(string d, string a, string c, IReadOnlyList<AdjuntoCorreo>? adj = null) => Task.CompletedTask;
        }

        private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Now);

        private readonly PersonalController _personal;
        private readonly ObrasController _obras;
        private readonly int _obraId;

        public AsignacionCuadrillaTests()
        {
            _personal = new PersonalController(Db);
            var invitacion = new InvitacionService(new SinCorreo(), new ConfigurationBuilder().Build(), NullLogger<InvitacionService>.Instance);
            _obras = new ObrasController(Db, new PasswordHasher<Usuario>(), invitacion, new NotificacionHitoService(new SinCorreo(), NullLogger<NotificacionHitoService>.Instance));
            var obra = new Obra { Nombre = "Los Cedros", Cliente = "Del Río" };
            Db.Obras.Add(obra);
            Db.SaveChanges();
            _obraId = obra.Id;
        }

        private int Colaborador(string nombres = "Rómulo", string documento = "41258963", string especialidad = "Maestro de Obra")
        {
            var t = new Trabajador { Nombres = nombres, Apellidos = "Ccahuana", Documento = documento, Especialidad = especialidad, JornalDiario = 180, Telefono = "987654321" };
            Db.Personal.Add(t);
            Db.SaveChanges();
            return t.Id;
        }

        private void Vigencia(int trabajadorId, string tipo, int diasParaVencer)
        {
            Db.Vigencias.Add(new VigenciaDocumento { TrabajadorId = trabajadorId, Tipo = tipo, FechaInicio = Hoy.AddYears(-1), FechaVencimiento = Hoy.AddDays(diasParaVencer) });
            Db.SaveChanges();
        }

        private int ColaboradorApto(string nombres = "Rómulo", string documento = "41258963")
        {
            var id = Colaborador(nombres, documento);
            Vigencia(id, VigenciaDocumento.Sctr, 200);
            Vigencia(id, VigenciaDocumento.ExamenMedico, 200);
            return id;
        }

        private async Task<(int estado, Colaborador? colaborador, string? mensaje)> Asignar(int trabajadorId, int? obraId = null)
        {
            var r = await _obras.AsignarColaborador(obraId ?? _obraId, new Colaborador { TrabajadorId = trabajadorId });
            return r.Result switch
            {
                OkObjectResult ok => (200, (Colaborador)ok.Value!, null),
                BadRequestObjectResult mal => (400, null, (string)mal.Value!),
                NotFoundObjectResult => (404, null, null),
                _ => (500, null, null)
            };
        }

        private async Task<int> EnCuadrilla()
        {
            await using var db = OtroContexto();
            return await db.Cuadrilla.CountAsync(c => c.ObraId == _obraId);
        }

        [Fact]
        public async Task ConSctrYExamenVigentes_seAsigna_conFechaDeIngresoYEstadoActivo()
        {
            var id = ColaboradorApto();

            var (estado, c, _) = await Asignar(id);

            Assert.Equal(200, estado);
            Assert.Equal((id, "Rómulo Ccahuana", 180m, Hoy.ToString("dd/MM/yyyy"), "Activo", "Vigente"),
                (c!.TrabajadorId, c.NombreCompleto, c.Jornal, c.FechaIngresoObra, c.Estado, c.EstadoSctr));
        }

        [Fact]
        public async Task SctrVencido_impideLaAsignacion_ydiceCuandoVencio()
        {
            var id = Colaborador();
            Vigencia(id, VigenciaDocumento.Sctr, -23);
            Vigencia(id, VigenciaDocumento.ExamenMedico, 100);

            var (estado, _, mensaje) = await Asignar(id);

            Assert.Equal(400, estado);
            Assert.Contains("SCTR", mensaje);
            Assert.Contains($"venció el {Hoy.AddDays(-23):dd/MM/yyyy}", mensaje);
            Assert.DoesNotContain("Examen", mensaje);
            Assert.Equal(0, await EnCuadrilla());
        }

        [Fact]
        public async Task ExamenMedicoVencido_impideLaAsignacion()
        {
            var id = Colaborador();
            Vigencia(id, VigenciaDocumento.Sctr, 100);
            Vigencia(id, VigenciaDocumento.ExamenMedico, -1);

            var (estado, _, mensaje) = await Asignar(id);

            Assert.Equal(400, estado);
            Assert.Contains("Examen Médico", mensaje);
            Assert.Equal(0, await EnCuadrilla());
        }

        [Fact]
        public async Task SinDocumentosRegistrados_noSePuedeAsignar_yElMensajeLosNombraAmbos()
        {
            var (estado, _, mensaje) = await Asignar(Colaborador());

            Assert.Equal(400, estado);
            Assert.Contains("no tiene SCTR registrado", mensaje);
            Assert.Contains("no tiene Examen Médico registrado", mensaje);
        }

        [Fact]
        public async Task DocumentoPorVencer_todaviaPermiteAsignar()
        {
            var id = Colaborador();
            Vigencia(id, VigenciaDocumento.Sctr, 10);
            Vigencia(id, VigenciaDocumento.ExamenMedico, 0);   // vence hoy: aún vigente

            var (estado, c, _) = await Asignar(id);

            Assert.Equal(200, estado);
            Assert.Equal("Por vencer", c!.EstadoSctr);
        }

        [Fact]
        public async Task LaRenovacionRegistradaHabilitaLaAsignacion()
        {
            var id = Colaborador();
            Vigencia(id, VigenciaDocumento.Sctr, -5);
            Vigencia(id, VigenciaDocumento.ExamenMedico, 100);
            Assert.Equal(400, (await Asignar(id)).estado);

            var renovacion = await _personal.RegistrarVigencia(id, new() { Tipo = "sctr", FechaInicio = Hoy, FechaVencimiento = Hoy.AddYears(1) });

            Assert.Equal(201, Estado(renovacion));
            Assert.Equal(200, (await Asignar(id)).estado);
            Assert.Equal(2, await Db.Vigencias.CountAsync(v => v.Tipo == VigenciaDocumento.Sctr));   // el historial se conserva
        }

        [Fact]
        public async Task NoSePuedeAsignarDosVecesEnLaMismaObra_niEnUnaObraFinalizada()
        {
            var id = ColaboradorApto();
            Assert.Equal(200, (await Asignar(id)).estado);
            Assert.Equal(400, (await Asignar(id)).estado);

            var cerrada = new Obra { Nombre = "Cerrada", Cliente = "X", Estado = Obra.Finalizado };
            Db.Obras.Add(cerrada);
            await Db.SaveChangesAsync();
            Assert.Equal(400, (await Asignar(id, cerrada.Id)).estado);
            Assert.Equal(404, (await Asignar(id, 999)).estado);
        }

        [Fact]
        public async Task SinColaboradorElegido_esRechazado()
        {
            var r = await _obras.AsignarColaborador(_obraId, new Colaborador { NombreCompleto = "Escrito a mano" });

            Assert.IsType<BadRequestObjectResult>(r.Result);
            Assert.Equal(0, await EnCuadrilla());
        }

        [Fact]
        public async Task RegistrarSalida_conservaLaAsignacionEnElHistorial()
        {
            var id = ColaboradorApto();
            var (_, c, _) = await Asignar(id);

            var r = await _obras.RegistrarSalida(c!.Id, null);

            Assert.Equal(200, Estado(r));
            await using var db = OtroContexto();
            var guardado = await db.Cuadrilla.SingleAsync();
            Assert.Equal(("Retirado", Hoy), (guardado.Estado, guardado.FechaSalida!.Value));

            var historial = Lista(await _personal.Historial(id)).Single();
            Assert.Equal(Hoy, (DateOnly)Valor(historial, "fechaSalida"));
        }

        [Fact]
        public async Task RegistrarSalida_validaLaFecha_yNoSeRepite()
        {
            var id = ColaboradorApto();
            var (_, c, _) = await Asignar(id);

            Assert.IsType<BadRequestObjectResult>(await _obras.RegistrarSalida(c!.Id, new() { FechaSalida = Hoy.AddDays(1) }));
            Assert.IsType<BadRequestObjectResult>(await _obras.RegistrarSalida(c.Id, new() { FechaSalida = Hoy.AddDays(-3) }));
            Assert.IsType<NotFoundObjectResult>(await _obras.RegistrarSalida(999, null));

            Assert.IsType<OkObjectResult>(await _obras.RegistrarSalida(c.Id, new() { FechaSalida = Hoy }));
            Assert.IsType<BadRequestObjectResult>(await _obras.RegistrarSalida(c.Id, null));
        }

        [Fact]
        public async Task TrasLaSalida_sePuedeVolverAAsignar_ySeGuardaOtraParticipacion()
        {
            var id = ColaboradorApto();
            var (_, primera, _) = await Asignar(id);
            await _obras.RegistrarSalida(primera!.Id, null);

            var (estado, segunda, _) = await Asignar(id);

            Assert.Equal(200, estado);
            Assert.NotEqual(primera.Id, segunda!.Id);
            Assert.Equal(2, await EnCuadrilla());
            Assert.Equal(2, Lista(await _personal.Historial(id)).Count);
        }

        [Fact]
        public async Task Disponibles_excluyeAsignadosEInactivos_yIndicaSiPuedenAsignarse()
        {
            var asignado = ColaboradorApto("Rómulo", "41258963");
            var inactivo = ColaboradorApto("Elmer", "44870125");
            var vencido = Colaborador("Julio", "40123456", "Oficial");
            Vigencia(vencido, VigenciaDocumento.Sctr, -14);
            Vigencia(vencido, VigenciaDocumento.ExamenMedico, 90);
            var libre = ColaboradorApto("Wilmer", "47001122");
            await Asignar(asignado);
            await _personal.CambiarEstado(inactivo, new() { Activo = false });

            var lista = Lista(await _obras.ColaboradoresDisponibles(_obraId, null));

            Assert.Equal(new[] { vencido, libre }, lista.Select(x => (int)Valor(x, "id")));
            var julio = lista.Single(x => (int)Valor(x, "id") == vencido);
            Assert.False((bool)Valor(julio, "apto"));
            Assert.Contains("SCTR", (string)Valor(julio, "motivo"));
            Assert.Equal("Vencido", Valor(Valor(julio, "sctr"), "estado"));
            Assert.Equal("Vigente", Valor(Valor(julio, "examenMedico"), "estado"));
            Assert.True((bool)Valor(lista.Single(x => (int)Valor(x, "id") == libre), "apto"));
        }

        [Fact]
        public async Task Disponibles_buscaPorNombreDocumentoOEspecialidad_yVuelveAAparecerTrasLaSalida()
        {
            var romulo = ColaboradorApto("Rómulo", "41258963");
            Colaborador("Elmer", "44870125", "Operario");

            Assert.Single(Lista(await _obras.ColaboradoresDisponibles(_obraId, "rómulo")));
            Assert.Single(Lista(await _obras.ColaboradoresDisponibles(_obraId, "4487")));
            Assert.Single(Lista(await _obras.ColaboradoresDisponibles(_obraId, "operario")));
            Assert.Empty(Lista(await _obras.ColaboradoresDisponibles(_obraId, "zzz")));

            var (_, c, _) = await Asignar(romulo);
            Assert.Single(Lista(await _obras.ColaboradoresDisponibles(_obraId, null)));
            await _obras.RegistrarSalida(c!.Id, null);
            Assert.Equal(2, Lista(await _obras.ColaboradoresDisponibles(_obraId, null)).Count);
            Assert.IsType<NotFoundObjectResult>(await _obras.ColaboradoresDisponibles(999, null));
        }

        [Fact]
        public async Task RegistrarVigencia_validaLosDatos()
        {
            var id = Colaborador();

            Assert.Equal(400, Estado(await _personal.RegistrarVigencia(id, new() { Tipo = "Licencia", FechaInicio = Hoy, FechaVencimiento = Hoy.AddDays(30) })));
            Assert.Equal(400, Estado(await _personal.RegistrarVigencia(id, new() { Tipo = "SCTR", FechaInicio = Hoy })));
            Assert.Equal(400, Estado(await _personal.RegistrarVigencia(id, new() { Tipo = "SCTR", FechaInicio = Hoy, FechaVencimiento = Hoy })));
            Assert.Equal(404, Estado(await _personal.RegistrarVigencia(999, new() { Tipo = "SCTR", FechaInicio = Hoy, FechaVencimiento = Hoy.AddDays(30) })));
            Assert.Empty(Db.Vigencias);
        }

        [Fact]
        public async Task Vigencias_listaElHistorialConSuEstado()
        {
            var id = Colaborador();
            Vigencia(id, VigenciaDocumento.Sctr, -40);
            Vigencia(id, VigenciaDocumento.Sctr, 20);
            Vigencia(id, VigenciaDocumento.ExamenMedico, 200);

            var lista = Lista(await _personal.Vigencias(id));

            Assert.Equal(new[] { "Examen Médico", "SCTR", "SCTR" }, lista.Select(v => (string)Valor(v, "tipo")));
            Assert.Equal(new[] { "Vigente", "Por vencer", "Vencido" }, lista.Select(v => (string)Valor(v, "estado")));
            Assert.IsType<NotFoundObjectResult>(await _personal.Vigencias(999));
        }
    }
}
