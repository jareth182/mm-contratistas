using System.Globalization;

namespace MymContratistas.Api.Services
{
    // Parámetros estándar de los listados de gastos y pagos (HT-07), todos opcionales:
    //   ?pagina=1&tamano=20&estado=Registrado&partida=Encofrado&q=cemento&desde=2026-08-01&hasta=2026-08-31
    // Las fechas se aceptan como aaaa-mm-dd (campos de fecha del navegador) o dd/mm/aaaa.
    public class FiltroListado
    {
        public const int LargoMaximoTexto = 100;
        private static readonly string[] FormatosFecha = { "yyyy-MM-dd", "dd/MM/yyyy" };
        private static readonly string[] Estados = { "Registrado", "Anulado" };

        public int? Pagina { get; set; }
        public int? Tamano { get; set; }
        public string? Estado { get; set; }
        public string? Partida { get; set; }
        public string? Q { get; set; }
        public string? Desde { get; set; }
        public string? Hasta { get; set; }

        // Texto de búsqueda en minúsculas, o null si no se pidió
        public string? Texto => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim().ToLowerInvariant();
        public string? PartidaElegida => string.IsNullOrWhiteSpace(Partida) ? null : Partida.Trim();
        public string? EstadoElegido => string.IsNullOrWhiteSpace(Estado) ? null : Estados.FirstOrDefault(e => string.Equals(e, Estado.Trim(), StringComparison.OrdinalIgnoreCase));

        // Devuelve el mensaje de error o null si los filtros son válidos
        public string? Validar(out DateTime? desde, out DateTime? hasta)
        {
            desde = hasta = null;
            if (!string.IsNullOrWhiteSpace(Estado) && EstadoElegido == null) return "El estado debe ser Registrado o Anulado.";
            if ((Q?.Trim().Length ?? 0) > LargoMaximoTexto) return $"La búsqueda no puede superar los {LargoMaximoTexto} caracteres.";
            if (!string.IsNullOrWhiteSpace(Desde))
            {
                if (!DateTime.TryParseExact(Desde.Trim(), FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return "La fecha «desde» no es válida (aaaa-mm-dd).";
                desde = d;
            }
            if (!string.IsNullOrWhiteSpace(Hasta))
            {
                if (!DateTime.TryParseExact(Hasta.Trim(), FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var h)) return "La fecha «hasta» no es válida (aaaa-mm-dd).";
                hasta = h;
            }
            if (desde != null && hasta != null && desde > hasta) return "La fecha «desde» no puede ser posterior a «hasta».";
            return null;
        }

        // Las fechas de gastos y pagos se guardan como texto dd/MM/yyyy, que la base de datos no puede comparar:
        // este filtro se aplica en memoria sobre las filas de una sola obra, después de los filtros de base de datos.
        public static bool EnRango(DateTime fecha, DateTime? desde, DateTime? hasta) =>
            (desde == null && hasta == null) || (fecha != DateTime.MinValue && (desde == null || fecha >= desde) && (hasta == null || fecha <= hasta));
    }
}
