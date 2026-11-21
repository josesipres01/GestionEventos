using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredProceduresInsertarEventosYProyectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!migrationBuilder.ActiveProvider.Contains("SqlServer", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.Sql("""
                ;WITH EventosOrdenados AS (
                    SELECT
                        Orden,
                        ROW_NUMBER() OVER (
                            ORDER BY CASE WHEN Orden <= 0 THEN 2147483647 ELSE Orden END,
                                     FechaHora DESC,
                                     IdEvento
                        ) AS NuevoOrden
                    FROM dbo.EVENTOS
                )
                UPDATE EventosOrdenados
                SET Orden = NuevoOrden
                WHERE EXISTS (SELECT 1 FROM dbo.EVENTOS WHERE Orden <= 0);

                ;WITH ProyectosOrdenados AS (
                    SELECT
                        Orden,
                        ROW_NUMBER() OVER (
                            ORDER BY CASE WHEN Orden <= 0 THEN 2147483647 ELSE Orden END,
                                     FechaPublicacion DESC,
                                     IdProyecto
                        ) AS NuevoOrden
                    FROM dbo.PROYECTOS
                )
                UPDATE ProyectosOrdenados
                SET Orden = NuevoOrden
                WHERE EXISTS (SELECT 1 FROM dbo.PROYECTOS WHERE Orden <= 0);
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.Eventos_Insertar
                    @Nombre nvarchar(150),
                    @Descripcion nvarchar(max),
                    @FechaHora datetime2,
                    @FechaFin datetime2,
                    @Cupo int = NULL,
                    @Lugar nvarchar(150),
                    @ImagenUrl nvarchar(500) = NULL,
                    @IdUsuarioOrganizador int,
                    @IdUsuarioSolicitante int,
                    @EstadoAprobacion nvarchar(20) = N'Pendiente',
                    @FechaSolicitud datetime2,
                    @FechaCreacion datetime2
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM dbo.EVENTOS WHERE Orden <= 0)
                    BEGIN
                        ;WITH EventosOrdenados AS (
                            SELECT
                                Orden,
                                ROW_NUMBER() OVER (
                                    ORDER BY CASE WHEN Orden <= 0 THEN 2147483647 ELSE Orden END,
                                             FechaHora DESC,
                                             IdEvento
                                ) AS NuevoOrden
                            FROM dbo.EVENTOS
                        )
                        UPDATE EventosOrdenados
                        SET Orden = NuevoOrden;
                    END

                    DECLARE @Orden int;
                    SELECT @Orden = COALESCE(MAX(Orden), 0) + 1
                    FROM dbo.EVENTOS;

                    INSERT INTO dbo.EVENTOS (
                        Nombre,
                        Descripcion,
                        FechaHora,
                        FechaFin,
                        Cupo,
                        Lugar,
                        ImagenUrl,
                        Orden,
                        IdUsuarioOrganizador,
                        IdUsuarioSolicitante,
                        Activo,
                        ReactivacionSolicitada,
                        EstadoAprobacion,
                        FechaSolicitud,
                        FechaCreacion
                    )
                    VALUES (
                        @Nombre,
                        @Descripcion,
                        @FechaHora,
                        @FechaFin,
                        @Cupo,
                        @Lugar,
                        @ImagenUrl,
                        @Orden,
                        @IdUsuarioOrganizador,
                        @IdUsuarioSolicitante,
                        1,
                        0,
                        @EstadoAprobacion,
                        @FechaSolicitud,
                        @FechaCreacion
                    );

                    -- SACAMOS su id
                    SELECT CAST(SCOPE_IDENTITY() AS int);
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.Proyectos_Insertar
                    @IdUsuarioCreador int,
                    @IdUsuarioOrganizador int = NULL,
                    @Nombre nvarchar(150),
                    @Descripcion nvarchar(max),
                    @LugarReunion nvarchar(150),
                    @FechaInicio datetime2,
                    @FechaFin datetime2,
                    @Cupo int = NULL,
                    @ImagenUrl nvarchar(500) = NULL,
                    @IdCarrera int,
                    @EstadoAprobacion nvarchar(20) = N'Pendiente',
                    @FechaPublicacion datetime2,
                    @FechaSolicitud datetime2
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM dbo.PROYECTOS WHERE Orden <= 0)
                    BEGIN
                        ;WITH ProyectosOrdenados AS (
                            SELECT
                                Orden,
                                ROW_NUMBER() OVER (
                                    ORDER BY CASE WHEN Orden <= 0 THEN 2147483647 ELSE Orden END,
                                             FechaPublicacion DESC,
                                             IdProyecto
                                ) AS NuevoOrden
                            FROM dbo.PROYECTOS
                        )
                        UPDATE ProyectosOrdenados
                        SET Orden = NuevoOrden;
                    END

                    DECLARE @Orden int;
                    SELECT @Orden = COALESCE(MAX(Orden), 0) + 1
                    FROM dbo.PROYECTOS;

                    INSERT INTO dbo.PROYECTOS (
                        IdUsuarioCreador,
                        IdUsuarioOrganizador,
                        Activo,
                        ReactivacionSolicitada,
                        Nombre,
                        Descripcion,
                        LugarReunion,
                        FechaInicio,
                        FechaFin,
                        Cupo,
                        ImagenUrl,
                        Orden,
                        IdCarrera,
                        FechaPublicacion,
                        FechaSolicitud,
                        EstadoAprobacion
                    )
                    VALUES (
                        @IdUsuarioCreador,
                        @IdUsuarioOrganizador,
                        1,
                        0,
                        @Nombre,
                        @Descripcion,
                        @LugarReunion,
                        @FechaInicio,
                        @FechaFin,
                        @Cupo,
                        @ImagenUrl,
                        @Orden,
                        @IdCarrera,
                        @FechaPublicacion,
                        @FechaSolicitud,
                        @EstadoAprobacion
                    );

                    -- SACAMOS su id
                    SELECT CAST(SCOPE_IDENTITY() AS int);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!migrationBuilder.ActiveProvider.Contains("SqlServer", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.Eventos_Insertar;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.Proyectos_Insertar;");
        }
    }
}
