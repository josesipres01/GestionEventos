using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreventRoleDeleteCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @dropRoleForeignKeys nvarchar(max) = N'';
                SELECT @dropRoleForeignKeys +=
                    N'ALTER TABLE dbo.USUARIOS DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                INNER JOIN sys.columns columnInfo
                    ON columnInfo.object_id = fkc.parent_object_id
                   AND columnInfo.column_id = fkc.parent_column_id
                WHERE fk.parent_object_id = OBJECT_ID(N'dbo.USUARIOS')
                  AND fk.referenced_object_id = OBJECT_ID(N'dbo.ROLES')
                  AND columnInfo.name = N'IdRol';
                IF @dropRoleForeignKeys <> N'' EXEC sys.sp_executesql @dropRoleForeignKeys;

                ALTER TABLE dbo.USUARIOS ADD CONSTRAINT FK_USUARIOS_ROLES
                    FOREIGN KEY (IdRol) REFERENCES dbo.ROLES(IdRol);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_USUARIOS_ROLES')
                    ALTER TABLE dbo.USUARIOS DROP CONSTRAINT FK_USUARIOS_ROLES;
                ALTER TABLE dbo.USUARIOS ADD CONSTRAINT FK_USUARIOS_ROLES_IdRol
                    FOREIGN KEY (IdRol) REFERENCES dbo.ROLES(IdRol) ON DELETE CASCADE;
                """);
        }
    }
}
