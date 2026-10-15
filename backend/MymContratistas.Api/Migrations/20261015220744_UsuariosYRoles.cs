using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class UsuariosYRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { 1, "Administrador" },
                    { 2, "Gerente de Operaciones" },
                    { 3, "Supervisor de Obra" },
                    { 4, "Cliente" }
                });

            migrationBuilder.AddColumn<string>(name: "Nombres", table: "Usuarios", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Apellidos", table: "Usuarios", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<int>(name: "RolId", table: "Usuarios", type: "integer", nullable: false, defaultValue: 0);

            // Conserva los datos existentes: rol por nombre y nombre completo dividido en nombres / apellidos
            migrationBuilder.Sql(@"UPDATE ""Usuarios"" u SET ""RolId"" = r.""Id"" FROM ""Roles"" r WHERE r.""Nombre"" = u.""Rol"";");
            migrationBuilder.Sql(@"UPDATE ""Usuarios"" SET ""Nombres"" = split_part(btrim(""NombreCompleto""), ' ', 1),
                ""Apellidos"" = btrim(substr(btrim(""NombreCompleto""), length(split_part(btrim(""NombreCompleto""), ' ', 1)) + 1));");
            migrationBuilder.Sql(@"UPDATE ""Usuarios"" SET ""RolId"" = 4 WHERE ""RolId"" = 0;");

            migrationBuilder.DropColumn(name: "Rol", table: "Usuarios");
            migrationBuilder.DropColumn(name: "NombreCompleto", table: "Usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RolId",
                table: "Usuarios",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Nombre",
                table: "Roles",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Roles_RolId",
                table: "Usuarios",
                column: "RolId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Usuarios_Roles_RolId", table: "Usuarios");
            migrationBuilder.DropIndex(name: "IX_Usuarios_RolId", table: "Usuarios");

            migrationBuilder.AddColumn<string>(name: "Rol", table: "Usuarios", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "NombreCompleto", table: "Usuarios", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.Sql(@"UPDATE ""Usuarios"" u SET ""Rol"" = r.""Nombre"" FROM ""Roles"" r WHERE r.""Id"" = u.""RolId"";");
            migrationBuilder.Sql(@"UPDATE ""Usuarios"" SET ""NombreCompleto"" = btrim(""Nombres"" || ' ' || ""Apellidos"");");

            migrationBuilder.DropColumn(name: "RolId", table: "Usuarios");
            migrationBuilder.DropColumn(name: "Nombres", table: "Usuarios");
            migrationBuilder.DropColumn(name: "Apellidos", table: "Usuarios");
            migrationBuilder.DropTable(name: "Roles");
        }
    }
}
