using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    // Consulta de la documentación obligatoria del personal; la usan la asignación a cuadrillas y el control documental
    public static class VigenciasDocumentales
    {
        // Para cada colaborador, la última vigencia registrada de cada tipo (la de mayor vencimiento)
        public static async Task<Dictionary<int, Dictionary<string, VigenciaDocumento>>> UltimasAsync(ApplicationDbContext db, IReadOnlyCollection<int> trabajadorIds)
        {
            var vigencias = await db.Vigencias.Where(v => trabajadorIds.Contains(v.TrabajadorId)).ToListAsync();
            return vigencias
                .GroupBy(v => v.TrabajadorId)
                .ToDictionary(g => g.Key, g => g.GroupBy(v => v.Tipo)
                    .ToDictionary(t => t.Key, t => t.OrderByDescending(v => v.FechaVencimiento).ThenByDescending(v => v.Id).First()));
        }

        public const string SinRegistrar = "Sin registrar";

        // Estado de un documento del colaborador según su última vigencia: Vigente, Por vencer, Vencido o Sin registrar
        public static object Resumen(IReadOnlyDictionary<string, VigenciaDocumento>? ultimas, string tipo, DateOnly hoy) =>
            ultimas != null && ultimas.TryGetValue(tipo, out var v)
                ? new { estado = VigenciaDocumento.EstadoAl(v.FechaVencimiento, hoy), vencimiento = (DateOnly?)v.FechaVencimiento, diasRestantes = (int?)(v.FechaVencimiento.DayNumber - hoy.DayNumber) }
                : new { estado = SinRegistrar, vencimiento = (DateOnly?)null, diasRestantes = (int?)null };

        // Estado documental del colaborador: el más desfavorable de sus documentos (Vencido, Sin registrar, Por vencer o Vigente)
        public static string EstadoGeneral(IReadOnlyDictionary<string, VigenciaDocumento>? ultimas, DateOnly hoy)
        {
            var estados = VigenciaDocumento.Tipos
                .Select(t => ultimas != null && ultimas.TryGetValue(t, out var v) ? VigenciaDocumento.EstadoAl(v.FechaVencimiento, hoy) : SinRegistrar)
                .ToList();
            foreach (var peor in new[] { "Vencido", SinRegistrar, "Por vencer" })
                if (estados.Contains(peor)) return peor;
            return "Vigente";
        }

        // Mensaje que impide asignar al colaborador, o null si tiene SCTR y examen médico vigentes
        public static string? Impedimento(string nombre, IReadOnlyDictionary<string, VigenciaDocumento>? ultimas, DateOnly hoy)
        {
            var motivos = new List<string>();
            foreach (var tipo in VigenciaDocumento.Tipos)
            {
                if (ultimas == null || !ultimas.TryGetValue(tipo, out var vigencia))
                    motivos.Add($"no tiene {tipo} registrado");
                else if (vigencia.FechaVencimiento < hoy)
                    motivos.Add($"su {tipo} venció el {vigencia.FechaVencimiento:dd/MM/yyyy}");
            }
            return motivos.Count == 0 ? null : $"{nombre} no puede asignarse: {string.Join(" y ", motivos)}.";
        }
    }
}
