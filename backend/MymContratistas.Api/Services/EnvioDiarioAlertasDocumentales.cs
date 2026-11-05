namespace MymContratistas.Api.Services
{
    // Proceso programado (HT-04): revisa cada 15 minutos si toca enviar el aviso diario de documentación (HU24).
    // Configuración: Alertas:HoraEnvio (por defecto 07:00, hora del servidor) y Alertas:Habilitado (por defecto true).
    public class EnvioDiarioAlertasDocumentales : BackgroundService
    {
        public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);
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

        protected override async Task ExecuteAsync(CancellationToken detener)
        {
            if (!_config.GetValue("Alertas:Habilitado", true))
            {
                _logger.LogInformation("El aviso diario de documentación del personal está deshabilitado (Alertas:Habilitado=false)");
                return;
            }

            var hora = LeerHora(_config["Alertas:HoraEnvio"]);
            _logger.LogInformation("Aviso diario de documentación del personal programado a las {Hora:HH:mm}", hora);

            using var reloj = new PeriodicTimer(Intervalo);
            do
            {
                try
                {
                    using var alcance = _alcances.CreateScope();
                    var servicio = alcance.ServiceProvider.GetRequiredService<AlertasDocumentalesService>();
                    var resultado = await servicio.EnviarSiCorrespondeAsync(DateTime.Now, hora);
                    if (resultado.Estado is ResultadoEnvioAlertas.Enviado or ResultadoEnvioAlertas.Fallido)
                        _logger.LogInformation("Aviso de documentación del personal: {Estado} ({Alertas} alertas, {Enviados} destinatarios)", resultado.Estado, resultado.Alertas, resultado.Enviados);
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
