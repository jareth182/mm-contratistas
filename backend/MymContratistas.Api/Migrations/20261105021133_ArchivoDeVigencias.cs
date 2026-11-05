using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class ArchivoDeVigencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Archivo",
                table: "Vigencias",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ArchivoNombre",
                table: "Vigencias",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Archivo",
                table: "Vigencias");

            migrationBuilder.DropColumn(
                name: "ArchivoNombre",
                table: "Vigencias");
        }
    }
}
