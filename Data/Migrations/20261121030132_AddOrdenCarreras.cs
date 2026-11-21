using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdenCarreras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "CARRERAS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            if (!migrationBuilder.ActiveProvider.Contains("SqlServer", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.Sql("""
                ;WITH CarrerasOrdenadas AS (
                    SELECT
                        Orden,
                        ROW_NUMBER() OVER (
                            ORDER BY CASE WHEN Orden <= 0 THEN 2147483647 ELSE Orden END,
                                     Nombre,
                                     IdCarrera
                        ) AS NuevoOrden
                    FROM dbo.CARRERAS
                )
                UPDATE CarrerasOrdenadas
                SET Orden = NuevoOrden
                WHERE EXISTS (SELECT 1 FROM dbo.CARRERAS WHERE Orden <= 0);
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.Carreras_Insertar
                    @Nombre nvarchar(120)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM dbo.CARRERAS WHERE Orden <= 0)
                    BEGIN
                        ;WITH CarrerasOrdenadas AS (
                            SELECT
                                Orden,
                                ROW_NUMBER() OVER (
                                    ORDER BY CASE WHEN Orden <= 0 THEN 2147483647 ELSE Orden END,
                                             Nombre,
                                             IdCarrera
                                ) AS NuevoOrden
                            FROM dbo.CARRERAS
                        )
                        UPDATE CarrerasOrdenadas
                        SET Orden = NuevoOrden;
                    END

                    DECLARE @Orden int;
                    SELECT @Orden = COALESCE(MAX(Orden), 0) + 1
                    FROM dbo.CARRERAS;

                    INSERT INTO dbo.CARRERAS (Nombre, Orden)
                    VALUES (@Nombre, @Orden);

                    -- SACAMOS su id
                    SELECT CAST(SCOPE_IDENTITY() AS int);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.Contains("SqlServer", System.StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.Carreras_Insertar;");
            }

            migrationBuilder.DropColumn(
                name: "Orden",
                table: "CARRERAS");
        }
    }
}
