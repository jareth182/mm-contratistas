using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDescripcionYFotoHito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescripcionAvance",
                table: "Hitos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoUrl",
                table: "Hitos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescripcionAvance",
                table: "Hitos");

            migrationBuilder.DropColumn(
                name: "FotoUrl",
                table: "Hitos");
        }
    }
}
