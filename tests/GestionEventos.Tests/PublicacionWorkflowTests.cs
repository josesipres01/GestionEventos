using System.Security.Claims;
using GestionEventos.Controllers;
using GestionEventos.Data;
using GestionEventos.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GestionEventos.Tests;

public class PublicacionWorkflowTests
{
    [Fact]
    public void ForeignKeysToUsersDoNotCascadeAndRequiredIndexesAreConfigured()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var db = CreateDbContext(connection);
        var userForeignKeys = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Usuario))
            .ToList();

        Assert.NotEmpty(userForeignKeys);
        Assert.All(userForeignKeys, foreignKey => Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
        Assert.Contains(db.Model.FindEntityType(typeof(Usuario))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Usuario.Correo)]));
        Assert.Contains(db.Model.FindEntityType(typeof(Registro))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Registro.IdUsuario), nameof(Registro.IdEvento)]));
        Assert.Contains(db.Model.FindEntityType(typeof(RegistroProyecto))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(RegistroProyecto.IdUsuario), nameof(RegistroProyecto.IdProyecto)]));
    }

    [Fact]
    public async Task DeleteProject_NotifiesCreatorAndAcceptedCollaboratorsAndRecordsDeleter()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedProjectAsync(db);
        db.RegistrosProyectos.AddRange(
            new RegistroProyecto { IdUsuario = 3, IdProyecto = 10, Estado = EstadoRegistroProyecto.Aceptado, FechaRegistro = DateTime.UtcNow },
            new RegistroProyecto { IdUsuario = 4, IdProyecto = 10, Estado = EstadoRegistroProyecto.Pendiente, FechaRegistro = DateTime.UtcNow },
            new RegistroProyecto { IdUsuario = 5, IdProyecto = 10, Estado = EstadoRegistroProyecto.Rechazado, FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = CreateProjectController(db, actorId: 2);
        var result = await controller.Eliminar(10, "Cierre del proyecto");

        Assert.IsType<RedirectToActionResult>(result);
        var project = await db.Proyectos.SingleAsync(item => item.IdProyecto == 10);
        Assert.False(project.Activo);
        Assert.Equal(2, project.IdUsuarioEliminador);
        Assert.Equal(new[] { 1, 3 }, await db.Notificaciones.OrderBy(n => n.IdUsuario).Select(n => n.IdUsuario).ToArrayAsync());
    }

    [Fact]
    public async Task ResolverColaborador_OnlyAssignedOrganizerCanAcceptAndAcceptanceIsAudited()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedProjectAsync(db);
        db.RegistrosProyectos.Add(new RegistroProyecto
        {
            IdUsuario = 3,
            IdProyecto = 10,
            Estado = EstadoRegistroProyecto.Pendiente,
            FechaRegistro = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var unauthorized = CreateProjectController(db, actorId: 3);
        var forbidden = await unauthorized.ResolverSolicitudColaborador(10, 1, aceptar: true, comentario: null);
        Assert.IsType<ForbidResult>(forbidden);
        Assert.Equal(EstadoRegistroProyecto.Pendiente,
            (await db.RegistrosProyectos.SingleAsync()).Estado);

        var organizer = CreateProjectController(db, actorId: 2, role: "Organizador/Docente");
        var result = await organizer.ResolverSolicitudColaborador(10, 1, aceptar: true, comentario: null);

        Assert.IsType<RedirectToActionResult>(result);
        var registration = await db.RegistrosProyectos.SingleAsync();
        Assert.Equal(EstadoRegistroProyecto.Aceptado, registration.Estado);
        Assert.Equal(2, registration.IdUsuarioRevisor);
        Assert.NotNull(registration.FechaRevision);
        Assert.Equal(1, await db.Notificaciones.CountAsync(n => n.IdUsuario == 3 && n.IdProyecto == 10));
    }

    [Fact]
    public async Task RegisterEvent_DoesNotExceedCapacity()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Eventos.Add(CreateEvent(id: 20, organizerId: 2, creatorId: 1, capacity: 1));
        db.Registros.Add(new Registro { IdUsuario = 3, IdEvento = 20, FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = CreateEventController(db, actorId: 4);
        var result = await controller.RegistrarInteres(20);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, await db.Registros.CountAsync(registration => registration.IdEvento == 20));
    }

    [Fact]
    public async Task RegisterEvent_DoesNotCreateDuplicateRegistration()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Eventos.Add(CreateEvent(id: 20, organizerId: 2, creatorId: 1, capacity: null));
        db.Registros.Add(new Registro { IdUsuario = 3, IdEvento = 20, FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = CreateEventController(db, actorId: 3);
        var result = await controller.RegistrarInteres(20);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, await db.Registros.CountAsync(registration => registration.IdUsuario == 3 && registration.IdEvento == 20));
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static GestionEventosDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<GestionEventosDbContext>()
            .UseSqlite(connection)
            .Options;
        return new GestionEventosDbContext(options);
    }

    private static async Task SeedUsersAsync(GestionEventosDbContext db)
    {
        db.Roles.AddRange(
            new Rol { IdRol = 1, Nombre = "Estudiante" },
            new Rol { IdRol = 2, Nombre = "Organizador/Docente" },
            new Rol { IdRol = 3, Nombre = "Administrador" });
        db.Usuarios.AddRange(
            CreateUser(1, 1),
            CreateUser(2, 2),
            CreateUser(3, 1),
            CreateUser(4, 1),
            CreateUser(5, 1));
        await db.SaveChangesAsync();
    }

    private static async Task SeedProjectAsync(GestionEventosDbContext db)
    {
        await SeedUsersAsync(db);
        var career = new Carrera { IdCarrera = 1, Nombre = "Ciencias" };
        db.Carreras.Add(career);
        db.Proyectos.Add(new Proyecto
        {
            IdProyecto = 10,
            IdUsuarioCreador = 1,
            IdUsuarioOrganizador = 2,
            Nombre = "Proyecto de prueba",
            Descripcion = "Descripción",
            LugarReunion = "Campus",
            FechaInicio = DateTime.UtcNow.Date,
            FechaFin = DateTime.UtcNow.Date.AddDays(1),
            IdCarrera = career.IdCarrera,
            FechaPublicacion = DateTime.UtcNow,
            FechaSolicitud = DateTime.UtcNow,
            EstadoAprobacion = EstadoAprobacion.Aprobado,
            Activo = true
        });
        await db.SaveChangesAsync();
    }

    private static Usuario CreateUser(int id, int roleId) => new()
    {
        IdUsuario = id,
        IdRol = roleId,
        Nombres = $"Usuario{id}",
        ApellidoPaterno = "Prueba",
        Correo = $"usuario{id}@example.test",
        PasswordHash = "test-hash",
        Activo = true,
        FechaRegistro = DateTime.UtcNow
    };

    private static Evento CreateEvent(int id, int organizerId, int creatorId, int? capacity)
    {
        return new Evento
        {
            IdEvento = id,
            Nombre = "Evento de prueba",
            Descripcion = "Descripción",
            FechaHora = DateTime.UtcNow.AddDays(1),
            FechaFin = DateTime.UtcNow.AddDays(1).AddHours(1),
            Cupo = capacity,
            Lugar = "Campus",
            IdUsuarioOrganizador = organizerId,
            IdUsuarioSolicitante = creatorId,
            FechaCreacion = DateTime.UtcNow,
            FechaSolicitud = DateTime.UtcNow,
            EstadoAprobacion = EstadoAprobacion.Aprobado,
            Activo = true
        };
    }

    private static ProyectosController CreateProjectController(
        GestionEventosDbContext db,
        int actorId,
        string role = "Estudiante")
    {
        var controller = new ProyectosController(db, null!, null!);
        ConfigureController(controller, actorId, role);
        return controller;
    }

    private static EventosController CreateEventController(GestionEventosDbContext db, int actorId)
    {
        var controller = new EventosController(db, null!);
        ConfigureController(controller, actorId, "Estudiante", configureUrl: true);
        return controller;
    }

    private static void ConfigureController(Controller controller, int actorId, string role, bool configureUrl = false)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
            new Claim(ClaimTypes.Name, $"usuario{actorId}@example.test"),
            new Claim(ClaimTypes.Role, role)
        ], "test");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        if (configureUrl)
        {
            controller.Url = new UrlHelper(actionContext);
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
