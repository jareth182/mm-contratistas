namespace MymContratistas.Api.Services
{
    // Cabeceras de seguridad de todas las respuestas de la API (HT-06):
    //  - X-Content-Type-Options: nosniff  → el navegador respeta el tipo de contenido declarado (no interpreta un
    //    archivo subido como HTML o script).
    //  - Referrer-Policy: no-referrer     → las direcciones internas no se filtran a sitios externos.
    //  - Cache-Control: no-store          → solo en peticiones con sesión: lo privado (comprobantes, documentos,
    //    fotos) no queda en cachés compartidas ni en el disco del navegador.
    public class EncabezadosDeSeguridad
    {
        private readonly RequestDelegate _siguiente;

        public EncabezadosDeSeguridad(RequestDelegate siguiente)
        {
            _siguiente = siguiente;
        }

        public Task InvokeAsync(HttpContext contexto)
        {
            var cabeceras = contexto.Response.Headers;
            cabeceras["X-Content-Type-Options"] = "nosniff";
            cabeceras["Referrer-Policy"] = "no-referrer";
            if (contexto.Request.Headers.ContainsKey("Authorization"))
            {
                cabeceras["Cache-Control"] = "no-store";
                cabeceras["Pragma"] = "no-cache";
            }
            return _siguiente(contexto);
        }
    }
}
