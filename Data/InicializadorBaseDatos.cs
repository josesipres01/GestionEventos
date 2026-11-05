using System.Reflection;
using GestionEventos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GestionEventos.Data;

public static class InicializadorBaseDatos
{
    public static async Task AdoptarEsquemaExistenteAsync(GestionEventosDbContext db)
    {
        var esquema = await db.Database
            .SqlQueryRaw<int>("""
                SELECT CASE
                    WHEN OBJECT_ID(N'dbo.USUARIOS', N'U') IS NULL
                     AND OBJECT_ID(N'dbo.ROLES', N'U') IS NULL
                     AND OBJECT_ID(N'dbo.CARRERAS', N'U') IS NULL
                     AND OBJECT_ID(N'dbo.EVENTOS', N'U') IS NULL
                     AND OBJECT_ID(N'dbo.PROYECTOS', N'U') IS NULL THEN 0
                    WHEN OBJECT_ID(N'dbo.USUARIOS', N'U') IS NOT NULL
                     AND OBJECT_ID(N'dbo.ROLES', N'U') IS NOT NULL
                     AND OBJECT_ID(N'dbo.CARRERAS', N'U') IS NOT NULL
                     AND OBJECT_ID(N'dbo.EVENTOS', N'U') IS NOT NULL
                     AND OBJECT_ID(N'dbo.PROYECTOS', N'U') IS NOT NULL THEN 1
                    ELSE 2
                END AS [Value]
                """)
            .SingleAsync();

        if (esquema == 2)
        {
            throw new InvalidOperationException(
                "La base de datos contiene un esquema parcial no reconocido. No se aplicaron migraciones para evitar sobrescribir datos.");
        }

        if (esquema == 0)
        {
            return;
        }

        var existeHistorial = await db.Database
            .SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync() == 1;
        if (!existeHistorial)
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE dbo.__EFMigrationsHistory (
                    MigrationId nvarchar(150) NOT NULL,
                    ProductVersion nvarchar(32) NOT NULL,
                    CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId));
                """);
        }
        else if (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM dbo.__EFMigrationsHistory").SingleAsync() > 0)
        {
            return;
        }

        var migrations = db.GetService<IMigrationsAssembly>().Migrations;
        var initialMigrationId = migrations.Single(migration => migration.Value.Name == "InitialSchema").Key;
        var productVersion = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "10.0.0";

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({initialMigrationId}, {productVersion})");
    }

    public static async Task PrepararDatosSemillaAsync(GestionEventosDbContext db)
    {
        var roles = new[]
        {
            (Id: 1, Nombre: "Estudiante"),
            (Id: 2, Nombre: "Organizador/Docente"),
            (Id: 3, Nombre: "Administrador")
        };

        foreach (var role in roles)
        {
            if (await db.Roles.AsNoTracking().AnyAsync(item => item.IdRol == role.Id))
            {
                continue;
            }

            if (await db.Roles.AsNoTracking().AnyAsync(item => item.Nombre == role.Nombre))
            {
                throw new InvalidOperationException(
                    $"El rol '{role.Nombre}' existe con un identificador distinto al esperado ({role.Id}).");
            }

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SET IDENTITY_INSERT dbo.ROLES ON;
                INSERT INTO dbo.ROLES (IdRol, Nombre) VALUES ({role.Id}, {role.Nombre});
                SET IDENTITY_INSERT dbo.ROLES OFF;
                """);
        }

        var carrerasBase = new[]
        {
            "Licenciatura en Ciencias de la Computación",
            "Ingeniería en Sistemas de Información",
            "Ingeniería en Mecatrónica",
            "Licenciatura en Derecho"
        };
        var carrerasExistentes = await db.Carreras
            .AsNoTracking()
            .Select(carrera => carrera.Nombre)
            .ToListAsync();
        var nuevasCarreras = carrerasBase
            .Where(nombre => !carrerasExistentes.Contains(nombre, StringComparer.OrdinalIgnoreCase))
            .Select(nombre => new Carrera { Nombre = nombre });
        db.Carreras.AddRange(nuevasCarreras);
        await db.SaveChangesAsync();
    }
}
