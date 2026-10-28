using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class DescripcionDePartidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "Partidas",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "Partidas");
        }
    }
}
