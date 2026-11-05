-- GestionEventosDbContext EF Core migrations own the database schema.
-- This file contains optional, idempotent seed data only.

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
