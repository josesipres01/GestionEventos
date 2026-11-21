using System.Security.Claims;
using System.Data;
using GestionEventos.Data;
using GestionEventos.Models;
using GestionEventos.Servicios;
using GestionEventos.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionEventos.Controllers;

[Authorize]
public class EventosController(
    GestionEventosDbContext dbContext,
    IWebHostEnvironment environment) : Controller
{
    private static readonly HashSet<string> ExtensionesImagenPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private const long TamanoMaximoImagenBytes = 5 * 1024 * 1024; // 5 MB

    public async Task<IActionResult> Index(int? idCarrera, DateTime? fechaDesde, DateTime? fechaHasta)
    {
        if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date)
        {
            ModelState.AddModelError(nameof(fechaHasta), "La fecha final debe ser igual o posterior a la fecha inicial.");
        }

        var carreras = await dbContext.Carreras
            .AsNoTracking()
            .OrderBy(carrera => carrera.Orden == 0 ? int.MaxValue : carrera.Orden)
            .ThenBy(carrera => carrera.Nombre)
            .ToListAsync();
        var filtroCarrera = idCarrera;

        var eventosQuery = dbContext.Eventos
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(evento => evento.Organizador)
                .ThenInclude(organizador => organizador.Carrera)
            .Include(evento => evento.Registros)
            .Include(evento => evento.Intereses)
            .Where(evento => evento.Activo && evento.EstadoAprobacion == EstadoAprobacion.Aprobado);
        var proyectosQuery = dbContext.Proyectos
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(proyecto => proyecto.Carrera)
            .Include(proyecto => proyecto.Creador)
            .Include(proyecto => proyecto.Organizador)
            .Include(proyecto => proyecto.Registros)
            .Include(proyecto => proyecto.Intereses)
            .Where(proyecto => proyecto.Activo && proyecto.EstadoAprobacion == EstadoAprobacion.Aprobado);

        if (filtroCarrera.HasValue)
        {
            eventosQuery = eventosQuery.Where(evento => evento.Organizador != null &&
                evento.Organizador.IdCarrera == filtroCarrera);
            proyectosQuery = proyectosQuery.Where(proyecto => proyecto.IdCarrera == filtroCarrera);
        }

        if (!(fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date))
        {
            if (fechaDesde.HasValue)
            {
                var desde = fechaDesde.Value.Date;
                eventosQuery = eventosQuery.Where(evento => evento.FechaHora >= desde);
                proyectosQuery = proyectosQuery.Where(proyecto => proyecto.FechaFin >= desde);
            }

            if (fechaHasta.HasValue)
            {
                var hasta = fechaHasta.Value.Date.AddDays(1);
                eventosQuery = eventosQuery.Where(evento => evento.FechaHora < hasta);
                proyectosQuery = proyectosQuery.Where(proyecto => proyecto.FechaInicio < hasta);
            }
        }

        var eventos = await eventosQuery
            .OrderBy(evento => evento.Orden == 0 ? int.MaxValue : evento.Orden)
            .ThenBy(evento => evento.FechaHora)
            .ToListAsync();

        var proyectos = await proyectosQuery
            .OrderBy(proyecto => proyecto.Orden == 0 ? int.MaxValue : proyecto.Orden)
            .ThenByDescending(proyecto => proyecto.FechaPublicacion)
            .ToListAsync();

        var eventosRegistradosIds = new HashSet<int>();
        var proyectosRegistradosIds = new HashSet<int>();
        var eventosInteresadosIds = new HashSet<int>();
        var proyectosInteresadosIds = new HashSet<int>();
        var notificacionesNoLeidas = new List<Notificacion>();
        var usuarioActualId = 0;

        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            usuarioActualId = usuarioId;
            eventosRegistradosIds = eventos
                .Where(e => e.Registros.Any(r => r.IdUsuario == usuarioId))
                .Select(e => e.IdEvento)
                .ToHashSet();

            proyectosRegistradosIds = proyectos
                .Where(p => p.Registros.Any(r =>
                    r.IdUsuario == usuarioId && r.Estado != EstadoRegistroProyecto.Rechazado))
                .Select(p => p.IdProyecto)
                .ToHashSet();
            eventosInteresadosIds = eventos
                .Where(e => e.Intereses.Any(interes => interes.IdUsuario == usuarioId))
                .Select(e => e.IdEvento)
                .ToHashSet();
            proyectosInteresadosIds = proyectos
                .Where(p => p.Intereses.Any(interes => interes.IdUsuario == usuarioId))
                .Select(p => p.IdProyecto)
                .ToHashSet();

            notificacionesNoLeidas = await dbContext.Notificaciones
                .AsNoTracking()
                .Where(n => n.IdUsuario == usuarioId && !n.Leida)
                .OrderByDescending(n => n.FechaCreacion)
                .Take(5)
                .ToListAsync();
        }

        ViewBag.Proyectos = proyectos;
        ViewBag.EventosRegistradosIds = eventosRegistradosIds;
        ViewBag.ProyectosRegistradosIds = proyectosRegistradosIds;
        ViewBag.EventosInteresadosIds = eventosInteresadosIds;
        ViewBag.ProyectosInteresadosIds = proyectosInteresadosIds;
        ViewBag.NotificacionesNoLeidas = notificacionesNoLeidas;
        ViewBag.UsuarioActualId = usuarioActualId;
        ViewBag.Carreras = carreras;
        ViewBag.IdCarreraSeleccionada = filtroCarrera;
        ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");
        ViewBag.EventosOrdenadosSelect = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            eventos,
            nameof(Evento.IdEvento),
            nameof(Evento.Nombre));
        ViewBag.ProyectosOrdenadosSelect = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            proyectos,
            nameof(Proyecto.IdProyecto),
            nameof(Proyecto.Nombre));

        return View("~/Views/Estudiante/ExplorarCampus.cshtml", eventos);
    }

    [HttpGet]
    public async Task<IActionResult> Catalogo(int? idCarrera, DateTime? fechaDesde, DateTime? fechaHasta)
    {
        if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date)
        {
            ModelState.AddModelError(nameof(fechaHasta), "La fecha final debe ser igual o posterior a la fecha inicial.");
        }

        var usuarioId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : 0;
        var usuarioCarreraId = usuarioId == 0
            ? null
            : await dbContext.Usuarios
                .AsNoTracking()
                .Where(usuario => usuario.IdUsuario == usuarioId)
                .Select(usuario => usuario.IdCarrera)
                .FirstOrDefaultAsync();
        var filtroCarrera = idCarrera == 0 ? null : idCarrera ?? usuarioCarreraId;
        var carreras = await dbContext.Carreras
            .AsNoTracking()
            .OrderBy(carrera => carrera.Orden == 0 ? int.MaxValue : carrera.Orden)
            .ThenBy(carrera => carrera.Nombre)
            .ToListAsync();

        var eventosQuery = dbContext.Eventos
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(evento => evento.Organizador)
                .ThenInclude(organizador => organizador.Carrera)
            .Include(evento => evento.Registros)
            .Include(evento => evento.Intereses)
            .Where(evento => evento.Activo && evento.EstadoAprobacion == EstadoAprobacion.Aprobado);

        if (filtroCarrera.HasValue)
        {
            eventosQuery = eventosQuery.Where(evento =>
                evento.Organizador != null && evento.Organizador.IdCarrera == filtroCarrera);
        }

        if (!(fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date))
        {
            if (fechaDesde.HasValue)
            {
                var desde = fechaDesde.Value.Date;
                eventosQuery = eventosQuery.Where(evento => evento.FechaHora >= desde);
            }

            if (fechaHasta.HasValue)
            {
                var hasta = fechaHasta.Value.Date.AddDays(1);
                eventosQuery = eventosQuery.Where(evento => evento.FechaHora < hasta);
            }
        }

        var eventos = await eventosQuery
            .OrderBy(evento => evento.Orden == 0 ? int.MaxValue : evento.Orden)
            .ThenBy(evento => evento.FechaHora)
            .ToListAsync();
        var eventosRegistradosIds = eventos
            .Where(evento => evento.Registros.Any(registro => registro.IdUsuario == usuarioId))
            .Select(evento => evento.IdEvento)
            .ToHashSet();
        var eventosInteresadosIds = eventos
            .Where(evento => evento.Intereses.Any(interes => interes.IdUsuario == usuarioId))
            .Select(evento => evento.IdEvento)
            .ToHashSet();

        ViewBag.Carreras = carreras;
        ViewBag.IdCarreraSeleccionada = filtroCarrera;
        ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");
        ViewBag.EventosRegistradosIds = eventosRegistradosIds;
        ViewBag.EventosInteresadosIds = eventosInteresadosIds;
        ViewBag.EventosOrdenadosSelect = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            eventos,
            nameof(Evento.IdEvento),
            nameof(Evento.Nombre));
        return View("~/Views/Estudiante/CatalogoEventos.cshtml", eventos);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Inscribir(int? idEvento = null)
    {
        var eventosDisponibles = await dbContext.Eventos
            .AsNoTracking()
            .Where(e => e.Activo && e.EstadoAprobacion == EstadoAprobacion.Aprobado)
            .OrderBy(e => e.Orden == 0 ? int.MaxValue : e.Orden)
            .ThenBy(e => e.FechaHora)
            .ToListAsync();

        var modelo = new InscripcionEventoViewModel
        {
            IdEvento = idEvento ?? (eventosDisponibles.FirstOrDefault()?.IdEvento ?? 0),
            Eventos = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                eventosDisponibles,
                nameof(Evento.IdEvento),
                nameof(Evento.Nombre),
                idEvento)
        };

        return View("~/Views/Estudiante/InscribirEvento.cshtml", modelo);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inscribir(InscripcionEventoViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            var eventosDisponibles = await dbContext.Eventos
                .AsNoTracking()
                .Where(e => e.Activo && e.EstadoAprobacion == EstadoAprobacion.Aprobado)
                .OrderBy(e => e.Orden == 0 ? int.MaxValue : e.Orden)
                .ThenBy(e => e.FechaHora)
                .ToListAsync();

            modelo.Eventos = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                eventosDisponibles,
                nameof(Evento.IdEvento),
                nameof(Evento.Nombre),
                modelo.IdEvento);

            return View("~/Views/Estudiante/InscribirEvento.cshtml", modelo);
        }

        return await RegistrarInteres(modelo.IdEvento);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var evento = await dbContext.Eventos
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(item => item.Organizador)
                .ThenInclude(organizador => organizador.Carrera)
            .Include(item => item.Registros)
                .ThenInclude(registro => registro.Usuario)
                    .ThenInclude(usuario => usuario.Carrera)
            .Include(item => item.Intereses)
            .Include(item => item.Asistencias)
            .FirstOrDefaultAsync(item => item.IdEvento == id);

        if (evento is null)
        {
            return NotFound();
        }

        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioNotificacionId))
        {
            await dbContext.Notificaciones
                .Where(n => n.IdUsuario == usuarioNotificacionId && n.IdEvento == id && !n.Leida)
                .ExecuteUpdateAsync(update => update.SetProperty(n => n.Leida, true));
        }

        if (!evento.Activo)
        {
            return NotFound();
        }

        if (evento.EstadoAprobacion != EstadoAprobacion.Aprobado &&
            (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var lectorId) ||
             (evento.IdUsuarioSolicitante != lectorId &&
              evento.IdUsuarioOrganizador != lectorId &&
              !User.IsInRole("Administrador") &&
              !User.IsInRole("Admin"))))
        {
            return NotFound();
        }

        var registroActual = (Registro?)null;
        var puedeEditar = false;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            registroActual = evento.Registros.FirstOrDefault(r => r.IdUsuario == usuarioId);
            var esAdmin = User.IsInRole("Administrador") || User.IsInRole("Admin");
            puedeEditar = esAdmin ||
                evento.IdUsuarioOrganizador == usuarioId ||
                evento.IdUsuarioSolicitante == usuarioId;
        }

        ViewBag.EstaRegistrado = registroActual is not null;
        ViewBag.EstaInteresado = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioInteresadoId) &&
            evento.Intereses.Any(interes => interes.IdUsuario == usuarioInteresadoId);
        ViewBag.IdRegistroActual = registroActual?.IdRegistro;
        ViewBag.PuedeEditar = puedeEditar;
        ViewBag.IdsConAsistencia = evento.Asistencias.Select(a => a.IdUsuario).ToHashSet();

        return View("~/Views/Estudiante/DetalleEvento.cshtml", evento);
    }

    [Authorize]
    public async Task<IActionResult> MisRegistros(DateTime? fechaDesde, DateTime? fechaHasta)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date)
        {
            ModelState.AddModelError(nameof(fechaHasta), "La fecha final debe ser igual o posterior a la fecha inicial.");
        }

        var registrosQuery = dbContext.Registros
            .AsNoTrackingWithIdentityResolution()
            .Where(registro => registro.IdUsuario == usuarioId && registro.Evento.Activo)
            .Include(registro => registro.Evento)
                .ThenInclude(evento => evento.Organizador)
            .AsQueryable();
        var registrosProyectosQuery = dbContext.RegistrosProyectos
            .AsNoTrackingWithIdentityResolution()
            .Where(registro =>
                registro.IdUsuario == usuarioId &&
                registro.Proyecto.Activo &&
                registro.Estado != EstadoRegistroProyecto.Rechazado)
            .Include(registro => registro.Proyecto)
                .ThenInclude(proyecto => proyecto.Carrera)
            .Include(registro => registro.Proyecto)
                .ThenInclude(proyecto => proyecto.Creador)
            .Include(registro => registro.Proyecto)
                .ThenInclude(proyecto => proyecto.Organizador)
            .AsQueryable();
        var interesesEventosQuery = dbContext.InteresesEventos
            .AsNoTrackingWithIdentityResolution()
            .Where(interes => interes.IdUsuario == usuarioId &&
                interes.Evento.Activo &&
                interes.Evento.EstadoAprobacion == EstadoAprobacion.Aprobado &&
                !dbContext.Registros.Any(registro =>
                    registro.IdEvento == interes.IdEvento && registro.IdUsuario == usuarioId))
            .Include(interes => interes.Evento)
                .ThenInclude(evento => evento.Organizador)
            .Include(interes => interes.Evento)
                .ThenInclude(evento => evento.Registros)
            .AsQueryable();
        var interesesProyectosQuery = dbContext.InteresesProyectos
            .AsNoTrackingWithIdentityResolution()
            .Where(interes => interes.IdUsuario == usuarioId &&
                interes.Proyecto.Activo &&
                interes.Proyecto.EstadoAprobacion == EstadoAprobacion.Aprobado &&
                !dbContext.RegistrosProyectos.Any(registro =>
                    registro.IdProyecto == interes.IdProyecto &&
                    registro.IdUsuario == usuarioId &&
                    registro.Estado != EstadoRegistroProyecto.Rechazado))
            .Include(interes => interes.Proyecto)
                .ThenInclude(proyecto => proyecto.Carrera)
            .Include(interes => interes.Proyecto)
                .ThenInclude(proyecto => proyecto.Creador)
            .Include(interes => interes.Proyecto)
                .ThenInclude(proyecto => proyecto.Organizador)
            .Include(interes => interes.Proyecto)
                .ThenInclude(proyecto => proyecto.Registros)
            .AsQueryable();

        if (!(fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date))
        {
            if (fechaDesde.HasValue)
            {
                var desde = fechaDesde.Value.Date;
                registrosQuery = registrosQuery.Where(registro => registro.Evento.FechaHora >= desde);
                registrosProyectosQuery = registrosProyectosQuery.Where(registro => registro.Proyecto.FechaFin >= desde);
                interesesEventosQuery = interesesEventosQuery.Where(interes => interes.Evento.FechaHora >= desde);
                interesesProyectosQuery = interesesProyectosQuery.Where(interes => interes.Proyecto.FechaFin >= desde);
            }

            if (fechaHasta.HasValue)
            {
                var hasta = fechaHasta.Value.Date.AddDays(1);
                registrosQuery = registrosQuery.Where(registro => registro.Evento.FechaHora < hasta);
                registrosProyectosQuery = registrosProyectosQuery.Where(registro => registro.Proyecto.FechaInicio < hasta);
                interesesEventosQuery = interesesEventosQuery.Where(interes => interes.Evento.FechaHora < hasta);
                interesesProyectosQuery = interesesProyectosQuery.Where(interes => interes.Proyecto.FechaInicio < hasta);
            }
        }

        var registros = await registrosQuery
            .OrderByDescending(registro => registro.FechaRegistro)
            .ToListAsync();
        var registrosProyectos = await registrosProyectosQuery
            .OrderByDescending(registro => registro.FechaRegistro)
            .ToListAsync();
        var interesesEventos = await interesesEventosQuery
            .ToListAsync();
        var interesesProyectos = await interesesProyectosQuery
            .ToListAsync();

        var notificaciones = await dbContext.Notificaciones
            .AsNoTracking()
            .Where(n => n.IdUsuario == usuarioId && !n.Leida)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(20)
            .ToListAsync();

        var eventosAcreditadosIds = await dbContext.AsistenciasEventos
            .AsNoTracking()
            .Where(a => a.IdUsuario == usuarioId)
            .Select(a => a.IdEvento)
            .ToHashSetAsync();

        ViewBag.RegistrosProyectos = registrosProyectos;
        ViewBag.InteresesEventos = interesesEventos;
        ViewBag.InteresesProyectos = interesesProyectos;
        ViewBag.Notificaciones = notificaciones;
        ViewBag.EventosAcreditadosIds = eventosAcreditadosIds;
        ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");

        return View("~/Views/Estudiante/AgendaPersonal.cshtml", registros);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarNotificacionLeida(int id, string? returnUrl = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        await dbContext.Notificaciones
            .Where(n => n.IdNotificacion == id && n.IdUsuario == usuarioId)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.Leida, true));

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction(nameof(MisRegistros));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarTodasLeidas(string? returnUrl = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        await dbContext.Notificaciones
            .Where(n => n.IdUsuario == usuarioId && !n.Leida)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.Leida, true));

        TempData["Mensaje"] = "Se marcaron los avisos como leídos.";

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction(nameof(MisRegistros));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MisEventos()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (!await EsOrganizadorOAdminAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var usuarioActual = await dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == usuarioId);

        if (usuarioActual is null)
        {
            return Challenge();
        }

        var esAdmin = usuarioActual.IdRol == 3 || usuarioActual.Rol.Nombre.Contains("Admin");
        var misEventos = await dbContext.Eventos
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(e => e.Organizador)
            .Include(e => e.Registros)
            .Include(e => e.Intereses)
            .Include(e => e.Asistencias)
            .Where(e =>
                e.IdUsuarioOrganizador == usuarioId ||
                e.IdUsuarioSolicitante == usuarioId)
            .OrderBy(e => e.Orden == 0 ? int.MaxValue : e.Orden)
            .ThenByDescending(e => e.FechaHora)
            .ToListAsync();

        var proyectosResponsable = await dbContext.Proyectos
            .AsNoTrackingWithIdentityResolution()
            .Include(p => p.Carrera)
            .Include(p => p.Creador)
            .Include(p => p.Organizador)
            .Include(p => p.Registros)
            .Where(p =>
                p.IdUsuarioOrganizador == usuarioId ||
                p.IdUsuarioCreador == usuarioId)
            .OrderBy(p => p.Orden == 0 ? int.MaxValue : p.Orden)
            .ThenByDescending(p => p.FechaPublicacion)
            .ToListAsync();

        ViewBag.ProyectosACargo = proyectosResponsable;
        ViewBag.EsAdministrador = esAdmin;
        ViewBag.UsuarioActualId = usuarioId;
        return View("~/Views/Organizador/PanelOrganizador.cshtml", misEventos);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ordenar([FromBody] int[] ids)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (!await EsOrganizadorOAdminAsync())
        {
            return Forbid();
        }

        var esAdmin = User.IsInRole("Administrador") ||
            User.IsInRole("Admin") ||
            await dbContext.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .AnyAsync(u =>
                    u.IdUsuario == usuarioId &&
                    u.Activo &&
                    (u.IdRol == 3 || u.Rol.Nombre.Contains("Admin")));

        var resultado = await ServicioOrdenPublicaciones.OrdenarEventosUsuarioAsync(
            dbContext,
            usuarioId,
            esAdmin,
            ids);

        return resultado switch
        {
            ResultadoOrdenamiento.Correcto => Ok(),
            ResultadoOrdenamiento.Prohibido => Forbid(),
            _ => BadRequest()
        };
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Inscritos(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var evento = await dbContext.Eventos
            .AsNoTracking()
            .Include(e => e.Organizador)
            .FirstOrDefaultAsync(e => e.IdEvento == id);

        if (evento is null)
        {
            return NotFound();
        }

        if (!await PuedeGestionarEventoAsync(evento, usuarioId))
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var idsInscritos = await dbContext.Registros
            .AsNoTracking()
            .Where(registro => registro.IdEvento == id)
            .Select(registro => registro.IdUsuario)
            .ToListAsync();
        var idsInteresados = await dbContext.InteresesEventos
            .AsNoTracking()
            .Where(interes => interes.IdEvento == id)
            .Select(interes => interes.IdUsuario)
            .ToListAsync();
        var idsParticipantes = idsInscritos.Concat(idsInteresados).ToHashSet();
        var usuarios = await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Carrera)
            .Where(usuario => idsParticipantes.Contains(usuario.IdUsuario))
            .OrderBy(usuario => usuario.ApellidoPaterno)
            .ThenBy(usuario => usuario.Nombres)
            .ToListAsync();
        var asistencias = await dbContext.AsistenciasEventos
            .AsNoTracking()
            .Where(asistencia => asistencia.IdEvento == id && idsParticipantes.Contains(asistencia.IdUsuario))
            .ToDictionaryAsync(asistencia => asistencia.IdUsuario, asistencia => (DateTime?)asistencia.FechaAsistencia);

        var usuariosDisponibles = await dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.Carrera)
            .Where(u =>
                u.Activo &&
                !idsParticipantes.Contains(u.IdUsuario) &&
                u.IdUsuario != evento.IdUsuarioOrganizador &&
                u.IdUsuario != evento.IdUsuarioSolicitante)
            .OrderBy(u => u.Nombres)
            .ThenBy(u => u.ApellidoPaterno)
            .Select(u => new
            {
                u.IdUsuario,
                Etiqueta = $"{u.Nombres} {u.ApellidoPaterno} {u.ApellidoMaterno}".Trim() +
                           $" — {u.Correo}" +
                           (u.Carrera != null ? $" ({u.Carrera.Nombre})" : "")
            })
            .ToListAsync();

        ViewBag.UsuariosDisponibles = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            usuariosDisponibles,
            "IdUsuario",
            "Etiqueta");

        var esAdmin = User.IsInRole("Administrador") ||
            User.IsInRole("Admin") ||
            await dbContext.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .AnyAsync(u =>
                    u.IdUsuario == usuarioId &&
                    u.Activo &&
                    (u.IdRol == 3 || u.Rol.Nombre.Contains("Admin")));

        var eventosGestionables = await dbContext.Eventos
            .AsNoTracking()
            .Where(e =>
                esAdmin ||
                e.IdUsuarioOrganizador == usuarioId ||
                e.IdUsuarioSolicitante == usuarioId)
            .OrderBy(e => e.Orden == 0 ? int.MaxValue : e.Orden)
            .ThenByDescending(e => e.FechaHora)
            .ToListAsync();

        var modelo = new ListaAsistenciaViewModel
        {
            Evento = evento,
            IdEventoSeleccionado = evento.IdEvento,
            Eventos = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                eventosGestionables,
                nameof(Evento.IdEvento),
                nameof(Evento.Nombre),
                evento.IdEvento),
            Participantes = usuarios.Select(usuario => new ParticipanteAsistenciaViewModel
            {
                IdUsuario = usuario.IdUsuario,
                NombreCompleto = string.Join(" ", new[]
                {
                    usuario.Nombres,
                    usuario.ApellidoPaterno,
                    usuario.ApellidoMaterno
                }.Where(nombre => !string.IsNullOrWhiteSpace(nombre))),
                Correo = usuario.Correo,
                Carrera = usuario.Carrera?.Nombre ?? "General",
                Inscrito = idsInscritos.Contains(usuario.IdUsuario),
                Interesado = idsInteresados.Contains(usuario.IdUsuario),
                Asistio = asistencias.ContainsKey(usuario.IdUsuario),
                FechaAcreditacion = asistencias.GetValueOrDefault(usuario.IdUsuario)
            }).ToList()
        };

        return View("~/Views/Organizador/ControlAsistenciaEvento.cshtml", modelo);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarParticipanteAsistencia(int id, int? idUsuario, string? busquedaUsuario)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioActualId))
        {
            return Challenge();
        }

        var evento = await dbContext.Eventos.FirstOrDefaultAsync(e => e.IdEvento == id);
        if (evento is null)
        {
            return NotFound();
        }

        if (!await PuedeGestionarEventoAsync(evento, usuarioActualId))
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        if (DateTime.Today < evento.FechaHora.Date)
        {
            TempData["Mensaje"] = $"El pase de participación estará disponible a partir del día de inicio del evento ({evento.FechaHora:dd/MM/yyyy}).";
            return RedirectToAction(nameof(Inscritos), new { id });
        }

        Usuario? usuario = null;
        if (idUsuario.HasValue && idUsuario.Value > 0)
        {
            usuario = await dbContext.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value && u.Activo);
        }

        if (usuario is null && !string.IsNullOrWhiteSpace(busquedaUsuario))
        {
            var termino = busquedaUsuario.Trim();
            var candidatos = await dbContext.Usuarios
                .AsNoTracking()
                .Include(u => u.Carrera)
                .Where(u =>
                    u.Activo &&
                    u.IdUsuario != evento.IdUsuarioOrganizador &&
                    u.IdUsuario != evento.IdUsuarioSolicitante)
                .ToListAsync();

            usuario = candidatos.FirstOrDefault(u =>
                string.Equals(u.Correo, termino, StringComparison.OrdinalIgnoreCase) ||
                termino.Contains(u.Correo, StringComparison.OrdinalIgnoreCase) ||
                $"{u.Nombres} {u.ApellidoPaterno} {u.ApellidoMaterno}".Trim().Contains(termino, StringComparison.OrdinalIgnoreCase));
        }

        if (usuario is null)
        {
            TempData["Mensaje"] = "No se encontró un usuario activo con ese nombre o correo. Selecciónalo de las sugerencias al escribir.";
            return RedirectToAction(nameof(Inscritos), new { id });
        }

        var ahora = DateTime.UtcNow;
        if (!await dbContext.Registros.AnyAsync(r => r.IdEvento == id && r.IdUsuario == usuario.IdUsuario))
        {
            dbContext.Registros.Add(new Registro
            {
                IdEvento = id,
                IdUsuario = usuario.IdUsuario,
                FechaRegistro = ahora
            });
        }

        if (!await dbContext.AsistenciasEventos.AnyAsync(a => a.IdEvento == id && a.IdUsuario == usuario.IdUsuario))
        {
            dbContext.AsistenciasEventos.Add(new AsistenciaEvento
            {
                IdEvento = id,
                IdUsuario = usuario.IdUsuario,
                FechaAsistencia = ahora
            });
        }

        await dbContext.SaveChangesAsync();
        TempData["Mensaje"] = $"Se agregó y acreditó la asistencia de {usuario.Nombres} {usuario.ApellidoPaterno}.";
        return RedirectToAction(nameof(Inscritos), new { id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarAsistencia(int id, List<int>? usuariosAsistieron)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var evento = await dbContext.Eventos
            .FirstOrDefaultAsync(item => item.IdEvento == id);
        if (evento is null)
        {
            return NotFound();
        }

        if (!await PuedeGestionarEventoAsync(evento, usuarioId))
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        if (DateTime.Today < evento.FechaHora.Date)
        {
            TempData["Mensaje"] = $"El pase de participación estará disponible a partir del día de inicio del evento ({evento.FechaHora:dd/MM/yyyy}).";
            return RedirectToAction(nameof(Inscritos), new { id });
        }

        var idsParticipantes = await dbContext.Registros
            .Where(registro => registro.IdEvento == id)
            .Select(registro => registro.IdUsuario)
            .Union(dbContext.InteresesEventos
                .Where(interes => interes.IdEvento == id)
                .Select(interes => interes.IdUsuario))
            .ToListAsync();
        var idsParticipantesSet = idsParticipantes.ToHashSet();
        var idsQueAsistieron = (usuariosAsistieron ?? new List<int>()).ToHashSet();

        if (!idsQueAsistieron.IsSubsetOf(idsParticipantesSet))
        {
            return BadRequest();
        }

        var asistenciasActuales = await dbContext.AsistenciasEventos
            .Where(asistencia =>
                asistencia.IdEvento == id &&
                idsParticipantesSet.Contains(asistencia.IdUsuario))
            .ToListAsync();
        var idsConAsistenciaActual = asistenciasActuales
            .Select(asistencia => asistencia.IdUsuario)
            .ToHashSet();

        dbContext.AsistenciasEventos.RemoveRange(
            asistenciasActuales.Where(asistencia => !idsQueAsistieron.Contains(asistencia.IdUsuario)));
        dbContext.AsistenciasEventos.AddRange(
            idsQueAsistieron
                .Where(idParticipante => !idsConAsistenciaActual.Contains(idParticipante))
                .Select(idParticipante => new AsistenciaEvento
                {
                    IdEvento = id,
                    IdUsuario = idParticipante,
                    FechaAsistencia = DateTime.UtcNow
                }));

        await dbContext.SaveChangesAsync();
        TempData["Mensaje"] = $"Se guardó la asistencia de {idsQueAsistieron.Count} personas.";
        return RedirectToAction(nameof(Inscritos), new { id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var modelo = new CrearEventoViewModel();
        await CargarOrganizadoresAsync(modelo);
        return View("~/Views/Estudiante/PublicarEvento.cshtml", modelo);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearEventoViewModel modelo)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (modelo.FechaFin <= modelo.FechaHora)
        {
            ModelState.AddModelError(nameof(modelo.FechaFin), "La fecha de fin debe ser posterior al inicio.");
        }

        var esOrganizadorOAdmin = await EsOrganizadorOAdminAsync();
        if (esOrganizadorOAdmin && modelo.IdUsuarioOrganizador <= 0)
        {
            modelo.IdUsuarioOrganizador = usuarioId;
        }

        var organizador = await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .FirstOrDefaultAsync(usuario =>
                usuario.IdUsuario == modelo.IdUsuarioOrganizador &&
                usuario.Activo &&
                (usuario.IdRol == 2 ||
                 usuario.Rol.Nombre.Contains("Organizador") ||
                 (esOrganizadorOAdmin && usuario.IdUsuario == usuarioId)));
        if (organizador is null)
        {
            ModelState.AddModelError(nameof(modelo.IdUsuarioOrganizador), "Selecciona un organizador activo.");
        }
        else if (!esOrganizadorOAdmin && organizador.IdUsuario == usuarioId)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioOrganizador),
                "Selecciona a otro organizador para que revise tu solicitud.");
        }

        ValidarImagenEvento(modelo.ImagenArchivo, nameof(modelo.ImagenArchivo));

        if (!ModelState.IsValid)
        {
            await CargarOrganizadoresAsync(modelo);
            return View("~/Views/Estudiante/PublicarEvento.cshtml", modelo);
        }

        var imagenUrl = await GuardarImagenEventoAsync(modelo.ImagenArchivo);
        var autoAprobado = esOrganizadorOAdmin && organizador!.IdUsuario == usuarioId;

        var nuevoEvento = new Evento
        {
            Nombre = modelo.Nombre.Trim(),
            Descripcion = modelo.Descripcion.Trim(),
            FechaHora = modelo.FechaHora,
            FechaFin = modelo.FechaFin,
            Cupo = modelo.Cupo,
            Lugar = modelo.Lugar.Trim(),
            ImagenUrl = imagenUrl,
            IdUsuarioOrganizador = organizador!.IdUsuario,
            IdUsuarioSolicitante = usuarioId,
            EstadoAprobacion = autoAprobado ? EstadoAprobacion.Aprobado : EstadoAprobacion.Pendiente,
            FechaSolicitud = DateTime.UtcNow,
            FechaCreacion = DateTime.UtcNow
        };

        await ServicioOrdenPublicaciones.CrearEventoAsync(dbContext, nuevoEvento);
        if (!autoAprobado)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = nuevoEvento.IdUsuarioOrganizador,
                IdEvento = nuevoEvento.IdEvento,
                Titulo = $"Nueva solicitud de evento: {nuevoEvento.Nombre}",
                Mensaje = $"{User.Identity?.Name ?? "Un estudiante"} envió un evento para tu revisión.",
                FechaCreacion = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        TempData["Mensaje"] = autoAprobado
            ? "El evento se publicó directamente bajo tu responsabilidad."
            : "Tu solicitud de evento se envió al organizador para su aprobación.";
        return autoAprobado
            ? RedirectToAction(nameof(Detalle), new { id = nuevoEvento.IdEvento })
            : RedirectToAction("Index", "Solicitudes");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var evento = await dbContext.Eventos.AsNoTracking().FirstOrDefaultAsync(e => e.IdEvento == id && e.Activo);
        if (evento is null)
        {
            return NotFound();
        }

        var esAdmin = User.IsInRole("Administrador") || User.IsInRole("Admin");
        if (!esAdmin && evento.IdUsuarioOrganizador != usuarioId && evento.IdUsuarioSolicitante != usuarioId)
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        return View("~/Views/Estudiante/EditarEvento.cshtml", new EditarEventoViewModel
        {
            IdEvento = evento.IdEvento,
            Nombre = evento.Nombre,
            Descripcion = evento.Descripcion,
            FechaHora = evento.FechaHora,
            FechaFin = evento.FechaFin,
            Cupo = evento.Cupo,
            Lugar = evento.Lugar,
            ImagenUrlActual = evento.ImagenUrl
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(EditarEventoViewModel modelo)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (modelo.FechaFin <= modelo.FechaHora)
        {
            ModelState.AddModelError(nameof(modelo.FechaFin), "La fecha de fin debe ser posterior al inicio.");
        }

        ValidarImagenEvento(modelo.ImagenArchivo, nameof(modelo.ImagenArchivo));

        var evento = await dbContext.Eventos.FirstOrDefaultAsync(e => e.IdEvento == modelo.IdEvento && e.Activo);
        if (evento is null)
        {
            return NotFound();
        }

        var esAdmin = User.IsInRole("Administrador") || User.IsInRole("Admin");
        if (!esAdmin && evento.IdUsuarioOrganizador != usuarioId && evento.IdUsuarioSolicitante != usuarioId)
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        if (!ModelState.IsValid)
        {
            modelo.ImagenUrlActual = evento.ImagenUrl;
            return View("~/Views/Estudiante/EditarEvento.cshtml", modelo);
        }

        var estabaAprobado = evento.EstadoAprobacion == EstadoAprobacion.Aprobado;
        // Detectar cambios para informar a los usuarios registrados
        var nuevoNombre = modelo.Nombre.Trim();
        var nuevaDescripcion = modelo.Descripcion.Trim();
        var nuevoLugar = modelo.Lugar.Trim();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var registrosExistentes = await dbContext.Registros
            .CountAsync(registro => registro.IdEvento == evento.IdEvento);
        if (modelo.Cupo.HasValue && modelo.Cupo.Value < registrosExistentes)
        {
            ModelState.AddModelError(nameof(modelo.Cupo), "El cupo no puede ser menor que las inscripciones actuales.");
        }

        if (!ModelState.IsValid)
        {
            await transaction.RollbackAsync();
            modelo.ImagenUrlActual = evento.ImagenUrl;
            return View("~/Views/Estudiante/EditarEvento.cshtml", modelo);
        }

        if (modelo.ImagenArchivo is { Length: > 0 })
        {
            EliminarImagenFisica(evento.ImagenUrl);
            evento.ImagenUrl = await GuardarImagenEventoAsync(modelo.ImagenArchivo);
        }

        evento.Nombre = nuevoNombre;
        evento.Descripcion = nuevaDescripcion;
        evento.FechaHora = modelo.FechaHora;
        evento.FechaFin = modelo.FechaFin;
        evento.Cupo = modelo.Cupo;
        evento.Lugar = nuevoLugar;
        if (estabaAprobado)
        {
            var usuariosRegistrados = await dbContext.Registros
                .AsNoTracking()
                .Where(registro => registro.IdEvento == evento.IdEvento)
                .Select(registro => registro.IdUsuario)
                .Distinct()
                .ToListAsync();
            foreach (var idUsuario in usuariosRegistrados)
            {
                dbContext.Notificaciones.Add(new Notificacion
                {
                    IdUsuario = idUsuario,
                    IdEvento = evento.IdEvento,
                    Titulo = $"El evento «{evento.Nombre}» fue actualizado",
                    Mensaje = "El organizador actualizó la información del evento. Consulta la publicación para ver los cambios.",
                    FechaCreacion = DateTime.UtcNow
                });
            }
        }
        else
        {
            evento.EstadoAprobacion = EstadoAprobacion.Pendiente;
            evento.ComentarioAprobacion = null;
            evento.FechaRevision = null;
            evento.IdUsuarioRevisor = null;
            evento.FechaSolicitud = DateTime.UtcNow;
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = evento.IdUsuarioOrganizador,
                IdEvento = evento.IdEvento,
                Titulo = $"Solicitud de evento actualizada: {evento.Nombre}",
                Mensaje = $"{User.Identity?.Name ?? "Un estudiante"} actualizó el evento y requiere una nueva revisión.",
                FechaCreacion = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Mensaje"] = estabaAprobado
            ? "Los cambios se guardaron y se notificó a las personas registradas."
            : "Los cambios se enviaron al organizador para su aprobación.";

        return RedirectToAction(nameof(Detalle), new { id = evento.IdEvento });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id, string? motivo)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var evento = await dbContext.Eventos.FirstOrDefaultAsync(e => e.IdEvento == id);
        if (evento is null)
        {
            return NotFound();
        }

        var esAdmin = User.IsInRole("Administrador") || User.IsInRole("Admin");
        var esOrganizador = await EsOrganizadorOAdminAsync();
        if (!esAdmin &&
            (!esOrganizador || evento.IdUsuarioOrganizador != usuarioId) &&
            evento.IdUsuarioSolicitante != usuarioId)
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        if (!evento.Activo)
        {
            TempData["Mensaje"] = "El evento ya está inactivo.";
            return esOrganizador
                ? RedirectToAction(nameof(MisEventos))
                : RedirectToAction("Index", "Solicitudes");
        }

        var motivoLimpio = motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivoLimpio) || motivoLimpio.Length > 1000)
        {
            TempData["Mensaje"] = "Indica un motivo de eliminación de hasta 1000 caracteres.";
            return esOrganizador
                ? RedirectToAction(nameof(MisEventos))
                : RedirectToAction("Index", "Solicitudes");
        }

        evento.Activo = false;
        evento.MotivoEliminacion = motivoLimpio;
        evento.FechaEliminacion = DateTime.UtcNow;
        evento.IdUsuarioEliminador = usuarioId;
        if (evento.IdUsuarioSolicitante != usuarioId)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = evento.IdUsuarioSolicitante,
                IdEvento = evento.IdEvento,
                Titulo = $"El evento «{evento.Nombre}» fue eliminado",
                Mensaje = $"El organizador eliminó este evento. Motivo: {motivoLimpio}",
                FechaCreacion = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = "El evento fue eliminado de tu panel. Se notificó al creador y se conservaron sus datos e inscripciones.";
        return esOrganizador
            ? RedirectToAction(nameof(MisEventos))
            : RedirectToAction("Index", "Solicitudes");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarInteres(int id, string? returnUrl = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var evento = await dbContext.Eventos
            .FirstOrDefaultAsync(item =>
                item.IdEvento == id &&
                item.Activo &&
                item.EstadoAprobacion == EstadoAprobacion.Aprobado);
        if (evento is null)
        {
            return NotFound();
        }

        var registro = await dbContext.Registros
            .FirstOrDefaultAsync(item => item.IdUsuario == usuarioId && item.IdEvento == id);
        if (registro is null)
        {
            if (evento.Cupo.HasValue &&
                await dbContext.Registros.CountAsync(item => item.IdEvento == id) >= evento.Cupo.Value)
            {
                TempData["Mensaje"] = "El evento ya alcanzó su cupo máximo.";
                return RedirectToAction(nameof(Detalle), new { id });
            }

            dbContext.Registros.Add(new Registro
            {
                IdUsuario = usuarioId,
                IdEvento = id,
                FechaRegistro = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Mensaje"] = "Tu asistencia quedó registrada en el evento.";
        }
        else
        {
            TempData["Mensaje"] = "Ya estás registrado en este evento.";
        }

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarInteres(int id, string? returnUrl = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var evento = await dbContext.Eventos
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.IdEvento == id &&
                item.Activo &&
                item.EstadoAprobacion == EstadoAprobacion.Aprobado);
        if (evento is null)
        {
            return NotFound();
        }

        var interes = await dbContext.InteresesEventos
            .FirstOrDefaultAsync(item => item.IdUsuario == usuarioId && item.IdEvento == id);
        var marcado = interes is null;
        if (interes is null)
        {
            dbContext.InteresesEventos.Add(new InteresEvento
            {
                IdUsuario = usuarioId,
                IdEvento = id,
                FechaInteres = DateTime.UtcNow
            });
        }
        else
        {
            dbContext.InteresesEventos.Remove(interes);
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Mensaje"] = marcado
            ? "Marcaste que te interesa este evento. Esto no es una inscripción."
            : "Quitaste este evento de tus intereses.";

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarRegistro(int id, string? returnUrl = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        await dbContext.Registros
            .Where(item =>
                item.IdUsuario == usuarioId &&
                (item.IdRegistro == id || item.IdEvento == id))
            .ExecuteDeleteAsync();

        TempData["Mensaje"] = "Se canceló tu registro al evento.";

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction(nameof(MisRegistros));
    }

    private void ValidarImagenEvento(IFormFile? archivo, string nombreCampo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return;
        }

        var extension = Path.GetExtension(archivo.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !ExtensionesImagenPermitidas.Contains(extension))
        {
            ModelState.AddModelError(nombreCampo, "Formato de imagen no válido. Usa JPG, PNG, WEBP o GIF.");
            return;
        }

        if (archivo.Length > TamanoMaximoImagenBytes)
        {
            ModelState.AddModelError(nombreCampo, "La imagen no debe superar los 5 MB.");
        }
    }

    private async Task<string?> GuardarImagenEventoAsync(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return null;
        }

        var carpetaDestino = Path.Combine(environment.WebRootPath, "uploads", "eventos");
        Directory.CreateDirectory(carpetaDestino);

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
        var rutaCompleta = Path.Combine(carpetaDestino, nombreArchivo);

        await using var stream = new FileStream(rutaCompleta, FileMode.Create);
        await archivo.CopyToAsync(stream);

        return $"/uploads/eventos/{nombreArchivo}";
    }

    private void EliminarImagenFisica(string? imagenUrl)
    {
        if (string.IsNullOrWhiteSpace(imagenUrl) || !imagenUrl.StartsWith("/uploads/eventos/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var rutaRelativa = imagenUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var rutaCompleta = Path.Combine(environment.WebRootPath, rutaRelativa);
        if (System.IO.File.Exists(rutaCompleta))
        {
            try
            {
                System.IO.File.Delete(rutaCompleta);
            }
            catch
            {
                // Ignorar error si el archivo está temporalmente en uso.
            }
        }
    }

    private async Task<bool> EsOrganizadorOAdminAsync()
    {
        if (User.IsInRole("Organizador/Docente") ||
            User.IsInRole("Organizador") ||
            User.IsInRole("Administrador") ||
            User.IsInRole("Admin"))
        {
            return true;
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return false;
        }

        return await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .AnyAsync(usuario =>
                usuario.IdUsuario == usuarioId &&
                usuario.Activo &&
                (usuario.IdRol == 2 ||
                 usuario.IdRol == 3 ||
                 usuario.Rol.Nombre.Contains("Organizador") ||
                 usuario.Rol.Nombre.Contains("Admin")));
    }

    private async Task<bool> PuedeGestionarEventoAsync(Evento evento, int usuarioId)
    {
        if (evento.IdUsuarioOrganizador == usuarioId || evento.IdUsuarioSolicitante == usuarioId)
        {
            return true;
        }

        return User.IsInRole("Administrador") ||
            User.IsInRole("Admin") ||
            await dbContext.Usuarios
                .AsNoTracking()
                .Include(usuario => usuario.Rol)
                .AnyAsync(usuario =>
                    usuario.IdUsuario == usuarioId &&
                    usuario.Activo &&
                    (usuario.IdRol == 3 || usuario.Rol.Nombre.Contains("Admin")));
    }

    private async Task CargarOrganizadoresAsync(CrearEventoViewModel? modelo = null)
    {
        var esOrganizadorOAdmin = await EsOrganizadorOAdminAsync();
        var usuarioActualId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        var organizadores = await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .Where(usuario =>
                usuario.Activo &&
                (usuario.IdRol == 2 ||
                 usuario.Rol.Nombre.Contains("Organizador") ||
                 (esOrganizadorOAdmin && usuario.IdUsuario == usuarioActualId)))
            .OrderBy(usuario => usuario.Nombres)
            .ThenBy(usuario => usuario.ApellidoPaterno)
            .Select(usuario => new
            {
                usuario.IdUsuario,
                NombreCompleto = usuario.Nombres + " " + usuario.ApellidoPaterno +
                    (usuario.ApellidoMaterno != null ? " " + usuario.ApellidoMaterno : "") +
                    " (" + usuario.Rol.Nombre + ")"
            })
            .ToListAsync();

        var selectOrganizadores = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            organizadores,
            "IdUsuario",
            "NombreCompleto",
            modelo?.IdUsuarioOrganizador);

        ViewBag.EsOrganizadorOAdmin = esOrganizadorOAdmin;
        ViewBag.Organizadores = selectOrganizadores;
        if (modelo is not null)
        {
            modelo.Organizadores = selectOrganizadores;
        }
    }
}
