using System.Reflection;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;
using Xunit;

// Varias pruebas cambian el directorio de trabajo (los controladores guardan archivos en ./Uploads)
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MymContratistas.Api.Tests
{
    // Base de datos SQLite en memoria y carpeta de trabajo temporal, una por prueba
    public abstract class PruebaConBaseDeDatos : IDisposable
    {
        private readonly SqliteConnection _conexion;
        private readonly string _directorioOriginal;
        protected readonly ApplicationDbContext Db;
        protected readonly string Carpeta;

        protected PruebaConBaseDeDatos()
        {
            _conexion = new SqliteConnection("DataSource=:memory:");
            _conexion.Open();
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);
            Db.Database.EnsureCreated();

            _directorioOriginal = Directory.GetCurrentDirectory();
            Carpeta = Path.Combine(Path.GetTempPath(), "mym-pruebas-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Carpeta);
            Directory.SetCurrentDirectory(Carpeta);
        }

        public virtual void Dispose()
        {
            Directory.SetCurrentDirectory(_directorioOriginal);
            try { Directory.Delete(Carpeta, true); } catch (IOException) { }
            Db.Dispose();
            _conexion.Dispose();
        }

        // Contexto nuevo sobre la misma base, para leer lo que realmente quedó guardado
        protected ApplicationDbContext OtroContexto() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);
    }

    public static class Respuesta
    {
        public static int Estado(IActionResult r) => r switch
        {
            ObjectResult o => o.StatusCode ?? 200,
            StatusCodeResult s => s.StatusCode,
            _ => 200
        };

        // Lee una propiedad del objeto anónimo que devuelven los controladores
        public static T Dato<T>(IActionResult resultado, string propiedad)
        {
            var valor = (resultado as ObjectResult)?.Value ?? throw new InvalidOperationException("Sin cuerpo en la respuesta");
            var info = valor.GetType().GetProperty(propiedad, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"La respuesta no tiene «{propiedad}»");
            return (T)info.GetValue(valor)!;
        }

        public static object Valor(object anonimo, string propiedad) =>
            anonimo.GetType().GetProperty(propiedad)!.GetValue(anonimo)!;

        public static List<object> Lista(IActionResult r) =>
            ((System.Collections.IEnumerable)((ObjectResult)r).Value!).Cast<object>().ToList();
    }

    public static class Archivos
    {
        public static IFormFile Formulario(string nombre, byte[] contenido) =>
            new FormFile(new MemoryStream(contenido), 0, contenido.Length, "archivos", nombre);

        // PNG válido de 1x1; "relleno" permite simular archivos grandes
        public static byte[] Png(int relleno = 0)
        {
            static byte[] Trozo(string tipo, byte[] datos)
            {
                var t = System.Text.Encoding.ASCII.GetBytes(tipo);
                var largo = BitConverter.GetBytes(datos.Length); Array.Reverse(largo);
                var crc = BitConverter.GetBytes(Crc32(t.Concat(datos).ToArray())); Array.Reverse(crc);
                return largo.Concat(t).Concat(datos).Concat(crc).ToArray();
            }
            using var comprimido = new MemoryStream();
            using (var z = new ZLibStream(comprimido, CompressionLevel.Optimal, true)) z.Write(new byte[] { 0, 255, 0, 0 });
            var ihdr = new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0 };
            return new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
                .Concat(Trozo("IHDR", ihdr)).Concat(Trozo("IDAT", comprimido.ToArray())).Concat(Trozo("IEND", Array.Empty<byte>()))
                .Concat(new byte[relleno]).ToArray();
        }

        public static byte[] Jpg() => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };

        public static byte[] Pdf() => System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%prueba\n");

        private static uint Crc32(byte[] datos)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in datos)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }
    }
}
