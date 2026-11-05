using Microsoft.EntityFrameworkCore;

namespace MymContratistas.Api.Services
{
    // Paginación estándar de los listados (HT-07): ?pagina=1&tamano=20, con un máximo de 100 por página
    public static class Paginacion
    {
        public const int TamanoPorDefecto = 20;
        public const int TamanoMaximo = 100;

        public record Pagina<T>(List<T> Items, object Info);

        public static int Tamano(int? tamano) => Math.Clamp(tamano ?? TamanoPorDefecto, 1, TamanoMaximo);

        // Datos de paginación que acompañan a cada listado; una página fuera de rango se lleva a la última
        public static (int Pagina, object Info) Calcular(int total, int? pagina, int? tamano)
        {
            var t = Tamano(tamano);
            var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)t));
            var p = Math.Clamp(pagina ?? 1, 1, totalPaginas);
            return (p, new { pagina = p, tamano = t, total, totalPaginas });
        }

        // En base de datos: un COUNT y un OFFSET/LIMIT
        public static async Task<Pagina<T>> PaginarAsync<T>(IQueryable<T> consulta, int? pagina, int? tamano)
        {
            var total = await consulta.CountAsync();
            var (p, info) = Calcular(total, pagina, tamano);
            var t = Tamano(tamano);
            return new Pagina<T>(await consulta.Skip((p - 1) * t).Take(t).ToListAsync(), info);
        }

        // En memoria: para listados que ordenan o filtran por un dato que la base no puede comparar (p. ej. fechas dd/MM/yyyy guardadas como texto)
        public static Pagina<T> Paginar<T>(IReadOnlyList<T> todos, int? pagina, int? tamano)
        {
            var (p, info) = Calcular(todos.Count, pagina, tamano);
            var t = Tamano(tamano);
            return new Pagina<T>(todos.Skip((p - 1) * t).Take(t).ToList(), info);
        }
    }
}
