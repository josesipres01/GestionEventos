-- GestionEventosDbContext EF Core migrations own the database schema.
-- This file contains optional, idempotent seed data and the stored procedures for Eventos_Insertar and Proyectos_Insertar.

INSERT INTO dbo.ROLES (Nombre)
SELECT seed.Nombre
FROM (VALUES
    (N'Estudiante'),
    (N'Organizador/Docente'),
    (N'Administrador')
) AS seed(Nombre)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ROLES existing WHERE existing.Nombre = seed.Nombre
);

INSERT INTO dbo.CARRERAS (Nombre)
SELECT seed.Nombre
FROM (VALUES
    (N'Licenciatura en Ciencias de la Computación'),
    (N'Ingeniería en Sistemas de Información'),
    (N'Ingeniería en Mecatrónica'),
    (N'Licenciatura en Derecho')
) AS seed(Nombre)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.CARRERAS existing WHERE existing.Nombre = seed.Nombre
);
GO

-- =============================================
-- Procedimiento almacenado: Eventos_Insertar
-- Calcula automáticamente @Orden = COALESCE(MAX(Orden), 0) + 1
-- e inserta el nuevo evento devolviendo su Id con SCOPE_IDENTITY()
-- =============================================
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
GO

-- =============================================
-- Procedimiento almacenado: Proyectos_Insertar
-- Calcula automáticamente @Orden = COALESCE(MAX(Orden), 0) + 1
-- e inserta el nuevo proyecto devolviendo su Id con SCOPE_IDENTITY()
-- =============================================
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
GO

-- =============================================
-- Procedimiento almacenado: Carreras_Insertar
-- Calcula automáticamente @Orden = COALESCE(MAX(Orden), 0) + 1
-- e inserta la nueva carrera devolviendo su Id con SCOPE_IDENTITY()
-- =============================================
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
GO

