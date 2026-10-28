using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MymContratistas.Api.Data;
using MymContratistas.Api.Models;
using MymContratistas.Api.Services;

// Carga el archivo .env (si existe) antes de construir la configuración
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 1. Agregar política CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition");
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Autenticación con JWT
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<InvitacionService>();
builder.Services.AddScoped<NotificacionHitoService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenService.ObtenerClave(builder.Configuration),
            ClockSkew = TimeSpan.Zero
        };

        // Un usuario desactivado pierde el acceso aunque su token no haya vencido
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async contexto =>
            {
                var idTexto = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var db = contexto.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var rolToken = contexto.Principal?.FindFirstValue(ClaimTypes.Role);
                var vigente = int.TryParse(idTexto, out var id)
                    && await db.Usuarios.AnyAsync(u => u.Id == id && u.Estado == "Activo" && u.Rol.Nombre == rolToken);
                // Si el usuario se desactivó o cambió de rol, debe iniciar sesión de nuevo
                if (!vigente) contexto.Fail("Usuario inactivo, inexistente o con rol modificado.");
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

var app = builder.Build();

// Usuarios iniciales (solo si la tabla está vacía y hay contraseña configurada)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
    DbSeeder.SembrarUsuarios(context, hasher, builder.Configuration["Seed:Password"]);
    DbSeeder.SembrarServicios(context);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 3. Aplicar CORS antes de las rutas
app.UseCors("PermitirTodo");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
