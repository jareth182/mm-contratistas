using Microsoft.EntityFrameworkCore;
using MymContratistas.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 1. Agregar política CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 2. Aplicar CORS antes de las rutas
app.UseCors("PermitirTodo");

app.UseHttpsRedirection();
app.MapControllers();
app.Run();