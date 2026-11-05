using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class IndicesDeListados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pagos_ObraId",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Hitos_ObraId",
                table: "Hitos");

            migrationBuilder.DropIndex(
                name: "IX_Gastos_ObraId",
                table: "Gastos");

            migrationBuilder.DropIndex(
                name: "IX_ComentariosHito_HitoId",
                table: "ComentariosHito");

            migrationBuilder.DropIndex(
                name: "IX_AvancesHito_HitoId",
                table: "AvancesHito");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ObraId_Estado",
                table: "Pagos",
                columns: new[] { "ObraId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ObraId_PartidaNombre",
                table: "Pagos",
                columns: new[] { "ObraId", "PartidaNombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Hitos_ObraId_Orden",
                table: "Hitos",
                columns: new[] { "ObraId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_ObraId_Estado",
                table: "Gastos",
                columns: new[] { "ObraId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_ObraId_PartidaNombre",
                table: "Gastos",
                columns: new[] { "ObraId", "PartidaNombre" });

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosHito_HitoId_Fecha",
                table: "ComentariosHito",
                columns: new[] { "HitoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_AvancesHito_HitoId_Fecha",
                table: "AvancesHito",
                columns: new[] { "HitoId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pagos_ObraId_Estado",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_ObraId_PartidaNombre",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Hitos_ObraId_Orden",
                table: "Hitos");

            migrationBuilder.DropIndex(
                name: "IX_Gastos_ObraId_Estado",
                table: "Gastos");

            migrationBuilder.DropIndex(
                name: "IX_Gastos_ObraId_PartidaNombre",
                table: "Gastos");

            migrationBuilder.DropIndex(
                name: "IX_ComentariosHito_HitoId_Fecha",
                table: "ComentariosHito");

            migrationBuilder.DropIndex(
                name: "IX_AvancesHito_HitoId_Fecha",
                table: "AvancesHito");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ObraId",
                table: "Pagos",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_Hitos_ObraId",
                table: "Hitos",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_ObraId",
                table: "Gastos",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosHito_HitoId",
                table: "ComentariosHito",
                column: "HitoId");

            migrationBuilder.CreateIndex(
                name: "IX_AvancesHito_HitoId",
                table: "AvancesHito",
                column: "HitoId");
        }
    }
}
