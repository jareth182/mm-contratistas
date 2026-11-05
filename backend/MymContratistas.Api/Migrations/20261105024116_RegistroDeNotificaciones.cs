using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class RegistroDeNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Canal = table.Column<string>(type: "text", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    Destinatario = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Mensaje = table.Column<string>(type: "text", nullable: false),
                    ObraId = table.Column<int>(type: "integer", nullable: true),
                    HitoId = table.Column<int>(type: "integer", nullable: true),
                    ClaveDeduplicacion = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnviadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Leida = table.Column<bool>(type: "boolean", nullable: false),
                    LeidaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_ClaveDeduplicacion",
                table: "Notificaciones",
                column: "ClaveDeduplicacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_Estado_Fecha",
                table: "Notificaciones",
                columns: new[] { "Estado", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId_Canal_Leida_Fecha",
                table: "Notificaciones",
                columns: new[] { "UsuarioId", "Canal", "Leida", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notificaciones");
        }
    }
}
