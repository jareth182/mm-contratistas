using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MymContratistas.Api.Controllers;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;
using Xunit;
using static MymContratistas.Api.Tests.Respuesta;

namespace MymContratistas.Api.Tests
{
    // Pruebas funcionales de HU24: alertas de documentación por vencer y aviso diario por correo (HU24-T6)
    public sealed class AlertasDocumentalesTests : PruebaConBaseDeDatos
    {
        private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Now);
        private static readonly TimeOnly HoraEnvio = new(7, 0);
        private const string GerenteA = "gerente.a@mym.pe";
        private const string GerenteB = "gerente.b@mym.pe";

        private readonly CorreoDePrueba _correo = new();
        private readonly PersonalController _personal;
        private readonly AlertasDocumentalesService _servicio;

        public AlertasDocumentalesTests()
        {
            _personal = new PersonalController(Db);
            _servicio = Servicio(_correo);

            var roles = Db.Roles.ToDictionary(r => r.Nombre, r => r.Id);
            Usuario Cuenta(string correo, string rol, string estado = "Activo") =>
                new() { Nombres = correo, Correo = correo, RolId = roles[rol], PasswordHash = "x", Estado = estado };
            Db.Usuarios.AddRange(
                Cuenta(GerenteA, NombresRol.Gerente), Cuenta(GerenteB, NombresRol.Gerente),
                Cuenta("inactivo@mym.pe", NombresRol.Gerente, "Inactivo"),
                Cuenta("admin@mym.pe", NombresRol.Administrador), Cuenta("supervisor@mym.pe", NombresRol.Supervisor));
            Db.SaveChanges();
        }

        private AlertasDocumentalesService Servicio(CorreoDePrueba correo) =>
            new(Db, Notificaciones.Con(correo), NullLogger<AlertasDocumentalesService>.Instance);

        private int Colaborador(string nombres, string documento, bool activo = true)
        {
            var t = new Trabajador { Nombres = nombres, Apellidos = "Prueba", Documento = documento, Especialidad = "Operario", JornalDiario = 150, Estado = activo ? Trabajador.Activo : Trabajador.Inactivo };
            Db.Personal.Add(t);
            Db.SaveChanges();
            return t.Id;
        }

        private void Vigencia(int trabajadorId, string tipo, int diasParaVencer)
        {
            var vence = Hoy.AddDays(diasParaVencer);
            Db.Vigencias.Add(new VigenciaDocumento { TrabajadorId = trabajadorId, Tipo = tipo, FechaInicio = vence.AddYears(-1), FechaVencimiento = vence });
            Db.SaveChanges();
        }

        private async Task<(List<object> alertas, object resumen)> Consultar()
        {
            var r = await _personal.Alertas();
            var cuerpo = ((ObjectResult)r).Value!;
            return (((System.Collections.IEnumerable)Valor(cuerpo, "alertas")).Cast<object>().ToList(), Valor(cuerpo, "resumen"));
        }

        private static DateTime A(DateOnly dia, int hora = 8) => dia.ToDateTime(new TimeOnly(hora, 0));

        // ---- Alertas del personal (T2) ----

        [Fact]
        public async Task Alertas_listaNombreDocumentoVencimientoYDiasRestantes_delMasUrgenteAlMenos()
        {
            var vencido = Colaborador("Saúl", "10000001");
            Vigencia(vencido, VigenciaDocumento.Sctr, -23);
            var proximo = Colaborador("Julio", "10000002");
            Vigencia(proximo, VigenciaDocumento.Sctr, 14);
            var lejano = Colaborador("Nilo", "10000003");
            Vigencia(lejano, VigenciaDocumento.ExamenMedico, 22);

            var (alertas, _) = await Consultar();

            Assert.Equal(new[] { "Saúl Prueba", "Julio Prueba", "Nilo Prueba" }, alertas.Select(a => (string)Valor(a, "colaborador")));
            Assert.Equal(new[] { "SCTR", "SCTR", "Examen Médico" }, alertas.Select(a => (string)Valor(a, "documento")));
            Assert.Equal(new[] { -23, 14, 22 }, alertas.Select(a => (int)Valor(a, "diasRestantes")));
            Assert.Equal(new[] { "Vencido", "Por vencer", "Por vencer" }, alertas.Select(a => (string)Valor(a, "estado")));
            Assert.Equal(Hoy.AddDays(-23), Valor(alertas[0], "vencimiento"));
            Assert.Equal(vencido, Valor(alertas[0], "trabajadorId"));
        }

