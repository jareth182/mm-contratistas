using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTablaUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Colaboradores_Obras_ObraId",
                table: "Colaboradores");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Colaboradores",
                table: "Colaboradores");

            migrationBuilder.RenameTable(
                name: "Colaboradores",
                newName: "Cuadrilla");

            migrationBuilder.RenameIndex(
                name: "IX_Colaboradores_ObraId",
                table: "Cuadrilla",
                newName: "IX_Cuadrilla_ObraId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cuadrilla",
                table: "Cuadrilla",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreCompleto = table.Column<string>(type: "text", nullable: false),
                    Correo = table.Column<string>(type: "text", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "integer", nullable: false),
                    BloqueoHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "BloqueoHasta", "Correo", "Estado", "IntentosFallidos", "NombreCompleto", "Password", "Rol" },
                values: new object[,]
                {
                    { 1, null, "marco.zapata@mym.pe", "Activo", 0, "Marco Zapata", "123", "Gerente de Operaciones" },
                    { 2, null, "contacto@delrio.pe", "Activo", 0, "Inversiones Del Río", "123", "Cliente" }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Cuadrilla_Obras_ObraId",
                table: "Cuadrilla",
                column: "ObraId",
                principalTable: "Obras",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cuadrilla_Obras_ObraId",
                table: "Cuadrilla");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Cuadrilla",
                table: "Cuadrilla");

            migrationBuilder.RenameTable(
                name: "Cuadrilla",
                newName: "Colaboradores");

            migrationBuilder.RenameIndex(
                name: "IX_Cuadrilla_ObraId",
                table: "Colaboradores",
                newName: "IX_Colaboradores_ObraId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Colaboradores",
                table: "Colaboradores",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Colaboradores_Obras_ObraId",
                table: "Colaboradores",
                column: "ObraId",
                principalTable: "Obras",
                principalColumn: "Id");
        }
    }
}
