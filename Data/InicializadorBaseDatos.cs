using Microsoft.EntityFrameworkCore;

namespace GestionEventos.Data;

public static class InicializadorBaseDatos
{
    public static async Task PrepararEsquemaAsync(GestionEventosDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.EVENTOS', 'ImagenUrl') IS NULL
                ALTER TABLE dbo.EVENTOS ADD ImagenUrl NVARCHAR(500) NULL;

            IF COL_LENGTH('dbo.EVENTOS', 'Activo') IS NULL
                ALTER TABLE dbo.EVENTOS ADD Activo BIT NOT NULL
                    CONSTRAINT DF_EVENTOS_Activo DEFAULT 1;

            IF COL_LENGTH('dbo.EVENTOS', 'ReactivacionSolicitada') IS NULL
                ALTER TABLE dbo.EVENTOS ADD ReactivacionSolicitada BIT NOT NULL
                    CONSTRAINT DF_EVENTOS_ReactivacionSolicitada DEFAULT 0;

            IF COL_LENGTH('dbo.EVENTOS', 'FechaSolicitudReactivacion') IS NULL
                ALTER TABLE dbo.EVENTOS ADD FechaSolicitudReactivacion DATETIME2 NULL;

            IF COL_LENGTH('dbo.EVENTOS', 'MotivoEliminacion') IS NULL
                ALTER TABLE dbo.EVENTOS ADD MotivoEliminacion NVARCHAR(1000) NULL;

            IF COL_LENGTH('dbo.EVENTOS', 'FechaEliminacion') IS NULL
                ALTER TABLE dbo.EVENTOS ADD FechaEliminacion DATETIME2 NULL;

            IF COL_LENGTH('dbo.EVENTOS', 'IdUsuarioSolicitante') IS NULL
                ALTER TABLE dbo.EVENTOS ADD IdUsuarioSolicitante INT NULL;

            IF COL_LENGTH('dbo.EVENTOS', 'EstadoAprobacion') IS NULL
                ALTER TABLE dbo.EVENTOS ADD EstadoAprobacion NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_EVENTOS_EstadoAprobacion DEFAULT N'Aprobado';

            IF COL_LENGTH('dbo.EVENTOS', 'ComentarioAprobacion') IS NULL
                ALTER TABLE dbo.EVENTOS ADD ComentarioAprobacion NVARCHAR(1000) NULL;

            IF COL_LENGTH('dbo.EVENTOS', 'FechaRevision') IS NULL
                ALTER TABLE dbo.EVENTOS ADD FechaRevision DATETIME2 NULL;

            IF COL_LENGTH('dbo.EVENTOS', 'FechaSolicitud') IS NULL
                ALTER TABLE dbo.EVENTOS ADD FechaSolicitud DATETIME2 NOT NULL
                    CONSTRAINT DF_EVENTOS_FechaSolicitud DEFAULT CURRENT_TIMESTAMP;

            IF COL_LENGTH('dbo.PROYECTOS', 'ImagenUrl') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD ImagenUrl NVARCHAR(500) NULL;

            IF COL_LENGTH('dbo.PROYECTOS', 'Activo') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD Activo BIT NOT NULL
                    CONSTRAINT DF_PROYECTOS_Activo DEFAULT 1;

            IF COL_LENGTH('dbo.PROYECTOS', 'ReactivacionSolicitada') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD ReactivacionSolicitada BIT NOT NULL
                    CONSTRAINT DF_PROYECTOS_ReactivacionSolicitada DEFAULT 0;

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaSolicitudReactivacion') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD FechaSolicitudReactivacion DATETIME2 NULL;

            IF COL_LENGTH('dbo.PROYECTOS', 'MotivoEliminacion') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD MotivoEliminacion NVARCHAR(1000) NULL;

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaEliminacion') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD FechaEliminacion DATETIME2 NULL;

            IF COL_LENGTH('dbo.PROYECTOS', 'IdUsuarioResponsable') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD IdUsuarioResponsable INT NULL;

            IF COL_LENGTH('dbo.PROYECTOS', 'EstadoAprobacion') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD EstadoAprobacion NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_PROYECTOS_EstadoAprobacion DEFAULT N'Aprobado';

            IF COL_LENGTH('dbo.PROYECTOS', 'ComentarioAprobacion') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD ComentarioAprobacion NVARCHAR(1000) NULL;

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaRevision') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD FechaRevision DATETIME2 NULL;

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaSolicitud') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD FechaSolicitud DATETIME2 NOT NULL
                    CONSTRAINT DF_PROYECTOS_FechaSolicitud DEFAULT CURRENT_TIMESTAMP;

            IF COL_LENGTH('dbo.PROYECTOS', 'LugarReunion') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD LugarReunion NVARCHAR(150) NOT NULL
                    CONSTRAINT DF_PROYECTOS_LugarReunion DEFAULT N'Por definir';

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaInicio') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD FechaInicio DATETIME2 NOT NULL
                    CONSTRAINT DF_PROYECTOS_FechaInicio DEFAULT CURRENT_TIMESTAMP;

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaFin') IS NULL
                ALTER TABLE dbo.PROYECTOS ADD FechaFin DATETIME2 NOT NULL
                    CONSTRAINT DF_PROYECTOS_FechaFin DEFAULT DATEADD(MONTH, 1, CURRENT_TIMESTAMP);

            IF OBJECT_ID('dbo.REGISTROS_PROYECTOS', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.REGISTROS_PROYECTOS (
                    IdRegistroProyecto INT IDENTITY(1,1) NOT NULL,
                    IdUsuario INT NOT NULL,
                    IdProyecto INT NOT NULL,
                    FechaRegistro DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT PK_REGISTROS_PROYECTOS PRIMARY KEY (IdRegistroProyecto),
                    CONSTRAINT FK_REGISTROS_PROYECTOS_USUARIOS FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario),
                    CONSTRAINT FK_REGISTROS_PROYECTOS_PROYECTOS FOREIGN KEY (IdProyecto) REFERENCES dbo.PROYECTOS(IdProyecto),
                    CONSTRAINT UQ_Usuario_Proyecto UNIQUE (IdUsuario, IdProyecto)
                );
            END

            IF OBJECT_ID('dbo.NOTIFICACIONES', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.NOTIFICACIONES (
                    IdNotificacion INT IDENTITY(1,1) NOT NULL,
                    IdUsuario INT NOT NULL,
                    IdEvento INT NULL,
                    IdProyecto INT NULL,
                    Titulo NVARCHAR(200) NOT NULL,
                    Mensaje NVARCHAR(MAX) NOT NULL,
                    Leida BIT NOT NULL DEFAULT 0,
                    FechaCreacion DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT PK_NOTIFICACIONES PRIMARY KEY (IdNotificacion),
                    CONSTRAINT FK_NOTIFICACIONES_USUARIOS FOREIGN KEY (IdUsuario) REFERENCES dbo.USUARIOS(IdUsuario) ON DELETE CASCADE
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE dbo.EVENTOS
            SET IdUsuarioSolicitante = IdUsuarioOrganizador
            WHERE IdUsuarioSolicitante IS NULL;

            ALTER TABLE dbo.EVENTOS ALTER COLUMN IdUsuarioSolicitante INT NOT NULL;

            UPDATE p
            SET IdUsuarioResponsable = u.IdUsuario
            FROM dbo.PROYECTOS p
            INNER JOIN dbo.USUARIOS u
                ON LTRIM(RTRIM(CONCAT(u.Nombres, N' ', u.ApellidoPaterno, N' ', COALESCE(u.ApellidoMaterno, N'')))) =
                   LTRIM(RTRIM(p.NombreResponsable))
            WHERE p.IdUsuarioResponsable IS NULL
              AND (u.IdRol = 2 OR EXISTS (
                  SELECT 1
                  FROM dbo.ROLES r
                  WHERE r.IdRol = u.IdRol AND r.Nombre LIKE N'%Organizador%'
              ));
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EVENTOS_SOLICITANTES')
                ALTER TABLE dbo.EVENTOS ADD CONSTRAINT FK_EVENTOS_SOLICITANTES
                    FOREIGN KEY (IdUsuarioSolicitante) REFERENCES dbo.USUARIOS(IdUsuario);

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PROYECTOS_RESPONSABLES')
                ALTER TABLE dbo.PROYECTOS ADD CONSTRAINT FK_PROYECTOS_RESPONSABLES
                    FOREIGN KEY (IdUsuarioResponsable) REFERENCES dbo.USUARIOS(IdUsuario);
            """);
    }
}
