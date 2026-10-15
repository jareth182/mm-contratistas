using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class PersonalDeObra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrabajadorId",
                table: "Cuadrilla",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Personal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombres = table.Column<string>(type: "text", nullable: false),
                    Apellidos = table.Column<string>(type: "text", nullable: false),
                    TipoDocumento = table.Column<string>(type: "text", nullable: false),
                    Documento = table.Column<string>(type: "text", nullable: false),
                    Especialidad = table.Column<string>(type: "text", nullable: false),
                    Telefono = table.Column<string>(type: "text", nullable: false),
                    JornalDiario = table.Column<decimal>(type: "numeric", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personal", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cuadrilla_TrabajadorId",
                table: "Cuadrilla",
                column: "TrabajadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Personal_Documento",
                table: "Personal",
                column: "Documento",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cuadrilla_Personal_TrabajadorId",
                table: "Cuadrilla",
                column: "TrabajadorId",
                principalTable: "Personal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cuadrilla_Personal_TrabajadorId",
                table: "Cuadrilla");

            migrationBuilder.DropTable(
                name: "Personal");

            migrationBuilder.DropIndex(
                name: "IX_Cuadrilla_TrabajadorId",
                table: "Cuadrilla");

            migrationBuilder.DropColumn(
                name: "TrabajadorId",
                table: "Cuadrilla");
        }
    }
}
