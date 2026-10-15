using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeguridadAutenticacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.RenameColumn(
                name: "Password",
                table: "Usuarios",
                newName: "PasswordHash");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "Usuarios",
                newName: "Password");

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "BloqueoHasta", "Correo", "Estado", "IntentosFallidos", "NombreCompleto", "Password", "Rol", "TokenExpiracion", "TokenRecuperacion" },
                values: new object[,]
                {
                    { 1, null, "marco.zapata@mym.pe", "Activo", 0, "Marco Zapata", "123", "Gerente de Operaciones", null, null },
                    { 2, null, "contacto@delrio.pe", "Activo", 0, "Inversiones Del Río", "123", "Cliente", null, null }
                });
        }
    }
}
