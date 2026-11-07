using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectProjectRegistrationsAndCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Cupo",
                table: "PROYECTOS",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PROYECTOS_Cupo",
                table: "PROYECTOS",
                sql: "[Cupo] IS NULL OR [Cupo] > 0");

            migrationBuilder.Sql("""
                UPDATE REGISTROS_PROYECTOS
                SET Estado = 'Aceptado'
                WHERE Estado = 'Pendiente';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PROYECTOS_Cupo",
                table: "PROYECTOS");

            migrationBuilder.DropColumn(
                name: "Cupo",
                table: "PROYECTOS");
        }
    }
}
