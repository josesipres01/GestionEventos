-- 1. Creación de la base de datos
CREATE DATABASE GestionEventosUnison;
GO

USE GestionEventosUnison;
GO

-- 2. Tabla ROLES
CREATE TABLE ROLES (
    IdRol INT IDENTITY(1,1) NOT NULL,
    Nombre NVARCHAR(30) NOT NULL,
    CONSTRAINT PK_ROLES PRIMARY KEY (IdRol),
    CONSTRAINT UQ_ROLES_Nombre UNIQUE (Nombre)
);
GO

-- 3. Tabla CARRERAS
CREATE TABLE CARRERAS (
    IdCarrera INT IDENTITY(1,1) NOT NULL,
    Nombre NVARCHAR(120) NOT NULL,
    CONSTRAINT PK_CARRERAS PRIMARY KEY (IdCarrera),
    CONSTRAINT UQ_CARRERAS_Nombre UNIQUE (Nombre)
);
GO

-- 4. Tabla USUARIOS
CREATE TABLE USUARIOS (
    IdUsuario INT IDENTITY(1,1) NOT NULL,
    Nombres NVARCHAR(80) NOT NULL,
    ApellidoPaterno NVARCHAR(60) NOT NULL,
    ApellidoMaterno NVARCHAR(60) NULL,
    Correo NVARCHAR(256) NOT NULL,
    PasswordHash NVARCHAR(256) NOT NULL,
    IdRol INT NOT NULL DEFAULT 1, -- Por defecto Rol 1 (Estudiante)
    IdCarrera INT NULL,
    Activo BIT NOT NULL DEFAULT 1,
    FechaRegistro DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT PK_USUARIOS PRIMARY KEY (IdUsuario),
    CONSTRAINT UQ_USUARIOS_Correo UNIQUE (Correo),
    CONSTRAINT FK_USUARIOS_ROLES FOREIGN KEY (IdRol) REFERENCES ROLES(IdRol),
    CONSTRAINT FK_USUARIOS_CARRERAS FOREIGN KEY (IdCarrera) REFERENCES CARRERAS(IdCarrera)
);
GO

-- 5. Tabla EVENTOS
CREATE TABLE EVENTOS (
    IdEvento INT IDENTITY(1,1) NOT NULL,
    Nombre NVARCHAR(150) NOT NULL,
    Descripcion NVARCHAR(MAX) NOT NULL,
    FechaHora DATETIME2 NOT NULL,
    Lugar NVARCHAR(150) NOT NULL,
    ImagenUrl NVARCHAR(500) NULL,
    Activo BIT NOT NULL DEFAULT 1,
    ReactivacionSolicitada BIT NOT NULL DEFAULT 0,
    FechaSolicitudReactivacion DATETIME2 NULL,
    MotivoEliminacion NVARCHAR(1000) NULL,
    FechaEliminacion DATETIME2 NULL,
    IdUsuarioOrganizador INT NOT NULL,
    IdUsuarioSolicitante INT NOT NULL,
    FechaCreacion DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EstadoAprobacion NVARCHAR(20) NOT NULL DEFAULT N'Pendiente',
    ComentarioAprobacion NVARCHAR(1000) NULL,
    FechaRevision DATETIME2 NULL,
    FechaSolicitud DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT PK_EVENTOS PRIMARY KEY (IdEvento),
    CONSTRAINT FK_EVENTOS_USUARIOS FOREIGN KEY (IdUsuarioOrganizador) REFERENCES USUARIOS(IdUsuario),
    CONSTRAINT FK_EVENTOS_SOLICITANTES FOREIGN KEY (IdUsuarioSolicitante) REFERENCES USUARIOS(IdUsuario)
);
GO

