using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class ComentariosDeHito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComentariosHito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AutorUsuarioId = table.Column<int>(type: "integer", nullable: false),
                    AutorNombre = table.Column<string>(type: "text", nullable: false),
                    EsPersonal = table.Column<bool>(type: "boolean", nullable: false),
                    ComentarioPadreId = table.Column<int>(type: "integer", nullable: true),
                    HitoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComentariosHito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComentariosHito_ComentariosHito_ComentarioPadreId",
                        column: x => x.ComentarioPadreId,
                        principalTable: "ComentariosHito",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComentariosHito_Hitos_HitoId",
                        column: x => x.HitoId,
                        principalTable: "Hitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosHito_ComentarioPadreId",
                table: "ComentariosHito",
                column: "ComentarioPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosHito_HitoId",
                table: "ComentariosHito",
                column: "HitoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComentariosHito");
        }
    }
}
