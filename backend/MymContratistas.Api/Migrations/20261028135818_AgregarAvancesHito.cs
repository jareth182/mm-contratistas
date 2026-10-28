using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAvancesHito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvancesHito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Porcentaje = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Responsable = table.Column<string>(type: "text", nullable: false),
                    HitoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvancesHito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvancesHito_Hitos_HitoId",
                        column: x => x.HitoId,
                        principalTable: "Hitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FotosAvance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvanceHitoId = table.Column<int>(type: "integer", nullable: false),
                    Archivo = table.Column<string>(type: "text", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosAvance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotosAvance_AvancesHito_AvanceHitoId",
                        column: x => x.AvanceHitoId,
                        principalTable: "AvancesHito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvancesHito_HitoId",
                table: "AvancesHito",
                column: "HitoId");

            migrationBuilder.CreateIndex(
                name: "IX_FotosAvance_AvanceHitoId",
                table: "FotosAvance",
                column: "AvanceHitoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosAvance");

            migrationBuilder.DropTable(
                name: "AvancesHito");
        }
    }
}
