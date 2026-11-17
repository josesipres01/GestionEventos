using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdenEventosYProyectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "PROYECTOS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "EVENTOS",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Orden",
                table: "PROYECTOS");

            migrationBuilder.DropColumn(
                name: "Orden",
                table: "EVENTOS");
        }
    }
}
