using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortableCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_REGISTROS_PROYECTOS_Estado",
                table: "REGISTROS_PROYECTOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PROYECTOS_EstadoAprobacion",
                table: "PROYECTOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EVENTOS_EstadoAprobacion",
                table: "EVENTOS");

            migrationBuilder.AddCheckConstraint(
                name: "CK_REGISTROS_PROYECTOS_Estado",
                table: "REGISTROS_PROYECTOS",
                sql: "[Estado] IN ('Pendiente', 'Aceptado', 'Rechazado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PROYECTOS_EstadoAprobacion",
                table: "PROYECTOS",
                sql: "[EstadoAprobacion] IN ('Pendiente', 'Aprobado', 'Rechazado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EVENTOS_EstadoAprobacion",
                table: "EVENTOS",
                sql: "[EstadoAprobacion] IN ('Pendiente', 'Aprobado', 'Rechazado')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_REGISTROS_PROYECTOS_Estado",
                table: "REGISTROS_PROYECTOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PROYECTOS_EstadoAprobacion",
                table: "PROYECTOS");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EVENTOS_EstadoAprobacion",
                table: "EVENTOS");

            migrationBuilder.AddCheckConstraint(
                name: "CK_REGISTROS_PROYECTOS_Estado",
                table: "REGISTROS_PROYECTOS",
                sql: "[Estado] IN (N'Pendiente', N'Aceptado', N'Rechazado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PROYECTOS_EstadoAprobacion",
                table: "PROYECTOS",
                sql: "[EstadoAprobacion] IN (N'Pendiente', N'Aprobado', N'Rechazado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EVENTOS_EstadoAprobacion",
                table: "EVENTOS",
                sql: "[EstadoAprobacion] IN (N'Pendiente', N'Aprobado', N'Rechazado')");
        }
    }
}
