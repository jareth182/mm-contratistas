using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MymContratistas.Api.Migrations
{
    /// <inheritdoc />
    public partial class HitosOrdenYFechas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "Hitos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Los hitos existentes quedan numerados 1, 2, 3... dentro de cada obra
            migrationBuilder.Sql(@"UPDATE ""Hitos"" h SET ""Orden"" = n.rn
                FROM (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""ObraId"" ORDER BY ""Id"") AS rn FROM ""Hitos"") n
                WHERE n.""Id"" = h.""Id"";");

            // Las fechas pasan de texto (DD/MM/YYYY) a tipo fecha conservando los valores válidos
            foreach (var columna in new[] { "FechaPlanificada", "FechaReal" })
            {
                migrationBuilder.Sql($@"ALTER TABLE ""Hitos"" ALTER COLUMN ""{columna}"" TYPE date USING (
                    CASE WHEN ""{columna}"" ~ '^[0-9]{{2}}/[0-9]{{2}}/[0-9]{{4}}$' THEN to_date(""{columna}"", 'DD/MM/YYYY')
                         WHEN ""{columna}"" ~ '^[0-9]{{4}}-[0-9]{{2}}-[0-9]{{2}}$' THEN ""{columna}""::date
                         ELSE NULL END);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var columna in new[] { "FechaPlanificada", "FechaReal" })
            {
                migrationBuilder.Sql($@"ALTER TABLE ""Hitos"" ALTER COLUMN ""{columna}"" TYPE text USING to_char(""{columna}"", 'DD/MM/YYYY');");
            }

            migrationBuilder.DropColumn(
                name: "Orden",
                table: "Hitos");
        }
    }
}
