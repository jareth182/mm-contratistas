using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MymContratistas.Api.Models;

namespace MymContratistas.Api.Services
{
    public class TokenService
    {
        public const int MinutosVigencia = 30;

        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public static SymmetricSecurityKey ObtenerClave(IConfiguration config)
        {
            var clave = config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(clave) || clave.Length < 32)
                throw new InvalidOperationException("Falta Jwt__Key en el .env (mínimo 32 caracteres).");
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave));
        }

        public (string Token, DateTime Expira) Generar(Usuario usuario)
        {
            var expira = DateTime.UtcNow.AddMinutes(MinutosVigencia);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.NombreCompleto),
                new Claim(ClaimTypes.Email, usuario.Correo),
                new Claim(ClaimTypes.Role, usuario.Rol.Nombre)
            };

            var credenciales = new SigningCredentials(ObtenerClave(_config), SecurityAlgorithms.HmacSha256);
            var jwt = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expira,
                signingCredentials: credenciales);

            return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
        }
    }
}