        [Theory]
        [InlineData(-1, true, "Vencido")]
        [InlineData(0, true, "Por vencer")]
        [InlineData(30, true, "Por vencer")]
        [InlineData(31, false, "")]
        [InlineData(200, false, "")]
        public async Task Alertas_incluyeDocumentosVencidosYLosQueVencenEn30DiasOMenos(int dias, bool alerta, string estado)
        {
            Vigencia(Colaborador("Ana", "10000001"), VigenciaDocumento.Sctr, dias);

            var (alertas, _) = await Consultar();

            Assert.Equal(alerta ? 1 : 0, alertas.Count);
            if (alerta) Assert.Equal(estado, Valor(alertas[0], "estado"));
        }

        [Fact]
        public async Task Alertas_ignoraAlPersonalInactivo_yALosDocumentosSinRegistrar()
        {
            Vigencia(Colaborador("Inactivo", "10000001", activo: false), VigenciaDocumento.Sctr, -10);
            Colaborador("SinDocumentos", "10000002");

            var (alertas, resumen) = await Consultar();

            Assert.Empty(alertas);
            Assert.Equal(0, Valor(resumen, "total"));
        }

        [Fact]
        public async Task Alertas_usaSoloLaVigenciaMasReciente_conUnaAlertaPorDocumento()
        {
            var id = Colaborador("Rómulo", "10000001");
            Vigencia(id, VigenciaDocumento.Sctr, -400);          // historial vencido: no es alerta
            Vigencia(id, VigenciaDocumento.Sctr, 300);           // vigente
            Vigencia(id, VigenciaDocumento.ExamenMedico, 5);     // por vencer
            var otro = Colaborador("Elmer", "10000002");
            Vigencia(otro, VigenciaDocumento.Sctr, 2);
            Vigencia(otro, VigenciaDocumento.ExamenMedico, -2);

            var (alertas, _) = await Consultar();

            Assert.Equal(3, alertas.Count);
            Assert.Equal(1, alertas.Count(a => (int)Valor(a, "trabajadorId") == id));
            Assert.Equal(2, alertas.Count(a => (int)Valor(a, "trabajadorId") == otro));
        }

        // ---- Indicador del panel (T1) ----

        [Fact]
        public async Task ElResumen_cuentaPorVencerYVencidos()
        {
            var a = Colaborador("A", "10000001"); Vigencia(a, VigenciaDocumento.Sctr, -5);
            var b = Colaborador("B", "10000002"); Vigencia(b, VigenciaDocumento.Sctr, 10); Vigencia(b, VigenciaDocumento.ExamenMedico, 25);
            var c = Colaborador("C", "10000003"); Vigencia(c, VigenciaDocumento.Sctr, 120);

            var (_, resumen) = await Consultar();

            Assert.Equal(2, Valor(resumen, "porVencer"));
            Assert.Equal(1, Valor(resumen, "vencidos"));
            Assert.Equal(3, Valor(resumen, "total"));
        }

        // ---- Actualización al renovar (T5) ----

        [Fact]
        public async Task AlRenovar_laAlertaYElIndicadorSeActualizan()
        {
            var id = Colaborador("Saúl", "10000001");
            Vigencia(id, VigenciaDocumento.Sctr, -23);
            Vigencia(id, VigenciaDocumento.ExamenMedico, 10);
            Assert.Equal(2, Valor((await Consultar()).resumen, "total"));

            var renovacion = await _personal.RegistrarVigencia(id, new PersonalController.VigenciaDto
            {
                Tipo = "SCTR", FechaInicio = Hoy, FechaVencimiento = Hoy.AddYears(1), Archivo = Archivos.Formulario("sctr.pdf", Archivos.Pdf())
            });

            Assert.Equal(201, Estado(renovacion));
            var (alertas, resumen) = await Consultar();
            Assert.Equal("Examen Médico", Valor(Assert.Single(alertas), "documento"));
            Assert.Equal(0, Valor(resumen, "vencidos"));
            Assert.Equal(1, Valor(resumen, "porVencer"));
        }

        [Fact]
        public async Task LaRenovacion_hechaAntesDelEnvio_dejaElDocumentoFueraDelCorreo()
        {
            var id = Colaborador("Saúl", "10000001");
            Vigencia(id, VigenciaDocumento.Sctr, -3);
            Vigencia(id, VigenciaDocumento.ExamenMedico, 9);
            await _personal.RegistrarVigencia(id, new PersonalController.VigenciaDto
            {
                Tipo = "SCTR", FechaInicio = Hoy, FechaVencimiento = Hoy.AddYears(1), Archivo = Archivos.Formulario("sctr.pdf", Archivos.Pdf())
            });

            await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);

