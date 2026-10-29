using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class PagosConAnulacionYPartida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Archivo",
                table: "Pagos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ColaboradorId",
                table: "Pagos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAnulacion",
                table: "Pagos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "Pagos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartidaNombre",
                table: "Pagos",
                type: "text",
                nullable: false,
                defaultValue: "Mano de obra");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Archivo",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "ColaboradorId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "FechaAnulacion",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "PartidaNombre",
                table: "Pagos");
        }
    }
}
