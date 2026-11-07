using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateUserInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "INTERESES_EVENTOS",
                columns: table => new
                {
                    IdInteresEvento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdEvento = table.Column<int>(type: "int", nullable: false),
                    FechaInteres = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INTERESES_EVENTOS", x => x.IdInteresEvento);
                    table.ForeignKey(
                        name: "FK_INTERESES_EVENTOS_EVENTOS",
                        column: x => x.IdEvento,
                        principalTable: "EVENTOS",
                        principalColumn: "IdEvento");
                    table.ForeignKey(
                        name: "FK_INTERESES_EVENTOS_USUARIOS",
                        column: x => x.IdUsuario,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "INTERESES_PROYECTOS",
                columns: table => new
                {
                    IdInteresProyecto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdProyecto = table.Column<int>(type: "int", nullable: false),
                    FechaInteres = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INTERESES_PROYECTOS", x => x.IdInteresProyecto);
                    table.ForeignKey(
                        name: "FK_INTERESES_PROYECTOS_PROYECTOS",
                        column: x => x.IdProyecto,
                        principalTable: "PROYECTOS",
                        principalColumn: "IdProyecto");
                    table.ForeignKey(
                        name: "FK_INTERESES_PROYECTOS_USUARIOS",
                        column: x => x.IdUsuario,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateIndex(
                name: "IX_INTERESES_EVENTOS_IdEvento",
                table: "INTERESES_EVENTOS",
                column: "IdEvento");

            migrationBuilder.CreateIndex(
                name: "UQ_INTERESES_EVENTOS_IdUsuario_IdEvento",
                table: "INTERESES_EVENTOS",
                columns: new[] { "IdUsuario", "IdEvento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INTERESES_PROYECTOS_IdProyecto",
                table: "INTERESES_PROYECTOS",
                column: "IdProyecto");

            migrationBuilder.CreateIndex(
                name: "UQ_INTERESES_PROYECTOS_IdUsuario_IdProyecto",
                table: "INTERESES_PROYECTOS",
                columns: new[] { "IdUsuario", "IdProyecto" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INTERESES_EVENTOS");

            migrationBuilder.DropTable(
                name: "INTERESES_PROYECTOS");
        }
    }
}
