namespace MymContratistas.Api.Services
{
    // Arma la ruta de un archivo guardado a partir del nombre que consta en la base de datos (HT-06).
    // Solo se usa el nombre del archivo, nunca carpetas ni «..»: aunque un registro tuviera un valor manipulado,
    // la ruta no puede salir de la carpeta de archivos (ni para leer, ni para descargar, ni para borrar).
    public static class RutasSeguras
    {
        public static string Archivo(string carpeta, string? nombre) =>
            Path.Combine(carpeta, Path.GetFileName(nombre ?? string.Empty));
    }
}
