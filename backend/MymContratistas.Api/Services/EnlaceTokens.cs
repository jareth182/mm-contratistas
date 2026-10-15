using System.Security.Cryptography;
using System.Text;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    // Tokens de un solo uso para recuperar o definir la contraseña. En la BD solo se guarda su hash.
    public static class EnlaceTokens
    {
        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        // Asigna un token nuevo al usuario y devuelve el valor que viaja en el enlace del correo
        public static string Generar(Usuario usuario, TimeSpan vigencia)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            usuario.TokenRecuperacion = Hash(token);
            usuario.TokenExpiracion = DateTime.UtcNow.Add(vigencia);
            return token;
        }
    }
}
