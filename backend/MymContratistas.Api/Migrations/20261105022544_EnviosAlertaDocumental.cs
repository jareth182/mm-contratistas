using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnviosAlertaDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnviosAlertaDocumental",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    EnviadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Alertas = table.Column<int>(type: "integer", nullable: false),
                    Destinatarios = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosAlertaDocumental", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosAlertaDocumental_Fecha",
                table: "EnviosAlertaDocumental",
                column: "Fecha",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnviosAlertaDocumental");
        }
    }
}
