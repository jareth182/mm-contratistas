using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class CreacionTablaGastos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gasto_Obras_ObraId",
                table: "Gasto");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Gasto",
                table: "Gasto");

            migrationBuilder.RenameTable(
                name: "Gasto",
                newName: "Gastos");

            migrationBuilder.RenameIndex(
                name: "IX_Gasto_ObraId",
                table: "Gastos",
                newName: "IX_Gastos_ObraId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Gastos",
                table: "Gastos",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Gastos_Obras_ObraId",
                table: "Gastos",
                column: "ObraId",
                principalTable: "Obras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gastos_Obras_ObraId",
                table: "Gastos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Gastos",
                table: "Gastos");

            migrationBuilder.RenameTable(
                name: "Gastos",
                newName: "Gasto");

            migrationBuilder.RenameIndex(
                name: "IX_Gastos_ObraId",
                table: "Gasto",
                newName: "IX_Gasto_ObraId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Gasto",
                table: "Gasto",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Gasto_Obras_ObraId",
                table: "Gasto",
                column: "ObraId",
                principalTable: "Obras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
