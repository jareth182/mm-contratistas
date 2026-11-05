using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;
using Xunit;

namespace MymContratistas.Api.Tests
{
    // Pruebas de HT-04: proceso programado de verificación de vencimientos documentales
    public sealed class ProcesoProgramadoTests : PruebaConBaseDeDatos
    {
        private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Now);
        private static readonly TimeOnly HoraEnvio = new(7, 0);
        private readonly CorreoDePrueba _correo = new();

        public ProcesoProgramadoTests()
        {
            var rol = Db.Roles.Single(r => r.Nombre == NombresRol.Gerente).Id;
            foreach (var correo in new[] { "gerente.a@mym.pe", "gerente.b@mym.pe" })
                Db.Usuarios.Add(new Usuario { Nombres = correo, Correo = correo, RolId = rol, PasswordHash = "x" });
            var t = new Trabajador { Nombres = "Saúl", Apellidos = "Prueba", Documento = "10000001", Especialidad = "Operario", JornalDiario = 150 };
            Db.Personal.Add(t);
            Db.SaveChanges();
            Db.Vigencias.Add(new VigenciaDocumento { TrabajadorId = t.Id, Tipo = VigenciaDocumento.Sctr, FechaInicio = Hoy.AddYears(-1), FechaVencimiento = Hoy.AddDays(-2) });
            Db.SaveChanges();
        }

        private AlertasDocumentalesService Servicio() =>
            new(Db, Notificaciones.Con(Db, _correo), NullLogger<AlertasDocumentalesService>.Instance);

        private static DateTime Ahora(int hora = 8) => Hoy.ToDateTime(new TimeOnly(hora, 0));

        // ---- Configuración ----

        [Theory]
        [InlineData("900", 900)]
        [InlineData("1", 1)]
        [InlineData("0.25", 0.25)]
        [InlineData("0", 0.05)]
        [InlineData("-30", 0.05)]
        [InlineData(null, 900)]
        [InlineData("", 900)]
        [InlineData("cada rato", 900)]
        [InlineData("NaN", 900)]
        [InlineData("Infinity", 900)]
        public void ElIntervalo_seLeeEnSegundos_conUnMinimo_yUnValorInvalidoUsaElPredeterminado(string? texto, double esperado)
        {
            Assert.Equal(TimeSpan.FromSeconds(esperado), EnvioDiarioAlertasDocumentales.LeerIntervalo(texto));
        }

        // ---- Robustez del envío diario ----

        [Fact]
        public async Task SiSePierdeElRegistroDelDia_elReintentoNoDuplicaLosCorreos()
        {
            await Servicio().EnviarSiCorrespondeAsync(Ahora(), HoraEnvio);
            Assert.Equal(2, _correo.Enviados.Count);
            // Simula una caída justo después de enviar y antes de guardar el registro del día
            Db.EnviosAlertaDocumental.RemoveRange(await Db.EnviosAlertaDocumental.ToListAsync());
            await Db.SaveChangesAsync();

            var reintento = await Servicio().EnviarSiCorrespondeAsync(Ahora(9), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Enviado, reintento.Estado);
            Assert.Equal(2, reintento.Enviados);
            Assert.Equal(2, _correo.Enviados.Count);                              // ningún correo repetido
            Assert.Equal(1, await Db.EnviosAlertaDocumental.CountAsync());        // y el día quedó registrado
        }

        [Fact]
        public async Task SiOtraInstanciaRegistraElDiaMientrasSeEnvia_noHayErrorNiDuplicados()
        {
            var primera = true;
            _correo.AntesDeEnviar = () =>
            {
                if (!primera) return;
                primera = false;
                using var otra = OtroContexto();
                otra.EnviosAlertaDocumental.Add(new EnvioAlertaDocumental { Fecha = Hoy, EnviadoEn = DateTime.UtcNow, Alertas = 1, Destinatarios = 2 });
                otra.SaveChanges();
            };

            var resultado = await Servicio().EnviarSiCorrespondeAsync(Ahora(), HoraEnvio);

            Assert.Equal(ResultadoEnvioAlertas.Enviado, resultado.Estado);
            Assert.Equal(1, await OtroContexto().EnviosAlertaDocumental.CountAsync());
            Assert.Equal(2, _correo.Enviados.Count);
        }

        [Fact]
        public async Task DosInstanciasEnviando_aLaVez_soloUnaEnviaCadaCorreo()
        {
            // La segunda instancia encuentra cada envío ya reservado por la primera
            var otraInstancia = new AlertasDocumentalesService(OtroContexto(), Notificaciones.Con(OtroContexto(), _correo), NullLogger<AlertasDocumentalesService>.Instance);
            var ejecutada = false;
            _correo.AntesDeEnviar = () =>
            {
                if (ejecutada) return;
                ejecutada = true;
                // Mientras la primera instancia envía su primer correo, la segunda intenta enviar los mismos avisos
                otraInstancia.EnviarSiCorrespondeAsync(Ahora(), HoraEnvio).GetAwaiter().GetResult();
            };

            await Servicio().EnviarSiCorrespondeAsync(Ahora(), HoraEnvio);

            Assert.Equal(new[] { "gerente.a@mym.pe", "gerente.b@mym.pe" }, _correo.Enviados.Select(e => e.Para).OrderBy(x => x));
        }

        // ---- El proceso en segundo plano ----

