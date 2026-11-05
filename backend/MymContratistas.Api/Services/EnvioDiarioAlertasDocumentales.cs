namespace MymContratistas.Api.Services
{
    // Proceso programado (HT-04): cada cierto tiempo comprueba si toca el aviso diario de documentación del personal (HU24).
    // El estado documental (HU23) no se guarda: se calcula en cada ejecución a partir de las vigencias, de modo que
    // una renovación se refleja de inmediato en las alertas. El proceso solo decide cuándo enviar el aviso del día.
    //
    // Configuración:
    //   Alertas:Habilitado         true por defecto; false desactiva el proceso
    //   Alertas:HoraEnvio          HH:mm, hora del servidor; 07:00 por defecto (un valor inválido usa el predeterminado)
    //   Alertas:IntervaloSegundos  cada cuánto se revisa; 900 (15 min) por defecto
    public class EnvioDiarioAlertasDocumentales : BackgroundService
    {
        public const double IntervaloPorDefectoSegundos = 900;
        public const double IntervaloMinimoSegundos = 0.05;
        public static readonly TimeOnly HoraPorDefecto = new(7, 0);

        private readonly IServiceScopeFactory _alcances;
        private readonly IConfiguration _config;
        private readonly ILogger<EnvioDiarioAlertasDocumentales> _logger;

        public EnvioDiarioAlertasDocumentales(IServiceScopeFactory alcances, IConfiguration config, ILogger<EnvioDiarioAlertasDocumentales> logger)
        {
            _alcances = alcances;
            _config = config;
            _logger = logger;
        }

        public static TimeOnly LeerHora(string? texto) =>
            TimeOnly.TryParseExact(texto?.Trim(), new[] { "HH:mm", "H:mm" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var hora)
                ? hora : HoraPorDefecto;

        public static TimeSpan LeerIntervalo(string? texto) =>
            TimeSpan.FromSeconds(double.TryParse(texto, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var segundos) && double.IsFinite(segundos)
                ? Math.Max(IntervaloMinimoSegundos, segundos) : IntervaloPorDefectoSegundos);

        protected override async Task ExecuteAsync(CancellationToken detener)
        {
            if (!_config.GetValue("Alertas:Habilitado", true))
            {
                _logger.LogInformation("El aviso diario de documentación del personal está deshabilitado (Alertas:Habilitado=false)");
                return;
            }

            var hora = LeerHora(_config["Alertas:HoraEnvio"]);
            var intervalo = LeerIntervalo(_config["Alertas:IntervaloSegundos"]);
            _logger.LogInformation("Aviso diario de documentación del personal programado a las {Hora:HH:mm}, con revisión cada {Intervalo}", hora, intervalo);

            using var reloj = new PeriodicTimer(intervalo);
            do
            {
                try
                {
                    using var alcance = _alcances.CreateScope();
                    var servicio = alcance.ServiceProvider.GetRequiredService<AlertasDocumentalesService>();
                    var resultado = await servicio.EnviarSiCorrespondeAsync(DateTime.Now, hora);
                    if (resultado.Estado is ResultadoEnvioAlertas.Enviado or ResultadoEnvioAlertas.Fallido or ResultadoEnvioAlertas.SinDestinatarios)
                        _logger.LogInformation("Aviso de documentación del personal: {Estado} ({Alertas} alertas, {Enviados} destinatarios)", resultado.Estado, resultado.Alertas, resultado.Enviados);
                    else
                        _logger.LogDebug("Aviso de documentación del personal: {Estado}", resultado.Estado);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Un error puntual (p. ej. la base no responde) no debe detener el proceso: se reintenta en el siguiente ciclo
                    _logger.LogError(ex, "Falló la revisión del aviso diario de documentación del personal");
                }
            }
            while (await reloj.WaitForNextTickAsync(detener));
        }
    }
}
