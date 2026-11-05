using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CARRERAS",
                columns: table => new
                {
                    IdCarrera = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CARRERAS", x => x.IdCarrera);
                });

            migrationBuilder.CreateTable(
                name: "ROLES",
                columns: table => new
                {
                    IdRol = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLES", x => x.IdRol);
                });

            migrationBuilder.CreateTable(
                name: "USUARIOS",
                columns: table => new
                {
                    IdUsuario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombres = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IdRol = table.Column<int>(type: "int", nullable: false),
                    IdCarrera = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USUARIOS", x => x.IdUsuario);
                    table.ForeignKey(
                        name: "FK_USUARIOS_CARRERAS_IdCarrera",
                        column: x => x.IdCarrera,
                        principalTable: "CARRERAS",
                        principalColumn: "IdCarrera");
                    table.ForeignKey(
                        name: "FK_USUARIOS_ROLES_IdRol",
                        column: x => x.IdRol,
                        principalTable: "ROLES",
                        principalColumn: "IdRol",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EVENTOS",
                columns: table => new
                {
                    IdEvento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Lugar = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ImagenUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdUsuarioOrganizador = table.Column<int>(type: "int", nullable: false),
                    IdUsuarioSolicitante = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    ReactivacionSolicitada = table.Column<bool>(type: "bit", nullable: false),
                    FechaSolicitudReactivacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoEliminacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaEliminacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstadoAprobacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ComentarioAprobacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaRevision = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTOS", x => x.IdEvento);
                    table.ForeignKey(
                        name: "FK_EVENTOS_USUARIOS_IdUsuarioOrganizador",
                        column: x => x.IdUsuarioOrganizador,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                    table.ForeignKey(
                        name: "FK_EVENTOS_USUARIOS_IdUsuarioSolicitante",
                        column: x => x.IdUsuarioSolicitante,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "PROYECTOS",
                columns: table => new
                {
                    IdProyecto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdUsuarioResponsable = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    ReactivacionSolicitada = table.Column<bool>(type: "bit", nullable: false),
                    FechaSolicitudReactivacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoEliminacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaEliminacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NombreResponsable = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LugarReunion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImagenUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdCarrera = table.Column<int>(type: "int", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstadoAprobacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ComentarioAprobacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaRevision = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROYECTOS", x => x.IdProyecto);
                    table.ForeignKey(
                        name: "FK_PROYECTOS_CARRERAS_IdCarrera",
                        column: x => x.IdCarrera,
                        principalTable: "CARRERAS",
                        principalColumn: "IdCarrera",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PROYECTOS_USUARIOS_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                    table.ForeignKey(
                        name: "FK_PROYECTOS_USUARIOS_IdUsuarioResponsable",
                        column: x => x.IdUsuarioResponsable,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "REGISTROS",
                columns: table => new
                {
                    IdRegistro = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdEvento = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGISTROS", x => x.IdRegistro);
                    table.ForeignKey(
                        name: "FK_REGISTROS_EVENTOS_IdEvento",
                        column: x => x.IdEvento,
                        principalTable: "EVENTOS",
                        principalColumn: "IdEvento");
                    table.ForeignKey(
                        name: "FK_REGISTROS_USUARIOS_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateTable(
                name: "NOTIFICACIONES",
                columns: table => new
                {
                    IdNotificacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdEvento = table.Column<int>(type: "int", nullable: true),
                    IdProyecto = table.Column<int>(type: "int", nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Leida = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTIFICACIONES", x => x.IdNotificacion);
                    table.ForeignKey(
                        name: "FK_NOTIFICACIONES_EVENTOS_IdEvento",
                        column: x => x.IdEvento,
                        principalTable: "EVENTOS",
                        principalColumn: "IdEvento");
                    table.ForeignKey(
                        name: "FK_NOTIFICACIONES_PROYECTOS_IdProyecto",
                        column: x => x.IdProyecto,
                        principalTable: "PROYECTOS",
                        principalColumn: "IdProyecto");
                    table.ForeignKey(
                        name: "FK_NOTIFICACIONES_USUARIOS_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "REGISTROS_PROYECTOS",
                columns: table => new
                {
                    IdRegistroProyecto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdProyecto = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGISTROS_PROYECTOS", x => x.IdRegistroProyecto);
                    table.ForeignKey(
                        name: "FK_REGISTROS_PROYECTOS_PROYECTOS_IdProyecto",
                        column: x => x.IdProyecto,
                        principalTable: "PROYECTOS",
                        principalColumn: "IdProyecto",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_REGISTROS_PROYECTOS_USUARIOS_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "USUARIOS",
                        principalColumn: "IdUsuario");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_IdUsuarioOrganizador",
                table: "EVENTOS",
                column: "IdUsuarioOrganizador");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTOS_IdUsuarioSolicitante",
                table: "EVENTOS",
                column: "IdUsuarioSolicitante");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICACIONES_IdEvento",
                table: "NOTIFICACIONES",
                column: "IdEvento");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICACIONES_IdProyecto",
                table: "NOTIFICACIONES",
                column: "IdProyecto");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICACIONES_IdUsuario",
                table: "NOTIFICACIONES",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_PROYECTOS_IdCarrera",
                table: "PROYECTOS",
                column: "IdCarrera");

            migrationBuilder.CreateIndex(
                name: "IX_PROYECTOS_IdUsuario",
                table: "PROYECTOS",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_PROYECTOS_IdUsuarioResponsable",
                table: "PROYECTOS",
                column: "IdUsuarioResponsable");

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_IdEvento",
                table: "REGISTROS",
                column: "IdEvento");

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_IdUsuario_IdEvento",
                table: "REGISTROS",
                columns: new[] { "IdUsuario", "IdEvento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_PROYECTOS_IdProyecto",
                table: "REGISTROS_PROYECTOS",
                column: "IdProyecto");

            migrationBuilder.CreateIndex(
                name: "IX_REGISTROS_PROYECTOS_IdUsuario_IdProyecto",
                table: "REGISTROS_PROYECTOS",
                columns: new[] { "IdUsuario", "IdProyecto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USUARIOS_IdCarrera",
                table: "USUARIOS",
                column: "IdCarrera");

            migrationBuilder.CreateIndex(
                name: "IX_USUARIOS_IdRol",
                table: "USUARIOS",
                column: "IdRol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NOTIFICACIONES");

            migrationBuilder.DropTable(
                name: "REGISTROS");

            migrationBuilder.DropTable(
                name: "REGISTROS_PROYECTOS");

            migrationBuilder.DropTable(
                name: "EVENTOS");

            migrationBuilder.DropTable(
                name: "PROYECTOS");

            migrationBuilder.DropTable(
                name: "USUARIOS");

            migrationBuilder.DropTable(
                name: "CARRERAS");

            migrationBuilder.DropTable(
                name: "ROLES");
        }
    }
}