        private ServiceProvider Proveedor()
        {
            var servicios = new ServiceCollection()
                .AddSingleton<IEmailService>(_correo)
                .AddScoped<NotificacionService>()
                .AddScoped<NotificacionHitoService>()
                .AddScoped<AlertasDocumentalesService>()
                .AddLogging();
            servicios.AddScoped(_ => OtroContexto());
            return servicios.BuildServiceProvider();
        }

        private static IConfiguration Config(string intervalo = "0.1", string hora = "00:00") =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Alertas:HoraEnvio"] = hora, ["Alertas:IntervaloSegundos"] = intervalo }).Build();

        private static async Task<bool> Esperar(Func<bool> condicion, int milisegundos = 6000)
        {
            for (var t = 0; t < milisegundos && !condicion(); t += 25) await Task.Delay(25);
            return condicion();
        }

        [Fact]
        public async Task ElProceso_variasRevisionesEnElMismoDia_envianUnaSolaVez()
        {
            using var proveedor = Proveedor();
            var proceso = new EnvioDiarioAlertasDocumentales(proveedor.GetRequiredService<IServiceScopeFactory>(), Config(), NullLogger<EnvioDiarioAlertasDocumentales>.Instance);

            await proceso.StartAsync(CancellationToken.None);
            Assert.True(await Esperar(() => _correo.Enviados.Count == 2), "no envió el aviso");
            await Task.Delay(800);                                                // varias revisiones más (cada 0,1 s)
            await proceso.StopAsync(CancellationToken.None);

            Assert.Equal(2, _correo.Enviados.Count);
            await using var db = OtroContexto();
            Assert.Equal(1, await db.EnviosAlertaDocumental.CountAsync());
        }

        [Fact]
        public async Task ElProceso_sobreviveAUnErrorYSigueRevisando()
        {
            var intentos = 0;
            var servicios = new ServiceCollection()
                .AddSingleton<IEmailService>(_correo)
                .AddScoped<NotificacionService>()
                .AddScoped<NotificacionHitoService>()
                .AddScoped<AlertasDocumentalesService>()
                .AddLogging();
            servicios.AddScoped(_ => Interlocked.Increment(ref intentos) <= 2 ? throw new InvalidOperationException("la base de datos no responde") : OtroContexto());
            using var proveedor = servicios.BuildServiceProvider();
            var proceso = new EnvioDiarioAlertasDocumentales(proveedor.GetRequiredService<IServiceScopeFactory>(), Config(), NullLogger<EnvioDiarioAlertasDocumentales>.Instance);

            await proceso.StartAsync(CancellationToken.None);
            var envio = await Esperar(() => _correo.Enviados.Count == 2);
            await proceso.StopAsync(CancellationToken.None);

            Assert.True(envio, "el proceso se detuvo tras el error");
            Assert.True(intentos >= 3);
        }

        [Fact]
        public async Task ElProceso_antesDeLaHora_noEnvia_yAlLlegarLaHoraSiEnvia()
        {
            using var proveedor = Proveedor();
            var horaFutura = TimeOnly.FromDateTime(DateTime.Now).AddHours(1);
            if (horaFutura < TimeOnly.FromDateTime(DateTime.Now)) return;         // cerca de la medianoche no hay una hora posterior hoy

            var proceso = new EnvioDiarioAlertasDocumentales(proveedor.GetRequiredService<IServiceScopeFactory>(), Config(hora: horaFutura.ToString("HH:mm")), NullLogger<EnvioDiarioAlertasDocumentales>.Instance);
            await proceso.StartAsync(CancellationToken.None);
            await Task.Delay(600);
            await proceso.StopAsync(CancellationToken.None);

            Assert.Empty(_correo.Enviados);
            Assert.Empty(await OtroContexto().EnviosAlertaDocumental.ToListAsync());
        }

        [Fact]
        public async Task ElProceso_seDetieneConRapidez_aunConUnIntervaloLargo()
        {
            using var proveedor = Proveedor();
            var proceso = new EnvioDiarioAlertasDocumentales(proveedor.GetRequiredService<IServiceScopeFactory>(), Config(intervalo: "900"), NullLogger<EnvioDiarioAlertasDocumentales>.Instance);

            await proceso.StartAsync(CancellationToken.None);
            await Esperar(() => _correo.Enviados.Count == 2);
            var reloj = Stopwatch.StartNew();
            await proceso.StopAsync(CancellationToken.None);

            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(2), $"tardó {reloj.Elapsed} en detenerse");
        }

        [Fact]
        public async Task ElProceso_conUnaHoraInvalida_usaLasSieteDeLaManana_yNoFalla()
        {
            using var proveedor = Proveedor();
            var proceso = new EnvioDiarioAlertasDocumentales(proveedor.GetRequiredService<IServiceScopeFactory>(), Config(hora: "no-es-hora"), NullLogger<EnvioDiarioAlertasDocumentales>.Instance);

            await proceso.StartAsync(CancellationToken.None);
            await Task.Delay(500);
            await proceso.StopAsync(CancellationToken.None);

            var despuesDeLasSiete = TimeOnly.FromDateTime(DateTime.Now) >= EnvioDiarioAlertasDocumentales.HoraPorDefecto;
            Assert.Equal(despuesDeLasSiete ? 2 : 0, _correo.Enviados.Count);
        }
    }
}
