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
using GestionEventos.ViewModels;

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
        Assert.Contains(db.Model.FindEntityType(typeof(InteresEvento))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InteresEvento.IdUsuario), nameof(InteresEvento.IdEvento)]));
        Assert.Contains(db.Model.FindEntityType(typeof(InteresProyecto))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InteresProyecto.IdUsuario), nameof(InteresProyecto.IdProyecto)]));
        Assert.Contains(db.Model.FindEntityType(typeof(AsistenciaEvento))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(AsistenciaEvento.IdUsuario), nameof(AsistenciaEvento.IdEvento)]));
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
        Assert.Equal(new[] { 1, 3, 4 }, await db.Notificaciones.OrderBy(n => n.IdUsuario).Select(n => n.IdUsuario).ToArrayAsync());
    }

    [Fact]
    public async Task DeleteProject_DeniesCreatorWhenTheyAreNotTheAssignedResponsible()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedProjectAsync(db);

        var controller = CreateProjectController(db, actorId: 1);
        var result = Assert.IsType<RedirectToActionResult>(await controller.Eliminar(10, "Motivo de prueba"));

        Assert.Equal("AccesoDenegado", result.ActionName);
        Assert.True((await db.Proyectos.SingleAsync(proyecto => proyecto.IdProyecto == 10)).Activo);
    }

    [Fact]
    public async Task ApproveProject_UpdatesCreatorStatusAndCreatesVisibleNotification()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedProjectAsync(db);
        var project = await db.Proyectos.SingleAsync(item => item.IdProyecto == 10);
        project.EstadoAprobacion = EstadoAprobacion.Pendiente;
        await db.SaveChangesAsync();

        var organizer = CreateSolicitudesController(db, actorId: 2);
        Assert.IsType<RedirectToActionResult>(
            await organizer.ResolverProyecto(id: 10, aprobar: true, comentario: null));

        var requester = CreateSolicitudesController(db, actorId: 1);
        var result = Assert.IsType<ViewResult>(await requester.Index());
        var model = Assert.IsType<SolicitudesViewModel>(result.Model);

        Assert.Equal(EstadoAprobacion.Aprobado, Assert.Single(model.MisSolicitudes).Estado);
        Assert.Contains(model.Notificaciones, notification =>
            notification.IdProyecto == 10 &&
            notification.Titulo.Contains("aprobada", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProjectCatalog_DefaultsToTheUsersCareerAndAllowsAllCareers()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedProjectAsync(db);
        var user = await db.Usuarios.SingleAsync(usuario => usuario.IdUsuario == 3);
        user.IdCarrera = 1;
        db.Carreras.Add(new Carrera { IdCarrera = 2, Nombre = "Ingeniería" });
        db.Proyectos.Add(new Proyecto
        {
            IdProyecto = 11,
            IdUsuarioCreador = 1,
            IdUsuarioOrganizador = 2,
            Nombre = "Otro proyecto",
            Descripcion = "Descripción",
            LugarReunion = "Campus",
            FechaInicio = DateTime.UtcNow.Date,
            FechaFin = DateTime.UtcNow.Date.AddDays(1),
            IdCarrera = 2,
            FechaPublicacion = DateTime.UtcNow,
            FechaSolicitud = DateTime.UtcNow,
            EstadoAprobacion = EstadoAprobacion.Aprobado,
            Activo = true
        });
        await db.SaveChangesAsync();

        var controller = CreateProjectController(db, actorId: 3);
        var defaultResult = Assert.IsType<ViewResult>(await controller.Index(
            idCarrera: null,
            fechaDesde: null,
            fechaHasta: null));
        var defaultProjects = Assert.IsAssignableFrom<IEnumerable<Proyecto>>(defaultResult.Model);
        Assert.Equal(new[] { 10 }, defaultProjects.Select(proyecto => proyecto.IdProyecto).ToArray());

        var allResult = Assert.IsType<ViewResult>(await controller.Index(idCarrera: 0, fechaDesde: null, fechaHasta: null));
        var allProjects = Assert.IsAssignableFrom<IEnumerable<Proyecto>>(allResult.Model);
        Assert.Equal(new[] { 10, 11 }, allProjects.Select(proyecto => proyecto.IdProyecto).Order().ToArray());
    }

    [Fact]
    public async Task RegisterProject_ConfirmsRegistrationWithoutOrganizerApproval()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedProjectAsync(db);

        var controller = CreateProjectController(db, actorId: 3);
        var result = await controller.RegistrarInteres(10);

        Assert.IsType<RedirectToActionResult>(result);
        var registration = await db.RegistrosProyectos.SingleAsync();
        Assert.Equal(EstadoRegistroProyecto.Aceptado, registration.Estado);
        Assert.Null(registration.IdUsuarioRevisor);
        Assert.Null(registration.FechaRevision);
        Assert.Empty(await db.Notificaciones.ToListAsync());
    }

    [Fact]
    public async Task RegisterProject_DoesNotExceedCapacity()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedProjectAsync(db);
        db.RegistrosProyectos.Add(new RegistroProyecto
        {
            IdUsuario = 3,
            IdProyecto = 10,
            Estado = EstadoRegistroProyecto.Aceptado,
            FechaRegistro = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateProjectController(db, actorId: 4);
        var result = await controller.RegistrarInteres(10);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, await db.RegistrosProyectos.CountAsync(registration => registration.IdProyecto == 10));
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

    [Fact]
    public async Task MarkEventInterest_DoesNotRegisterOrConsumeCapacity_AndCanBeRemoved()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Eventos.Add(CreateEvent(id: 20, organizerId: 2, creatorId: 1, capacity: 1));
        db.Registros.Add(new Registro { IdUsuario = 3, IdEvento = 20, FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = CreateEventController(db, actorId: 4);
        Assert.IsType<RedirectToActionResult>(await controller.AlternarInteres(20));

        Assert.Equal(1, await db.InteresesEventos.CountAsync(interes => interes.IdUsuario == 4 && interes.IdEvento == 20));
        Assert.Equal(1, await db.Registros.CountAsync(registro => registro.IdEvento == 20));
        Assert.DoesNotContain(await db.Registros.ToListAsync(), registro => registro.IdUsuario == 4);

        Assert.IsType<RedirectToActionResult>(await controller.AlternarInteres(20));
        Assert.Empty(await db.InteresesEventos.ToListAsync());
    }

    [Fact]
    public async Task EventAttendanceList_CombinesInterestedAndRegisteredPeopleWithoutDuplicates()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Eventos.Add(CreateEvent(id: 20, organizerId: 2, creatorId: 1, capacity: null));
        db.Registros.Add(new Registro { IdUsuario = 3, IdEvento = 20, FechaRegistro = DateTime.UtcNow });
        db.InteresesEventos.AddRange(
            new InteresEvento { IdUsuario = 3, IdEvento = 20, FechaInteres = DateTime.UtcNow },
            new InteresEvento { IdUsuario = 4, IdEvento = 20, FechaInteres = DateTime.UtcNow });
        db.AsistenciasEventos.Add(new AsistenciaEvento
        {
            IdUsuario = 3,
            IdEvento = 20,
            FechaAsistencia = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateEventController(db, actorId: 2);
        var result = Assert.IsType<ViewResult>(await controller.Inscritos(20));
        var modelo = Assert.IsType<ListaAsistenciaViewModel>(result.Model);

        Assert.Equal(2, modelo.Participantes.Count);
        Assert.Contains(modelo.Participantes, participante =>
            participante.IdUsuario == 3 && participante.Inscrito && participante.Interesado && participante.Asistio);
        Assert.Contains(modelo.Participantes, participante =>
            participante.IdUsuario == 4 && !participante.Inscrito && participante.Interesado && !participante.Asistio);
    }

    [Fact]
    public async Task RegisterEventAttendance_SavesOnlyEligiblePeopleAndSupportsUnmarking()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Eventos.Add(CreateEvent(id: 20, organizerId: 2, creatorId: 1, capacity: null));
        db.Registros.Add(new Registro { IdUsuario = 3, IdEvento = 20, FechaRegistro = DateTime.UtcNow });
        db.InteresesEventos.Add(new InteresEvento { IdUsuario = 4, IdEvento = 20, FechaInteres = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = CreateEventController(db, actorId: 2);
        Assert.IsType<RedirectToActionResult>(await controller.RegistrarAsistencia(20, [3, 4]));
        Assert.Equal(new[] { 3, 4 },
            await db.AsistenciasEventos.OrderBy(asistencia => asistencia.IdUsuario)
                .Select(asistencia => asistencia.IdUsuario).ToArrayAsync());

        Assert.IsType<RedirectToActionResult>(await controller.RegistrarAsistencia(20, [4]));
        Assert.Equal(new[] { 4 }, await db.AsistenciasEventos.Select(asistencia => asistencia.IdUsuario).ToArrayAsync());

        Assert.IsType<BadRequestResult>(await controller.RegistrarAsistencia(20, [5]));
        Assert.Equal(new[] { 4 }, await db.AsistenciasEventos.Select(asistencia => asistencia.IdUsuario).ToArrayAsync());
    }

    [Fact]
    public async Task RegisterEventAttendance_DeniesUsersWhoDoNotOrganizeTheEvent()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Eventos.Add(CreateEvent(id: 20, organizerId: 2, creatorId: 1, capacity: null));
        db.Registros.Add(new Registro { IdUsuario = 3, IdEvento = 20, FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = CreateEventController(db, actorId: 3);
        var result = Assert.IsType<RedirectToActionResult>(await controller.RegistrarAsistencia(20, [3]));

        Assert.Equal("AccesoDenegado", result.ActionName);
        Assert.Empty(await db.AsistenciasEventos.ToListAsync());
    }

    [Fact]
    public async Task MarkProjectInterest_DoesNotRegisterOrConsumeCapacity_AndCanBeRemoved()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedProjectAsync(db);
        db.RegistrosProyectos.Add(new RegistroProyecto
        {
            IdUsuario = 4,
            IdProyecto = 10,
            Estado = EstadoRegistroProyecto.Aceptado,
            FechaRegistro = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateProjectController(db, actorId: 3);
        Assert.IsType<RedirectToActionResult>(await controller.AlternarInteres(10));

        Assert.Equal(1, await db.InteresesProyectos.CountAsync(interes => interes.IdUsuario == 3 && interes.IdProyecto == 10));
        Assert.Equal(1, await db.RegistrosProyectos.CountAsync(registro => registro.IdProyecto == 10));
        Assert.DoesNotContain(await db.RegistrosProyectos.ToListAsync(), registro => registro.IdUsuario == 3);

        Assert.IsType<RedirectToActionResult>(await controller.AlternarInteres(10));
        Assert.Empty(await db.InteresesProyectos.ToListAsync());
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
            Cupo = 1,
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

    [Fact]
    public async Task AdminOrdenarEventosYProyectos_PersisteOrdenYReflejaEnCatalogosPublicos()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Usuarios.Add(CreateUser(6, 3)); // Administrador
        var career = new Carrera { IdCarrera = 1, Nombre = "Ciencias" };
        db.Carreras.Add(career);
        db.Eventos.AddRange(
            CreateEvent(10, organizerId: 2, creatorId: 1, capacity: 10),
            CreateEvent(20, organizerId: 2, creatorId: 1, capacity: 10),
            CreateEvent(30, organizerId: 2, creatorId: 1, capacity: 10));
        db.Proyectos.AddRange(
            CreateProject(100, creatorId: 1, organizerId: 2, careerId: 1),
            CreateProject(200, creatorId: 1, organizerId: 2, careerId: 1),
            CreateProject(300, creatorId: 1, organizerId: 2, careerId: 1));
        await db.SaveChangesAsync();

        var admin = CreateAdminController(db, actorId: 6);
        Assert.IsType<OkResult>(await admin.OrdenarEventos([30, 10, 20]));
        Assert.IsType<OkResult>(await admin.OrdenarProyectos([300, 100, 200]));

        Assert.Equal(1, (await db.Eventos.SingleAsync(e => e.IdEvento == 30)).Orden);
        Assert.Equal(2, (await db.Eventos.SingleAsync(e => e.IdEvento == 10)).Orden);
        Assert.Equal(3, (await db.Eventos.SingleAsync(e => e.IdEvento == 20)).Orden);

        Assert.Equal(1, (await db.Proyectos.SingleAsync(p => p.IdProyecto == 300)).Orden);
        Assert.Equal(2, (await db.Proyectos.SingleAsync(p => p.IdProyecto == 100)).Orden);
        Assert.Equal(3, (await db.Proyectos.SingleAsync(p => p.IdProyecto == 200)).Orden);

        var eventosController = CreateEventController(db, actorId: 3);
        var explorar = Assert.IsType<ViewResult>(await eventosController.Index(null, null, null));
        var eventosOrdenados = Assert.IsAssignableFrom<IEnumerable<Evento>>(explorar.Model).Select(e => e.IdEvento).ToArray();
        var proyectosOrdenados = Assert.IsAssignableFrom<IEnumerable<Proyecto>>(explorar.ViewData["Proyectos"]).Select(p => p.IdProyecto).ToArray();

        Assert.Equal([30, 10, 20], eventosOrdenados);
        Assert.Equal([300, 100, 200], proyectosOrdenados);

        var catalogoEventos = Assert.IsType<ViewResult>(await eventosController.Catalogo(0, null, null));
        Assert.Equal([30, 10, 20], Assert.IsAssignableFrom<IEnumerable<Evento>>(catalogoEventos.Model).Select(e => e.IdEvento).ToArray());

        var proyectosController = CreateProjectController(db, actorId: 3);
        var catalogoProyectos = Assert.IsType<ViewResult>(await proyectosController.Index(0, null, null));
        Assert.Equal([300, 100, 200], Assert.IsAssignableFrom<IEnumerable<Proyecto>>(catalogoProyectos.Model).Select(p => p.IdProyecto).ToArray());
    }

    [Fact]
    public async Task OrganizadorOrdenarEventosYProyectos_ReordenaSoloSusPosicionesSinAlterarOtrosOrganizadores()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Usuarios.Add(CreateUser(6, 2)); // Segundo organizador
        db.Carreras.Add(new Carrera { IdCarrera = 1, Nombre = "Ciencias" });

        var ev1 = CreateEvent(10, organizerId: 2, creatorId: 1, capacity: 10);
        ev1.Orden = 1;
        var ev2 = CreateEvent(20, organizerId: 6, creatorId: 3, capacity: 10);
        ev2.Orden = 2;
        var ev3 = CreateEvent(30, organizerId: 2, creatorId: 1, capacity: 10);
        ev3.Orden = 3;
        var ev4 = CreateEvent(40, organizerId: 6, creatorId: 3, capacity: 10);
        ev4.Orden = 4;
        db.Eventos.AddRange(ev1, ev2, ev3, ev4);

        var pr1 = CreateProject(100, creatorId: 1, organizerId: 2, careerId: 1);
        pr1.Orden = 1;
        var pr2 = CreateProject(200, creatorId: 3, organizerId: 6, careerId: 1);
        pr2.Orden = 2;
        var pr3 = CreateProject(300, creatorId: 1, organizerId: 2, careerId: 1);
        pr3.Orden = 3;
        var pr4 = CreateProject(400, creatorId: 3, organizerId: 6, careerId: 1);
        pr4.Orden = 4;
        db.Proyectos.AddRange(pr1, pr2, pr3, pr4);
        await db.SaveChangesAsync();

        var orgEventos = CreateEventController(db, actorId: 2, role: "Organizador");
        var orgProyectos = CreateProjectController(db, actorId: 2, role: "Organizador");

        Assert.IsType<OkResult>(await orgEventos.Ordenar([30, 10]));
        Assert.IsType<OkResult>(await orgProyectos.Ordenar([300, 100]));

        Assert.Equal(1, (await db.Eventos.SingleAsync(e => e.IdEvento == 30)).Orden);
        Assert.Equal(2, (await db.Eventos.SingleAsync(e => e.IdEvento == 20)).Orden);
        Assert.Equal(3, (await db.Eventos.SingleAsync(e => e.IdEvento == 10)).Orden);
        Assert.Equal(4, (await db.Eventos.SingleAsync(e => e.IdEvento == 40)).Orden);

        Assert.Equal(1, (await db.Proyectos.SingleAsync(p => p.IdProyecto == 300)).Orden);
        Assert.Equal(2, (await db.Proyectos.SingleAsync(p => p.IdProyecto == 200)).Orden);
        Assert.Equal(3, (await db.Proyectos.SingleAsync(p => p.IdProyecto == 100)).Orden);
        Assert.Equal(4, (await db.Proyectos.SingleAsync(p => p.IdProyecto == 400)).Orden);
    }

    [Fact]
    public async Task OrganizadorYEstudiante_NoPuedenOrdenarPublicacionesAjenasOSinPermiso()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Usuarios.Add(CreateUser(6, 2)); // Segundo organizador
        db.Carreras.Add(new Carrera { IdCarrera = 1, Nombre = "Ciencias" });

        db.Eventos.AddRange(
            CreateEvent(10, organizerId: 2, creatorId: 1, capacity: 10),
            CreateEvent(20, organizerId: 6, creatorId: 3, capacity: 10));
        db.Proyectos.AddRange(
            CreateProject(100, creatorId: 1, organizerId: 2, careerId: 1),
            CreateProject(200, creatorId: 3, organizerId: 6, careerId: 1));
        await db.SaveChangesAsync();

        var studentEvents = CreateEventController(db, actorId: 1, role: "Estudiante");
        var studentProjects = CreateProjectController(db, actorId: 1, role: "Estudiante");
        var studentAdmin = CreateAdminController(db, actorId: 1, role: "Estudiante");

        Assert.IsType<ForbidResult>(await studentEvents.Ordenar([10]));
        Assert.IsType<ForbidResult>(await studentProjects.Ordenar([100]));
        Assert.IsType<ForbidResult>(await studentAdmin.OrdenarEventos([10, 20]));
        Assert.IsType<ForbidResult>(await studentAdmin.OrdenarProyectos([100, 200]));

        var orgEvents = CreateEventController(db, actorId: 2, role: "Organizador");
        var orgProjects = CreateProjectController(db, actorId: 2, role: "Organizador");

        Assert.IsType<ForbidResult>(await orgEvents.Ordenar([20, 10]));
        Assert.IsType<ForbidResult>(await orgProjects.Ordenar([200, 100]));
        Assert.All(await db.Eventos.ToListAsync(), e => Assert.Equal(0, e.Orden));
        Assert.All(await db.Proyectos.ToListAsync(), p => Assert.Equal(0, p.Orden));
    }

    [Fact]
    public async Task CrearEventoYProyecto_CalculaOrdenSiguienteConCoalesceMaxYRecuperaId()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Carreras.Add(new Carrera { IdCarrera = 1, Nombre = "Ciencias" });
        await db.SaveChangesAsync();

        var eventosController = CreateEventController(db, actorId: 1);
        Assert.IsType<RedirectToActionResult>(await eventosController.Crear(new CrearEventoViewModel
        {
            Nombre = "Primer Evento",
            Descripcion = "Desc 1",
            FechaHora = DateTime.UtcNow.AddDays(1),
            FechaFin = DateTime.UtcNow.AddDays(1).AddHours(2),
            Cupo = 20,
            Lugar = "Auditorio",
            IdUsuarioOrganizador = 2
        }));
        Assert.IsType<RedirectToActionResult>(await eventosController.Crear(new CrearEventoViewModel
        {
            Nombre = "Segundo Evento",
            Descripcion = "Desc 2",
            FechaHora = DateTime.UtcNow.AddDays(2),
            FechaFin = DateTime.UtcNow.AddDays(2).AddHours(2),
            Cupo = 30,
            Lugar = "Sala B",
            IdUsuarioOrganizador = 2
        }));

        var eventosCreados = await db.Eventos.OrderBy(e => e.IdEvento).ToListAsync();
        Assert.Equal(2, eventosCreados.Count);
        Assert.Equal(1, eventosCreados[0].Orden);
        Assert.Equal(2, eventosCreados[1].Orden);
        Assert.All(eventosCreados, e => Assert.True(e.IdEvento > 0));
        Assert.Equal(2, await db.Notificaciones.CountAsync(n => n.IdEvento != null));
    }

    [Fact]
    public async Task OrganizadorOAdmin_PuedeCrearEventoYProyectoPorDefectoOSeleccionandoSuNombre()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedUsersAsync(db);
        db.Usuarios.Add(CreateUser(6, 3)); // Administrador
        db.Carreras.Add(new Carrera { IdCarrera = 1, Nombre = "Ciencias" });
        await db.SaveChangesAsync();

        var orgEventos = CreateEventController(db, actorId: 2, role: "Organizador");
        // 1) Sin seleccionar organizador (0 -> se asigna por defecto a sí mismo)
        Assert.IsType<RedirectToActionResult>(await orgEventos.Crear(new CrearEventoViewModel
        {
            Nombre = "Evento por defecto Organizador",
            Descripcion = "Descripción",
            FechaHora = DateTime.UtcNow.AddDays(1),
            FechaFin = DateTime.UtcNow.AddDays(1).AddHours(2),
            Cupo = 25,
            Lugar = "Auditorio",
            IdUsuarioOrganizador = 0
        }));

        // 2) Seleccionando su propio nombre explícitamente
        Assert.IsType<RedirectToActionResult>(await orgEventos.Crear(new CrearEventoViewModel
        {
            Nombre = "Evento propio Organizador",
            Descripcion = "Descripción",
            FechaHora = DateTime.UtcNow.AddDays(2),
            FechaFin = DateTime.UtcNow.AddDays(2).AddHours(2),
            Cupo = 25,
            Lugar = "Auditorio",
            IdUsuarioOrganizador = 2
        }));

        // 3) Admin creando evento sin seleccionar organizador (0 -> por defecto Admin)
        var adminEventos = CreateEventController(db, actorId: 6, role: "Administrador");
        Assert.IsType<RedirectToActionResult>(await adminEventos.Crear(new CrearEventoViewModel
        {
            Nombre = "Evento por defecto Admin",
            Descripcion = "Descripción",
            FechaHora = DateTime.UtcNow.AddDays(3),
            FechaFin = DateTime.UtcNow.AddDays(3).AddHours(2),
            Cupo = 40,
            Lugar = "Sala Magna",
            IdUsuarioOrganizador = 0
        }));

        var eventos = await db.Eventos.OrderBy(e => e.Orden).ToListAsync();
        Assert.Equal(3, eventos.Count);
        Assert.Equal(2, eventos[0].IdUsuarioOrganizador);
        Assert.Equal(2, eventos[1].IdUsuarioOrganizador);
        Assert.Equal(6, eventos[2].IdUsuarioOrganizador);
        Assert.All(eventos, e => Assert.Equal(EstadoAprobacion.Aprobado, e.EstadoAprobacion));
    }

    private static Proyecto CreateProject(int id, int creatorId, int organizerId, int careerId) => new()
    {
        IdProyecto = id,
        IdUsuarioCreador = creatorId,
        IdUsuarioOrganizador = organizerId,
        Nombre = $"Proyecto {id}",
        Descripcion = "Descripción",
        LugarReunion = "Campus",
        FechaInicio = DateTime.UtcNow.Date,
        FechaFin = DateTime.UtcNow.Date.AddDays(2),
        Cupo = 5,
        IdCarrera = careerId,
        FechaPublicacion = DateTime.UtcNow.AddMinutes(-id),
        FechaSolicitud = DateTime.UtcNow,
        EstadoAprobacion = EstadoAprobacion.Aprobado,
        Activo = true
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
        ConfigureController(controller, actorId, role, configureUrl: true);
        return controller;
    }

    private static EventosController CreateEventController(
        GestionEventosDbContext db,
        int actorId,
        string role = "Estudiante")
    {
        var controller = new EventosController(db, null!);
        ConfigureController(controller, actorId, role, configureUrl: true);
        return controller;
    }

    private static AdminController CreateAdminController(
        GestionEventosDbContext db,
        int actorId,
        string role = "Administrador")
    {
        var controller = new AdminController(db, null!, null!);
        ConfigureController(controller, actorId, role, configureUrl: true);
        return controller;
    }

    private static SolicitudesController CreateSolicitudesController(GestionEventosDbContext db, int actorId)
    {
        var controller = new SolicitudesController(db);
        ConfigureController(controller, actorId, actorId == 2 ? "Organizador" : "Estudiante");
        controller.Url = new TestUrlHelper(controller.ControllerContext);
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

    private sealed class TestUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;

        public string? Action(UrlActionContext actionContext) => "/";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/";
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }
}