-- 6. Tabla PROYECTOS
CREATE TABLE PROYECTOS (
    IdProyecto INT IDENTITY(1,1) NOT NULL,
    IdUsuario INT NOT NULL,
    IdUsuarioResponsable INT NULL,
    Activo BIT NOT NULL DEFAULT 1,
    ReactivacionSolicitada BIT NOT NULL DEFAULT 0,
    FechaSolicitudReactivacion DATETIME2 NULL,
    MotivoEliminacion NVARCHAR(1000) NULL,
    FechaEliminacion DATETIME2 NULL,
    Nombre NVARCHAR(150) NOT NULL,
    Descripcion NVARCHAR(MAX) NOT NULL,
    NombreResponsable NVARCHAR(150) NOT NULL,
    LugarReunion NVARCHAR(150) NOT NULL,
    FechaInicio DATETIME2 NOT NULL,
    FechaFin DATETIME2 NOT NULL,
    ImagenUrl NVARCHAR(500) NULL,
    IdCarrera INT NOT NULL,
    FechaPublicacion DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EstadoAprobacion NVARCHAR(20) NOT NULL DEFAULT N'Pendiente',
    ComentarioAprobacion NVARCHAR(1000) NULL,
    FechaRevision DATETIME2 NULL,
    FechaSolicitud DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT PK_PROYECTOS PRIMARY KEY (IdProyecto),
    CONSTRAINT FK_PROYECTOS_USUARIOS FOREIGN KEY (IdUsuario) REFERENCES USUARIOS(IdUsuario),
    CONSTRAINT FK_PROYECTOS_RESPONSABLES FOREIGN KEY (IdUsuarioResponsable) REFERENCES USUARIOS(IdUsuario),
    CONSTRAINT FK_PROYECTOS_CARRERAS FOREIGN KEY (IdCarrera) REFERENCES CARRERAS(IdCarrera)
);
GO

-- 7. Tabla REGISTROS (Inscripciones de estudiantes a eventos)
CREATE TABLE REGISTROS (
    IdRegistro INT IDENTITY(1,1) NOT NULL,
    IdUsuario INT NOT NULL,
    IdEvento INT NOT NULL,
    FechaRegistro DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT PK_REGISTROS PRIMARY KEY (IdRegistro),
    CONSTRAINT FK_REGISTROS_USUARIOS FOREIGN KEY (IdUsuario) REFERENCES USUARIOS(IdUsuario),
    CONSTRAINT FK_REGISTROS_EVENTOS FOREIGN KEY (IdEvento) REFERENCES EVENTOS(IdEvento),
    -- Evita que un estudiante se registre 2 veces al mismo evento:
    CONSTRAINT UQ_Usuario_Evento UNIQUE (IdUsuario, IdEvento) 
);
GO

-- 8. Tabla REGISTROS_PROYECTOS (Inscripciones de colaboradores a proyectos)
IF OBJECT_ID('dbo.REGISTROS_PROYECTOS', 'U') IS NULL
BEGIN
    CREATE TABLE REGISTROS_PROYECTOS (
        IdRegistroProyecto INT IDENTITY(1,1) NOT NULL,
        IdUsuario INT NOT NULL,
        IdProyecto INT NOT NULL,
        FechaRegistro DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
        CONSTRAINT PK_REGISTROS_PROYECTOS PRIMARY KEY (IdRegistroProyecto),
        CONSTRAINT FK_REGISTROS_PROYECTOS_USUARIOS FOREIGN KEY (IdUsuario) REFERENCES USUARIOS(IdUsuario),
        CONSTRAINT FK_REGISTROS_PROYECTOS_PROYECTOS FOREIGN KEY (IdProyecto) REFERENCES PROYECTOS(IdProyecto),
        CONSTRAINT UQ_Usuario_Proyecto UNIQUE (IdUsuario, IdProyecto)
    );
END
GO

-- Insertar Roles
INSERT INTO ROLES (Nombre) VALUES 
('Estudiante'),
('Organizador/Docente'),
('Administrador');

-- Insertar Carreras de ejemplo
INSERT INTO CARRERAS (Nombre) VALUES 
('Licenciatura en Ciencias de la Computación'),
('Ingeniería en Sistemas de Información'),
('Ingeniería en Mecatrónica'),
('Licenciatura en Derecho');
GO
