using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProyectoRelacionadoConServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServicioId",
                table: "Portafolio",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Portafolio_ServicioId",
                table: "Portafolio",
                column: "ServicioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Portafolio_Servicios_ServicioId",
                table: "Portafolio",
                column: "ServicioId",
                principalTable: "Servicios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Portafolio_Servicios_ServicioId",
                table: "Portafolio");

            migrationBuilder.DropIndex(
                name: "IX_Portafolio_ServicioId",
                table: "Portafolio");

            migrationBuilder.DropColumn(
                name: "ServicioId",
                table: "Portafolio");
        }
    }
}
