using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionEventos.Data.Migrations
{
    public partial class ModernizeDataModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @dropForeignKeys nvarchar(max) = N'';
                SELECT @dropForeignKeys +=
                    N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' +
                    QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + N' DROP CONSTRAINT ' +
                    QUOTENAME(fk.name) + N';'
                FROM sys.foreign_keys fk
                WHERE fk.parent_object_id IN (
                    OBJECT_ID(N'dbo.EVENTOS'),
                    OBJECT_ID(N'dbo.PROYECTOS'),
                    OBJECT_ID(N'dbo.REGISTROS'),
                    OBJECT_ID(N'dbo.REGISTROS_PROYECTOS'),
                    OBJECT_ID(N'dbo.NOTIFICACIONES'));
                IF @dropForeignKeys <> N'' EXEC sys.sp_executesql @dropForeignKeys;

                IF COL_LENGTH(N'dbo.EVENTOS', N'Activo') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD Activo bit NOT NULL CONSTRAINT DF_EVENTOS_Activo_Migration DEFAULT (1);
                IF COL_LENGTH(N'dbo.EVENTOS', N'ReactivacionSolicitada') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD ReactivacionSolicitada bit NOT NULL CONSTRAINT DF_EVENTOS_Reactivacion_Migration DEFAULT (0);
                IF COL_LENGTH(N'dbo.EVENTOS', N'FechaSolicitudReactivacion') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD FechaSolicitudReactivacion datetime2 NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'MotivoEliminacion') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD MotivoEliminacion nvarchar(1000) NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'FechaEliminacion') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD FechaEliminacion datetime2 NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'ImagenUrl') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD ImagenUrl nvarchar(500) NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'EstadoAprobacion') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD EstadoAprobacion nvarchar(20) NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'ComentarioAprobacion') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD ComentarioAprobacion nvarchar(1000) NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'FechaSolicitud') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD FechaSolicitud datetime2 NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'FechaCreacion') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD FechaCreacion datetime2 NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'IdUsuarioSolicitante') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD IdUsuarioSolicitante int NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'FechaFin') IS NULL
                BEGIN
                    ALTER TABLE dbo.EVENTOS ADD FechaFin datetime2 NULL;
                    UPDATE dbo.EVENTOS SET FechaFin = DATEADD(HOUR, 2, FechaHora);
                END
                IF COL_LENGTH(N'dbo.EVENTOS', N'Cupo') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD Cupo int NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'IdUsuarioRevisor') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD IdUsuarioRevisor int NULL;
                IF COL_LENGTH(N'dbo.EVENTOS', N'IdUsuarioEliminador') IS NULL
                    ALTER TABLE dbo.EVENTOS ADD IdUsuarioEliminador int NULL;

                UPDATE dbo.EVENTOS
                SET IdUsuarioSolicitante = IdUsuarioOrganizador
                WHERE IdUsuarioSolicitante IS NULL;
                UPDATE dbo.EVENTOS
                SET EstadoAprobacion = N'Aprobado'
                WHERE EstadoAprobacion IS NULL OR EstadoAprobacion NOT IN (N'Pendiente', N'Aprobado', N'Rechazado');
                UPDATE dbo.EVENTOS SET FechaSolicitud = COALESCE(FechaSolicitud, FechaCreacion, SYSUTCDATETIME());
                UPDATE dbo.EVENTOS SET FechaCreacion = COALESCE(FechaCreacion, FechaSolicitud, SYSUTCDATETIME());
                UPDATE dbo.EVENTOS SET FechaFin = DATEADD(HOUR, 2, FechaHora) WHERE FechaFin IS NULL OR FechaFin <= FechaHora;
                ALTER TABLE dbo.EVENTOS ALTER COLUMN IdUsuarioSolicitante int NOT NULL;
                ALTER TABLE dbo.EVENTOS ALTER COLUMN EstadoAprobacion nvarchar(20) NOT NULL;
                ALTER TABLE dbo.EVENTOS ALTER COLUMN FechaSolicitud datetime2 NOT NULL;
                ALTER TABLE dbo.EVENTOS ALTER COLUMN FechaCreacion datetime2 NOT NULL;
                ALTER TABLE dbo.EVENTOS ALTER COLUMN FechaFin datetime2 NOT NULL;

                IF COL_LENGTH(N'dbo.PROYECTOS', N'Activo') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD Activo bit NOT NULL CONSTRAINT DF_PROYECTOS_Activo_Migration DEFAULT (1);
                IF COL_LENGTH(N'dbo.PROYECTOS', N'ReactivacionSolicitada') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD ReactivacionSolicitada bit NOT NULL CONSTRAINT DF_PROYECTOS_Reactivacion_Migration DEFAULT (0);
                IF COL_LENGTH(N'dbo.PROYECTOS', N'FechaSolicitudReactivacion') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD FechaSolicitudReactivacion datetime2 NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'MotivoEliminacion') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD MotivoEliminacion nvarchar(1000) NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'FechaEliminacion') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD FechaEliminacion datetime2 NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'ImagenUrl') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD ImagenUrl nvarchar(500) NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'LugarReunion') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD LugarReunion nvarchar(150) NOT NULL CONSTRAINT DF_PROYECTOS_Lugar_Migration DEFAULT (N'Por definir');
                IF COL_LENGTH(N'dbo.PROYECTOS', N'FechaInicio') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD FechaInicio datetime2 NOT NULL CONSTRAINT DF_PROYECTOS_FechaInicio_Migration DEFAULT (SYSUTCDATETIME());
                IF COL_LENGTH(N'dbo.PROYECTOS', N'FechaFin') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD FechaFin datetime2 NOT NULL CONSTRAINT DF_PROYECTOS_FechaFin_Migration DEFAULT (DATEADD(MONTH, 1, SYSUTCDATETIME()));
                IF COL_LENGTH(N'dbo.PROYECTOS', N'FechaPublicacion') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD FechaPublicacion datetime2 NOT NULL CONSTRAINT DF_PROYECTOS_FechaPublicacion_Migration DEFAULT (SYSUTCDATETIME());
                IF COL_LENGTH(N'dbo.PROYECTOS', N'FechaSolicitud') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD FechaSolicitud datetime2 NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'EstadoAprobacion') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD EstadoAprobacion nvarchar(20) NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'ComentarioAprobacion') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD ComentarioAprobacion nvarchar(1000) NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'FechaRevision') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD FechaRevision datetime2 NULL;

                IF COL_LENGTH(N'dbo.PROYECTOS', N'IdUsuarioCreador') IS NULL
                    EXEC sys.sp_rename N'dbo.PROYECTOS.IdUsuario', N'IdUsuarioCreador', N'COLUMN';
                IF COL_LENGTH(N'dbo.PROYECTOS', N'IdUsuarioOrganizador') IS NULL AND
                   COL_LENGTH(N'dbo.PROYECTOS', N'IdUsuarioResponsable') IS NOT NULL
                    EXEC sys.sp_rename N'dbo.PROYECTOS.IdUsuarioResponsable', N'IdUsuarioOrganizador', N'COLUMN';
                IF COL_LENGTH(N'dbo.PROYECTOS', N'IdUsuarioOrganizador') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD IdUsuarioOrganizador int NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'IdUsuarioRevisor') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD IdUsuarioRevisor int NULL;
                IF COL_LENGTH(N'dbo.PROYECTOS', N'IdUsuarioEliminador') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD IdUsuarioEliminador int NULL;
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdUsuario')
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdUsuarioCreador')
                    EXEC sys.sp_rename N'dbo.PROYECTOS.IX_PROYECTOS_IdUsuario', N'IX_PROYECTOS_IdUsuarioCreador', N'INDEX';
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdUsuarioResponsable')
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdUsuarioOrganizador')
                    EXEC sys.sp_rename N'dbo.PROYECTOS.IX_PROYECTOS_IdUsuarioResponsable', N'IX_PROYECTOS_IdUsuarioOrganizador', N'INDEX';

                IF COL_LENGTH(N'dbo.PROYECTOS', N'NombreResponsable') IS NOT NULL
                BEGIN
                    EXEC sys.sp_executesql N'
                        UPDATE p
                        SET IdUsuarioOrganizador = u.IdUsuario
                        FROM dbo.PROYECTOS p
                        INNER JOIN dbo.USUARIOS u
                            ON LTRIM(RTRIM(CONCAT(u.Nombres, N'' '', u.ApellidoPaterno, N'' '', COALESCE(u.ApellidoMaterno, N'''')))) =
                               LTRIM(RTRIM(p.NombreResponsable))
                        WHERE p.IdUsuarioOrganizador IS NULL
                          AND (u.IdRol = 2 OR EXISTS (
                              SELECT 1 FROM dbo.ROLES r
                              WHERE r.IdRol = u.IdRol AND r.Nombre LIKE N''%Organizador%''
                          ));';
                    ALTER TABLE dbo.PROYECTOS DROP COLUMN NombreResponsable;
                END
                UPDATE dbo.PROYECTOS
                SET EstadoAprobacion = N'Aprobado'
                WHERE EstadoAprobacion IS NULL OR EstadoAprobacion NOT IN (N'Pendiente', N'Aprobado', N'Rechazado');
                UPDATE dbo.PROYECTOS SET FechaSolicitud = COALESCE(FechaSolicitud, FechaPublicacion, SYSUTCDATETIME());
                ALTER TABLE dbo.PROYECTOS ALTER COLUMN EstadoAprobacion nvarchar(20) NOT NULL;
                ALTER TABLE dbo.PROYECTOS ALTER COLUMN FechaSolicitud datetime2 NOT NULL;

                IF OBJECT_ID(N'dbo.REGISTROS_PROYECTOS', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.REGISTROS_PROYECTOS (
                        IdRegistroProyecto int IDENTITY(1,1) NOT NULL CONSTRAINT PK_REGISTROS_PROYECTOS PRIMARY KEY,
                        IdUsuario int NOT NULL,
                        IdProyecto int NOT NULL,
                        FechaRegistro datetime2 NOT NULL CONSTRAINT DF_REGISTROS_PROYECTOS_Fecha_Migration DEFAULT (SYSUTCDATETIME()));
                END
                IF COL_LENGTH(N'dbo.REGISTROS_PROYECTOS', N'FechaRegistro') IS NULL
                    ALTER TABLE dbo.REGISTROS_PROYECTOS ADD FechaRegistro datetime2 NOT NULL CONSTRAINT DF_REGISTROS_PROYECTOS_Fecha_Migration2 DEFAULT (SYSUTCDATETIME());
                IF COL_LENGTH(N'dbo.REGISTROS_PROYECTOS', N'Estado') IS NULL
                BEGIN
                    ALTER TABLE dbo.REGISTROS_PROYECTOS ADD Estado nvarchar(20) NOT NULL
                        CONSTRAINT DF_REGISTROS_PROYECTOS_Estado_Migration DEFAULT (N'Aceptado');
                END
                IF COL_LENGTH(N'dbo.REGISTROS_PROYECTOS', N'IdUsuarioRevisor') IS NULL
                    ALTER TABLE dbo.REGISTROS_PROYECTOS ADD IdUsuarioRevisor int NULL;
                IF COL_LENGTH(N'dbo.REGISTROS_PROYECTOS', N'FechaRevision') IS NULL
                    ALTER TABLE dbo.REGISTROS_PROYECTOS ADD FechaRevision datetime2 NULL;
                UPDATE dbo.REGISTROS_PROYECTOS
                SET Estado = N'Aceptado'
                WHERE Estado IS NULL OR Estado NOT IN (N'Pendiente', N'Aceptado', N'Rechazado');

                IF OBJECT_ID(N'dbo.NOTIFICACIONES', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.NOTIFICACIONES (
                        IdNotificacion int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NOTIFICACIONES PRIMARY KEY,
                        IdUsuario int NOT NULL,
                        IdEvento int NULL,
                        IdProyecto int NULL,
                        Titulo nvarchar(200) NOT NULL,
                        Mensaje nvarchar(max) NOT NULL,
                        Leida bit NOT NULL CONSTRAINT DF_NOTIFICACIONES_Leida_Migration DEFAULT (0),
                        FechaCreacion datetime2 NOT NULL CONSTRAINT DF_NOTIFICACIONES_Fecha_Migration DEFAULT (SYSUTCDATETIME()));
                END
                IF COL_LENGTH(N'dbo.NOTIFICACIONES', N'Leida') IS NULL
                    ALTER TABLE dbo.NOTIFICACIONES ADD Leida bit NOT NULL CONSTRAINT DF_NOTIFICACIONES_Leida_Migration2 DEFAULT (0);
                IF COL_LENGTH(N'dbo.NOTIFICACIONES', N'FechaCreacion') IS NULL
                    ALTER TABLE dbo.NOTIFICACIONES ADD FechaCreacion datetime2 NOT NULL CONSTRAINT DF_NOTIFICACIONES_Fecha_Migration2 DEFAULT (SYSUTCDATETIME());

                DECLARE @dropChecks nvarchar(max) = N'';
                SELECT @dropChecks += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) +
                    N'.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
                FROM sys.check_constraints
                WHERE name IN (N'CK_EVENTOS_EstadoAprobacion', N'CK_EVENTOS_Cupo', N'CK_EVENTOS_Fechas',
                    N'CK_PROYECTOS_EstadoAprobacion', N'CK_REGISTROS_PROYECTOS_Estado',
                    N'CK_NOTIFICACIONES_UnaPublicacion');
                IF @dropChecks <> N'' EXEC sys.sp_executesql @dropChecks;

                ALTER TABLE dbo.EVENTOS ADD CONSTRAINT CK_EVENTOS_EstadoAprobacion
                    CHECK (EstadoAprobacion IN (N'Pendiente', N'Aprobado', N'Rechazado'));
                ALTER TABLE dbo.EVENTOS ADD CONSTRAINT CK_EVENTOS_Cupo CHECK (Cupo IS NULL OR Cupo > 0);
                ALTER TABLE dbo.EVENTOS ADD CONSTRAINT CK_EVENTOS_Fechas CHECK (FechaFin > FechaHora);
                ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT CK_PROYECTOS_EstadoAprobacion
                    CHECK (EstadoAprobacion IN (N'Pendiente', N'Aprobado', N'Rechazado'));
                ALTER TABLE dbo.REGISTROS_PROYECTOS ADD CONSTRAINT CK_REGISTROS_PROYECTOS_Estado
                    CHECK (Estado IN (N'Pendiente', N'Aceptado', N'Rechazado'));
                ALTER TABLE dbo.NOTIFICACIONES ADD CONSTRAINT CK_NOTIFICACIONES_UnaPublicacion
                    CHECK (IdEvento IS NULL OR IdProyecto IS NULL);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EVENTOS') AND name = N'IX_EVENTOS_Activo_EstadoAprobacion')
                    CREATE INDEX IX_EVENTOS_Activo_EstadoAprobacion ON dbo.EVENTOS(Activo, EstadoAprobacion);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EVENTOS') AND name = N'IX_EVENTOS_IdUsuarioOrganizador')
                    CREATE INDEX IX_EVENTOS_IdUsuarioOrganizador ON dbo.EVENTOS(IdUsuarioOrganizador);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EVENTOS') AND name = N'IX_EVENTOS_IdUsuarioSolicitante')
                    CREATE INDEX IX_EVENTOS_IdUsuarioSolicitante ON dbo.EVENTOS(IdUsuarioSolicitante);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EVENTOS') AND name = N'IX_EVENTOS_IdUsuarioRevisor')
                    CREATE INDEX IX_EVENTOS_IdUsuarioRevisor ON dbo.EVENTOS(IdUsuarioRevisor);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EVENTOS') AND name = N'IX_EVENTOS_IdUsuarioEliminador')
                    CREATE INDEX IX_EVENTOS_IdUsuarioEliminador ON dbo.EVENTOS(IdUsuarioEliminador);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_Activo_EstadoAprobacion')
                    CREATE INDEX IX_PROYECTOS_Activo_EstadoAprobacion ON dbo.PROYECTOS(Activo, EstadoAprobacion);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdUsuarioCreador')
                    CREATE INDEX IX_PROYECTOS_IdUsuarioCreador ON dbo.PROYECTOS(IdUsuarioCreador);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdUsuarioOrganizador')
                    CREATE INDEX IX_PROYECTOS_IdUsuarioOrganizador ON dbo.PROYECTOS(IdUsuarioOrganizador);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdCarrera')
                    CREATE INDEX IX_PROYECTOS_IdCarrera ON dbo.PROYECTOS(IdCarrera);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdUsuarioRevisor')
                    CREATE INDEX IX_PROYECTOS_IdUsuarioRevisor ON dbo.PROYECTOS(IdUsuarioRevisor);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PROYECTOS') AND name = N'IX_PROYECTOS_IdUsuarioEliminador')
                    CREATE INDEX IX_PROYECTOS_IdUsuarioEliminador ON dbo.PROYECTOS(IdUsuarioEliminador);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REGISTROS') AND name = N'IX_REGISTROS_IdEvento')
                    CREATE INDEX IX_REGISTROS_IdEvento ON dbo.REGISTROS(IdEvento);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REGISTROS') AND name = N'IX_REGISTROS_IdUsuario')
                    CREATE INDEX IX_REGISTROS_IdUsuario ON dbo.REGISTROS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REGISTROS_PROYECTOS') AND name = N'IX_REGISTROS_PROYECTOS_IdProyecto_Estado')
                    CREATE INDEX IX_REGISTROS_PROYECTOS_IdProyecto_Estado ON dbo.REGISTROS_PROYECTOS(IdProyecto, Estado);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REGISTROS_PROYECTOS') AND name = N'IX_REGISTROS_PROYECTOS_IdProyecto')
                    CREATE INDEX IX_REGISTROS_PROYECTOS_IdProyecto ON dbo.REGISTROS_PROYECTOS(IdProyecto);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REGISTROS_PROYECTOS') AND name = N'IX_REGISTROS_PROYECTOS_IdUsuario')
                    CREATE INDEX IX_REGISTROS_PROYECTOS_IdUsuario ON dbo.REGISTROS_PROYECTOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.REGISTROS_PROYECTOS') AND name = N'IX_REGISTROS_PROYECTOS_IdUsuarioRevisor')
                    CREATE INDEX IX_REGISTROS_PROYECTOS_IdUsuarioRevisor ON dbo.REGISTROS_PROYECTOS(IdUsuarioRevisor);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NOTIFICACIONES') AND name = N'IX_NOTIFICACIONES_IdUsuario')
                    CREATE INDEX IX_NOTIFICACIONES_IdUsuario ON dbo.NOTIFICACIONES(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NOTIFICACIONES') AND name = N'IX_NOTIFICACIONES_IdEvento')
                    CREATE INDEX IX_NOTIFICACIONES_IdEvento ON dbo.NOTIFICACIONES(IdEvento);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NOTIFICACIONES') AND name = N'IX_NOTIFICACIONES_IdProyecto')
                    CREATE INDEX IX_NOTIFICACIONES_IdProyecto ON dbo.NOTIFICACIONES(IdProyecto);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.USUARIOS') AND name = N'IX_USUARIOS_IdRol')
                    CREATE INDEX IX_USUARIOS_IdRol ON dbo.USUARIOS(IdRol);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.USUARIOS') AND name = N'IX_USUARIOS_IdCarrera')
                    CREATE INDEX IX_USUARIOS_IdCarrera ON dbo.USUARIOS(IdCarrera);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.REGISTROS') AND i.is_unique = 1
                    AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = OBJECT_ID(N'dbo.REGISTROS') AND ic.index_id = i.index_id AND ic.key_ordinal = 1 AND c.name = N'IdUsuario')
                    AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = OBJECT_ID(N'dbo.REGISTROS') AND ic.index_id = i.index_id AND ic.key_ordinal = 2 AND c.name = N'IdEvento')
                    AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0) = 2)
                    CREATE UNIQUE INDEX UQ_REGISTROS_IdUsuario_IdEvento ON dbo.REGISTROS(IdUsuario, IdEvento);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.REGISTROS_PROYECTOS') AND i.is_unique = 1
                    AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = OBJECT_ID(N'dbo.REGISTROS_PROYECTOS') AND ic.index_id = i.index_id AND ic.key_ordinal = 1 AND c.name = N'IdUsuario')
                    AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = OBJECT_ID(N'dbo.REGISTROS_PROYECTOS') AND ic.index_id = i.index_id AND ic.key_ordinal = 2 AND c.name = N'IdProyecto')
                    AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0) = 2)
                    CREATE UNIQUE INDEX UQ_REGISTROS_PROYECTOS_IdUsuario_IdProyecto ON dbo.REGISTROS_PROYECTOS(IdUsuario, IdProyecto);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.USUARIOS') AND i.is_unique = 1 AND i.name = N'UQ_USUARIOS_Correo')
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.USUARIOS') AND i.is_unique = 1
                       AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                           WHERE ic.object_id = OBJECT_ID(N'dbo.USUARIOS') AND ic.index_id = i.index_id AND ic.key_ordinal = 1 AND c.name = N'Correo')
                       AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0) = 1)
                    CREATE UNIQUE INDEX UQ_USUARIOS_Correo ON dbo.USUARIOS(Correo);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.ROLES') AND i.is_unique = 1
                    AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = OBJECT_ID(N'dbo.ROLES') AND ic.index_id = i.index_id AND ic.key_ordinal = 1 AND c.name = N'Nombre')
                    AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0) = 1)
                    CREATE UNIQUE INDEX UQ_ROLES_Nombre ON dbo.ROLES(Nombre);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = OBJECT_ID(N'dbo.CARRERAS') AND i.is_unique = 1
                    AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = OBJECT_ID(N'dbo.CARRERAS') AND ic.index_id = i.index_id AND ic.key_ordinal = 1 AND c.name = N'Nombre')
                    AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0) = 1)
                    CREATE UNIQUE INDEX UQ_CARRERAS_Nombre ON dbo.CARRERAS(Nombre);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EVENTOS_USUARIOS_Organizador')
                    ALTER TABLE dbo.EVENTOS ADD CONSTRAINT FK_EVENTOS_USUARIOS_Organizador FOREIGN KEY (IdUsuarioOrganizador) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EVENTOS_USUARIOS_Solicitante')
                    ALTER TABLE dbo.EVENTOS ADD CONSTRAINT FK_EVENTOS_USUARIOS_Solicitante FOREIGN KEY (IdUsuarioSolicitante) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EVENTOS_USUARIOS_Revisor')
                    ALTER TABLE dbo.EVENTOS ADD CONSTRAINT FK_EVENTOS_USUARIOS_Revisor FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EVENTOS_USUARIOS_Eliminador')
                    ALTER TABLE dbo.EVENTOS ADD CONSTRAINT FK_EVENTOS_USUARIOS_Eliminador FOREIGN KEY (IdUsuarioEliminador) REFERENCES dbo.USUARIOS(IdUsuario);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PROYECTOS_USUARIOS_Creador')
                    ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_USUARIOS_Creador FOREIGN KEY (IdUsuarioCreador) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PROYECTOS_USUARIOS_Organizador')
                    ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_USUARIOS_Organizador FOREIGN KEY (IdUsuarioOrganizador) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PROYECTOS_USUARIOS_Revisor')
                    ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_USUARIOS_Revisor FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PROYECTOS_USUARIOS_Eliminador')
                    ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_USUARIOS_Eliminador FOREIGN KEY (IdUsuarioEliminador) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PROYECTOS_CARRERAS_IdCarrera')
                    ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_CARRERAS_IdCarrera FOREIGN KEY (IdCarrera) REFERENCES dbo.CARRERAS(IdCarrera);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_REGISTROS_USUARIOS_IdUsuario')
                    ALTER TABLE dbo.REGISTROS ADD CONSTRAINT FK_REGISTROS_USUARIOS_IdUsuario FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_REGISTROS_EVENTOS_IdEvento')
                    ALTER TABLE dbo.REGISTROS ADD CONSTRAINT FK_REGISTROS_EVENTOS_IdEvento FOREIGN KEY (IdEvento) REFERENCES dbo.EVENTOS(IdEvento);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_REGISTROS_PROYECTOS_USUARIOS')
                    ALTER TABLE dbo.REGISTROS_PROYECTOS ADD CONSTRAINT FK_REGISTROS_PROYECTOS_USUARIOS FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_REGISTROS_PROYECTOS_PROYECTOS')
                    ALTER TABLE dbo.REGISTROS_PROYECTOS ADD CONSTRAINT FK_REGISTROS_PROYECTOS_PROYECTOS FOREIGN KEY (IdProyecto) REFERENCES dbo.PROYECTOS(IdProyecto);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_REGISTROS_PROYECTOS_Revisor')
                    ALTER TABLE dbo.REGISTROS_PROYECTOS ADD CONSTRAINT FK_REGISTROS_PROYECTOS_Revisor FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.USUARIOS(IdUsuario);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_NOTIFICACIONES_USUARIOS_IdUsuario')
                    ALTER TABLE dbo.NOTIFICACIONES ADD CONSTRAINT FK_NOTIFICACIONES_USUARIOS_IdUsuario FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_NOTIFICACIONES_EVENTOS')
                    ALTER TABLE dbo.NOTIFICACIONES ADD CONSTRAINT FK_NOTIFICACIONES_EVENTOS FOREIGN KEY (IdEvento) REFERENCES dbo.EVENTOS(IdEvento);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_NOTIFICACIONES_PROYECTOS')
                    ALTER TABLE dbo.NOTIFICACIONES ADD CONSTRAINT FK_NOTIFICACIONES_PROYECTOS FOREIGN KEY (IdProyecto) REFERENCES dbo.PROYECTOS(IdProyecto);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @dropForeignKeys nvarchar(max) = N'';
                SELECT @dropForeignKeys +=
                    N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' +
                    QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + N' DROP CONSTRAINT ' +
                    QUOTENAME(fk.name) + N';'
                FROM sys.foreign_keys fk
                WHERE fk.parent_object_id IN (
                    OBJECT_ID(N'dbo.EVENTOS'), OBJECT_ID(N'dbo.PROYECTOS'),
                    OBJECT_ID(N'dbo.REGISTROS'), OBJECT_ID(N'dbo.REGISTROS_PROYECTOS'),
                    OBJECT_ID(N'dbo.NOTIFICACIONES'));
                IF @dropForeignKeys <> N'' EXEC sys.sp_executesql @dropForeignKeys;

                DECLARE @dropChecks nvarchar(max) = N'';
                SELECT @dropChecks += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) +
                    N'.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
                FROM sys.check_constraints
                WHERE name LIKE N'CK_EVENTOS_%' OR name = N'CK_PROYECTOS_EstadoAprobacion'
                    OR name = N'CK_REGISTROS_PROYECTOS_Estado' OR name = N'CK_NOTIFICACIONES_UnaPublicacion';
                IF @dropChecks <> N'' EXEC sys.sp_executesql @dropChecks;

                DROP INDEX IF EXISTS IX_EVENTOS_Activo_EstadoAprobacion ON dbo.EVENTOS;
                DROP INDEX IF EXISTS IX_EVENTOS_IdUsuarioRevisor ON dbo.EVENTOS;
                DROP INDEX IF EXISTS IX_EVENTOS_IdUsuarioEliminador ON dbo.EVENTOS;
                DROP INDEX IF EXISTS IX_PROYECTOS_Activo_EstadoAprobacion ON dbo.PROYECTOS;
                DROP INDEX IF EXISTS IX_PROYECTOS_IdUsuarioRevisor ON dbo.PROYECTOS;
                DROP INDEX IF EXISTS IX_PROYECTOS_IdUsuarioEliminador ON dbo.PROYECTOS;
                DROP INDEX IF EXISTS IX_REGISTROS_PROYECTOS_IdProyecto_Estado ON dbo.REGISTROS_PROYECTOS;
                DROP INDEX IF EXISTS IX_REGISTROS_PROYECTOS_IdUsuarioRevisor ON dbo.REGISTROS_PROYECTOS;
                DROP INDEX IF EXISTS UQ_REGISTROS_IdUsuario_IdEvento ON dbo.REGISTROS;
                DROP INDEX IF EXISTS UQ_REGISTROS_PROYECTOS_IdUsuario_IdProyecto ON dbo.REGISTROS_PROYECTOS;

                IF COL_LENGTH(N'dbo.PROYECTOS', N'NombreResponsable') IS NULL
                    ALTER TABLE dbo.PROYECTOS ADD NombreResponsable nvarchar(150) NOT NULL
                        CONSTRAINT DF_PROYECTOS_NombreResponsable_Down DEFAULT (N'');
                EXEC sys.sp_executesql N'
                    UPDATE p
                    SET NombreResponsable = LTRIM(RTRIM(CONCAT(u.Nombres, N'' '', u.ApellidoPaterno, N'' '', COALESCE(u.ApellidoMaterno, N''''))))
                    FROM dbo.PROYECTOS p
                    LEFT JOIN dbo.USUARIOS u ON u.IdUsuario = p.IdUsuarioOrganizador;';

                ALTER TABLE dbo.PROYECTOS DROP COLUMN IdUsuarioRevisor, IdUsuarioEliminador;
                EXEC sys.sp_rename N'dbo.PROYECTOS.IdUsuarioCreador', N'IdUsuario', N'COLUMN';
                EXEC sys.sp_rename N'dbo.PROYECTOS.IdUsuarioOrganizador', N'IdUsuarioResponsable', N'COLUMN';
                ALTER TABLE dbo.EVENTOS DROP COLUMN FechaFin, Cupo, IdUsuarioRevisor, IdUsuarioEliminador;
                ALTER TABLE dbo.REGISTROS_PROYECTOS DROP COLUMN Estado, IdUsuarioRevisor, FechaRevision;

                ALTER TABLE dbo.EVENTOS ADD CONSTRAINT FK_EVENTOS_USUARIOS_IdUsuarioOrganizador FOREIGN KEY (IdUsuarioOrganizador) REFERENCES dbo.USUARIOS(IdUsuario);
                ALTER TABLE dbo.EVENTOS ADD CONSTRAINT FK_EVENTOS_USUARIOS_IdUsuarioSolicitante FOREIGN KEY (IdUsuarioSolicitante) REFERENCES dbo.USUARIOS(IdUsuario);
                ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_USUARIOS_IdUsuario FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario);
                ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_USUARIOS_IdUsuarioResponsable FOREIGN KEY (IdUsuarioResponsable) REFERENCES dbo.USUARIOS(IdUsuario);
                ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_CARRERAS_IdCarrera FOREIGN KEY (IdCarrera) REFERENCES dbo.CARRERAS(IdCarrera);
                ALTER TABLE dbo.REGISTROS ADD CONSTRAINT FK_REGISTROS_USUARIOS_IdUsuario FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario);
                ALTER TABLE dbo.REGISTROS ADD CONSTRAINT FK_REGISTROS_EVENTOS_IdEvento FOREIGN KEY (IdEvento) REFERENCES dbo.EVENTOS(IdEvento);
                ALTER TABLE dbo.REGISTROS_PROYECTOS ADD CONSTRAINT FK_REGISTROS_PROYECTOS_USUARIOS_IdUsuario FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario);
                ALTER TABLE dbo.REGISTROS_PROYECTOS ADD CONSTRAINT FK_REGISTROS_PROYECTOS_PROYECTOS_IdProyecto FOREIGN KEY (IdProyecto) REFERENCES dbo.PROYECTOS(IdProyecto) ON DELETE CASCADE;
                ALTER TABLE dbo.NOTIFICACIONES ADD CONSTRAINT FK_NOTIFICACIONES_USUARIOS_IdUsuario FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario);
                ALTER TABLE dbo.NOTIFICACIONES ADD CONSTRAINT FK_NOTIFICACIONES_EVENTOS_IdEvento FOREIGN KEY (IdEvento) REFERENCES dbo.EVENTOS(IdEvento);
                ALTER TABLE dbo.NOTIFICACIONES ADD CONSTRAINT FK_NOTIFICACIONES_PROYECTOS_IdProyecto FOREIGN KEY (IdProyecto) REFERENCES dbo.PROYECTOS(IdProyecto);
                """);
        }
    }
}
