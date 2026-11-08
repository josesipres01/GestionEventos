using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ASISTENCIAS_EVENTOS",
                columns: table => new
                {
                    IdAsistenciaEvento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdEvento = table.Column<int>(type: "int", nullable: false),
                    FechaAsistencia = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASISTENCIAS_EVENTOS", x => x.IdAsistenciaEvento);
                    table.ForeignKey(
                        name: "FK_ASISTENCIAS_EVENTOS_EVENTOS",
                        column: x => x.IdEvento,
                        principalTable: "EVENTOS",
                        principalColumn: "IdEvento");
                    table.ForeignKey(
                        name: "FK_ASISTENCIAS_EVENTOS_USUARIOS",
                        column: x => x.IdUsuario,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ASISTENCIAS_EVENTOS_IdEvento",
                table: "ASISTENCIAS_EVENTOS",
                column: "IdEvento");

            migrationBuilder.CreateIndex(
                name: "UQ_ASISTENCIAS_EVENTOS_IdUsuario_IdEvento",
                table: "ASISTENCIAS_EVENTOS",
                columns: new[] { "IdUsuario", "IdEvento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ASISTENCIAS_EVENTOS");
        }
    }
}