            var cuerpo = _correo.Enviados[0].Cuerpo;
            Assert.Contains("Examen M", cuerpo);
            Assert.DoesNotContain(">SCTR<", cuerpo);
        }

        // ---- Aviso diario por correo (T4) ----

        [Fact]
        public async Task Envio_avisaATodosLosGerentesActivos_conElDetalleDeLasAlertas()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -23);
            Vigencia(Colaborador("Julio", "10000002"), VigenciaDocumento.ExamenMedico, 14);

            var r = await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Enviado, r.Estado);
            Assert.Equal(2, r.Alertas);
            Assert.Equal(2, r.Enviados);
            Assert.Equal(new[] { GerenteA, GerenteB }, _correo.Enviados.Select(e => e.Para).OrderBy(x => x));
            var correo = _correo.Enviados[0];
            Assert.Equal($"Documentación del personal al {Hoy:dd/MM/yyyy}: 1 vencido, 1 por vencer", correo.Asunto);
            Assert.Contains("Sa&#250;l Prueba", correo.Cuerpo);
            Assert.Contains($"{Hoy.AddDays(-23):dd/MM/yyyy}", correo.Cuerpo);
            Assert.Contains(">-23<", correo.Cuerpo);
            Assert.Contains("Vencido", correo.Cuerpo);
            Assert.Contains("Por vencer", correo.Cuerpo);
        }

        [Fact]
        public async Task Envio_noLlegaAGerentesInactivosNiAOtrosRoles()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);

            await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);

            Assert.DoesNotContain(_correo.Enviados, e => e.Para is "inactivo@mym.pe" or "admin@mym.pe" or "supervisor@mym.pe");
        }

        [Fact]
        public async Task Envio_antesDeLaHoraProgramada_noEnvia()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);

            var r = await _servicio.EnviarSiCorrespondeAsync(A(Hoy, hora: 6), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.AntesDeLaHora, r.Estado);
            Assert.Empty(_correo.Enviados);
            Assert.Empty(await Db.EnviosAlertaDocumental.ToListAsync());
        }

        [Fact]
        public async Task Envio_unaVezPorDia_aunqueSeRevisePorLaTardeOSeReinicieLaAplicacion()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);

            var primero = await _servicio.EnviarSiCorrespondeAsync(A(Hoy, 8), HoraEnvio);
            var tarde = await _servicio.EnviarSiCorrespondeAsync(A(Hoy, 15), HoraEnvio);
            var reinicio = await Servicio(_correo).EnviarSiCorrespondeAsync(A(Hoy, 18), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Enviado, primero.Estado);
            Assert.Equal(ResultadoEnvioAlertas.YaEnviado, tarde.Estado);
            Assert.Equal(ResultadoEnvioAlertas.YaEnviado, reinicio.Estado);
            Assert.Equal(2, _correo.Enviados.Count);                    // un correo por gerente, solo del primer envío
            var registro = await Db.EnviosAlertaDocumental.SingleAsync();
            Assert.Equal(Hoy, registro.Fecha);
            Assert.Equal(1, registro.Alertas);
            Assert.Equal(2, registro.Destinatarios);
        }

        [Fact]
        public async Task Envio_alDiaSiguiente_vuelveAEnviar()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, 10);

            await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);
            var manana = await _servicio.EnviarSiCorrespondeAsync(A(Hoy.AddDays(1)), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Enviado, manana.Estado);
            Assert.Equal(4, _correo.Enviados.Count);
            Assert.Equal(2, await Db.EnviosAlertaDocumental.CountAsync());
        }

        [Fact]
        public async Task Envio_siLaAplicacionEstabaApagadaALaHora_seEnviaAlArrancarDespues()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);

            var r = await _servicio.EnviarSiCorrespondeAsync(A(Hoy, 16), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Enviado, r.Estado);
        }

        [Fact]
        public async Task Envio_sinAlertas_noEnviaCorreo()
        {
            Vigencia(Colaborador("Ana", "10000001"), VigenciaDocumento.Sctr, 200);

            var r = await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.SinAlertas, r.Estado);
            Assert.Empty(_correo.Enviados);
        }

        [Fact]
        public async Task Envio_sinGerenteActivo_noEnviaYLoRegistra()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);
            foreach (var g in Db.Usuarios.Where(u => u.Rol.Nombre == NombresRol.Gerente)) g.Estado = "Inactivo";
            await Db.SaveChangesAsync();

            var r = await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.SinDestinatarios, r.Estado);
            Assert.Equal(1, r.Alertas);
            Assert.Empty(_correo.Enviados);
        }

        [Fact]
        public async Task Envio_siElCorreoFallaConTodos_noSeRegistra_yElSiguienteIntentoLoReenvia()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);
            _correo.Falla.UnionWith(new[] { GerenteA, GerenteB });

            var fallido = await _servicio.EnviarSiCorrespondeAsync(A(Hoy, 8), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Fallido, fallido.Estado);
            Assert.Empty(await Db.EnviosAlertaDocumental.ToListAsync());

            _correo.Falla.Clear();
            var reintento = await _servicio.EnviarSiCorrespondeAsync(A(Hoy, 8).AddMinutes(15), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Enviado, reintento.Estado);
            Assert.Equal(2, _correo.Enviados.Count);
        }

        [Fact]
        public async Task Envio_siFallaConUnGerente_loRecibeElOtro_yQuedaRegistrado()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);
            _correo.Falla.Add(GerenteA);

            var r = await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Enviado, r.Estado);
            Assert.Equal(1, r.Enviados);
            Assert.Equal(GerenteB, Assert.Single(_correo.Enviados).Para);
            Assert.Equal(1, await Db.EnviosAlertaDocumental.CountAsync());
        }

        [Fact]
        public async Task Envio_escapaElNombreDelColaboradorEnElCorreo()
        {
            var t = new Trabajador { Nombres = "<script>alert(1)</script>", Apellidos = "X", Documento = "10000009", Especialidad = "Operario", JornalDiario = 100 };
            Db.Personal.Add(t);
            await Db.SaveChangesAsync();
            Vigencia(t.Id, VigenciaDocumento.Sctr, -1);

            await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);

            Assert.DoesNotContain("<script>", _correo.Enviados[0].Cuerpo);
            Assert.Contains("&lt;script&gt;", _correo.Enviados[0].Cuerpo);
        }

        [Fact]
        public async Task ElUltimoEnvio_apareceEnLaConsultaDeAlertas()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);
            Assert.Null(Valor(((ObjectResult)await _personal.Alertas()).Value!, "ultimoEnvio"));

            await _servicio.EnviarSiCorrespondeAsync(A(Hoy), HoraEnvio);

            var ultimo = Valor(((ObjectResult)await _personal.Alertas()).Value!, "ultimoEnvio");
            Assert.Equal(Hoy, Valor(ultimo, "fecha"));
            Assert.Equal(2, Valor(ultimo, "destinatarios"));
        }

        // ---- Proceso programado (HT-04) ----

        [Theory]
        [InlineData("07:00", 7, 0)]
        [InlineData("6:45", 6, 45)]
        [InlineData(" 19:30 ", 19, 30)]
        [InlineData(null, 7, 0)]
        [InlineData("", 7, 0)]
        [InlineData("mañana", 7, 0)]
        [InlineData("25:00", 7, 0)]
        public void LaHoraConfigurada_seInterpreta_yUnValorInvalidoUsaLasSieteDeLaManana(string? texto, int hora, int minuto)
        {
            Assert.Equal(new TimeOnly(hora, minuto), EnvioDiarioAlertasDocumentales.LeerHora(texto));
        }

        private ServiceProvider Proveedor() => new ServiceCollection()
            .AddSingleton<IEmailService>(_correo)
            .AddScoped(_ => OtroContexto())
            .AddScoped<NotificacionHitoService>()
            .AddScoped<AlertasDocumentalesService>()
            .AddLogging()
            .BuildServiceProvider();

        private static async Task<bool> Esperar(Func<bool> condicion, int milisegundos = 5000)
        {
            for (var t = 0; t < milisegundos && !condicion(); t += 50) await Task.Delay(50);
            return condicion();
        }

        [Fact]
        public async Task ElProcesoProgramado_enviaElAvisoAlArrancar_siYaPasoLaHora()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);
            using var proveedor = Proveedor();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Alertas:HoraEnvio"] = "00:00" }).Build();
            var proceso = new EnvioDiarioAlertasDocumentales(proveedor.GetRequiredService<IServiceScopeFactory>(), config, NullLogger<EnvioDiarioAlertasDocumentales>.Instance);

            await proceso.StartAsync(CancellationToken.None);
            var enviado = await Esperar(() => _correo.Enviados.Count == 2);
            await proceso.StopAsync(CancellationToken.None);

            Assert.True(enviado, "el proceso programado no envió el aviso");
            await using var db = OtroContexto();
            Assert.Equal(1, await db.EnviosAlertaDocumental.CountAsync());
        }

        [Fact]
        public async Task ElProcesoProgramado_deshabilitado_noEnvia()
        {
            Vigencia(Colaborador("Saúl", "10000001"), VigenciaDocumento.Sctr, -1);
            using var proveedor = Proveedor();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Alertas:HoraEnvio"] = "00:00", ["Alertas:Habilitado"] = "false" }).Build();
            var proceso = new EnvioDiarioAlertasDocumentales(proveedor.GetRequiredService<IServiceScopeFactory>(), config, NullLogger<EnvioDiarioAlertasDocumentales>.Instance);

            await proceso.StartAsync(CancellationToken.None);
            await Task.Delay(400);
            await proceso.StopAsync(CancellationToken.None);

            Assert.Empty(_correo.Enviados);
        }
    }
}
