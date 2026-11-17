using System.Security.Claims;
using System.Data;
using GestionEventos.Data;
using GestionEventos.Models;
using GestionEventos.Servicios;
using GestionEventos.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionEventos.Controllers;

[Authorize]
public class ProyectosController(
    GestionEventosDbContext dbContext,
    IMemoryCache memoryCache,
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
            proyectosQuery = proyectosQuery.Where(proyecto => proyecto.IdCarrera == filtroCarrera);
        }

        if (!(fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date))
        {
            if (fechaDesde.HasValue)
            {
                var desde = fechaDesde.Value.Date;
                proyectosQuery = proyectosQuery.Where(proyecto => proyecto.FechaFin >= desde);
            }

            if (fechaHasta.HasValue)
            {
                var hasta = fechaHasta.Value.Date.AddDays(1);
                proyectosQuery = proyectosQuery.Where(proyecto => proyecto.FechaInicio < hasta);
            }
        }

        var proyectos = await proyectosQuery
            .OrderBy(proyecto => proyecto.Orden == 0 ? int.MaxValue : proyecto.Orden)
            .ThenByDescending(proyecto => proyecto.FechaPublicacion)
            .ToListAsync();

        var registradosIds = new HashSet<int>();
        var interesadosIds = new HashSet<int>();
        var usuarioActualId = usuarioId;
        if (usuarioId > 0)
        {
            registradosIds = proyectos
                .Where(p => p.Registros.Any(r =>
                    r.IdUsuario == usuarioId && r.Estado != EstadoRegistroProyecto.Rechazado))
                .Select(p => p.IdProyecto)
                .ToHashSet();
            interesadosIds = proyectos
                .Where(p => p.Intereses.Any(interes => interes.IdUsuario == usuarioId))
                .Select(p => p.IdProyecto)
                .ToHashSet();
        }

        ViewBag.ProyectosRegistradosIds = registradosIds;
        ViewBag.ProyectosInteresadosIds = interesadosIds;
        ViewBag.UsuarioActualId = usuarioActualId;
        ViewBag.Carreras = await dbContext.Carreras.AsNoTracking().OrderBy(carrera => carrera.Nombre).ToListAsync();
        ViewBag.IdCarreraSeleccionada = filtroCarrera;
        ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");
        return View("~/Views/Estudiante/CatalogoProyectos.cshtml", proyectos);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MisPropuestas()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var rolUsuario = await dbContext.Usuarios
            .AsNoTracking()
            .Where(u => u.IdUsuario == usuarioId)
            .Select(u => new { u.IdRol, NombreRol = u.Rol.Nombre })
            .FirstOrDefaultAsync();

        var esOrganizador = User.IsInRole("Organizador") ||
            User.IsInRole("Administrador") ||
            User.IsInRole("Admin") ||
            (rolUsuario is not null &&
             (rolUsuario.IdRol == 2 ||
              rolUsuario.IdRol == 3 ||
              rolUsuario.NombreRol.Contains("Organizador", StringComparison.OrdinalIgnoreCase) ||
              rolUsuario.NombreRol.Contains("Admin", StringComparison.OrdinalIgnoreCase)));

        if (esOrganizador)
        {
            return RedirectToAction("MisEventos", "Eventos");
        }

        var propuestas = await dbContext.Proyectos
            .AsNoTrackingWithIdentityResolution()
            .Include(proyecto => proyecto.Carrera)
            .Include(proyecto => proyecto.Organizador)
            .Include(proyecto => proyecto.Registros)
            .Include(proyecto => proyecto.Creador)
            .Where(proyecto =>
                proyecto.IdUsuarioCreador == usuarioId ||
                proyecto.IdUsuarioOrganizador == usuarioId)
            .OrderByDescending(proyecto => proyecto.FechaSolicitud)
            .ToListAsync();

        ViewBag.UsuarioActualId = usuarioId;
        ViewBag.EsOrganizador = false;
        return View("~/Views/Estudiante/MisPropuestasProyectos.cshtml", propuestas);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var proyecto = await dbContext.Proyectos
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(item => item.Carrera)
            .Include(item => item.Creador)
                .ThenInclude(usuario => usuario.Carrera)
            .Include(item => item.Organizador)
            .Include(item => item.Registros)
                .ThenInclude(registro => registro.Usuario)
                    .ThenInclude(usuario => usuario.Carrera)
            .Include(item => item.Intereses)
            .FirstOrDefaultAsync(item => item.IdProyecto == id);

        if (proyecto is null)
        {
            return NotFound();
        }

        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioNotificacionId))
        {
            await dbContext.Notificaciones
                .Where(n => n.IdUsuario == usuarioNotificacionId && n.IdProyecto == id && !n.Leida)
                .ExecuteUpdateAsync(update => update.SetProperty(n => n.Leida, true));
        }

        if (!proyecto.Activo)
        {
            return NotFound();
        }

        if (proyecto.EstadoAprobacion != EstadoAprobacion.Aprobado &&
            (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var lectorId) ||
             (proyecto.IdUsuarioCreador != lectorId &&
              proyecto.IdUsuarioOrganizador != lectorId &&
              !User.IsInRole("Administrador") &&
              !User.IsInRole("Admin"))))
        {
            return NotFound();
        }

        var registroActual = (RegistroProyecto?)null;
        var puedeEditar = false;
        var esEstudiante = User.IsInRole("Estudiante");
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            registroActual = proyecto.Registros.FirstOrDefault(r => r.IdUsuario == usuarioId);
            puedeEditar = await PuedeEditarProyectoAsync(proyecto, usuarioId);

            var rolUsuario = await dbContext.Usuarios
                .AsNoTracking()
                .Where(u => u.IdUsuario == usuarioId)
                .Select(u => new { u.IdRol, NombreRol = u.Rol.Nombre })
                .FirstOrDefaultAsync();
            if (rolUsuario is not null)
            {
                esEstudiante = rolUsuario.IdRol == 1 ||
                    rolUsuario.NombreRol.Contains("Estudiante", StringComparison.OrdinalIgnoreCase);
            }
        }

        ViewBag.EstaRegistrado = registroActual is not null &&
            registroActual.Estado != EstadoRegistroProyecto.Rechazado;
        ViewBag.EstaInteresado = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioInteresadoId) &&
            proyecto.Intereses.Any(interes => interes.IdUsuario == usuarioInteresadoId);
        ViewBag.PuedeEditar = puedeEditar;
        ViewBag.EsEstudiante = esEstudiante;
        ViewBag.EsProyectoPropio = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioPropietarioId) &&
            (proyecto.IdUsuarioCreador == usuarioPropietarioId ||
             proyecto.IdUsuarioOrganizador == usuarioPropietarioId);
        ViewBag.IdsConParticipacion = proyecto.Registros
            .Where(r => r.Estado != EstadoRegistroProyecto.Rechazado && r.FechaRevision.HasValue)
            .Select(r => r.IdUsuario)
            .ToHashSet();

        return View("~/Views/Estudiante/DetalleProyecto.cshtml", proyecto);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        await CargarCatalogosAsync();
        return View("~/Views/Estudiante/PublicarProyecto.cshtml", new CrearProyectoViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearProyectoViewModel modelo)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (modelo.FechaFin.Date < modelo.FechaInicio.Date)
        {
            ModelState.AddModelError(nameof(modelo.FechaFin), "La fecha de fin no puede ser anterior a la fecha de inicio.");
        }

        var carreras = await ObtenerCarrerasCacheAsync();
        if (!carreras.Any(carrera => carrera.IdCarrera == modelo.IdCarrera))
        {
            ModelState.AddModelError(nameof(modelo.IdCarrera), "Selecciona una carrera válida.");
        }

        var responsable = await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .FirstOrDefaultAsync(usuario =>
                usuario.IdUsuario == modelo.IdUsuarioOrganizador &&
                usuario.Activo &&
                (usuario.IdRol == 2 || usuario.Rol.Nombre.Contains("Organizador")));

        if (responsable is null)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioOrganizador),
                "Selecciona un usuario válido con rol Organizador.");
        }
        else if (responsable.IdUsuario == usuarioId)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioOrganizador),
                "Selecciona a otro organizador para que revise tu solicitud.");
        }

        ValidarImagenProyecto(modelo.ImagenArchivo, nameof(modelo.ImagenArchivo));

        if (!ModelState.IsValid)
        {
            await CargarCatalogosAsync();
            return View("~/Views/Estudiante/PublicarProyecto.cshtml", modelo);
        }

        var imagenUrl = await GuardarImagenProyectoAsync(modelo.ImagenArchivo);
        var maxOrden = await dbContext.Proyectos
            .AsNoTracking()
            .Select(p => (int?)p.Orden)
            .MaxAsync() ?? 0;

        var nuevoProyecto = new Proyecto
        {
            IdUsuarioCreador = usuarioId,
            IdUsuarioOrganizador = responsable!.IdUsuario,
            Nombre = modelo.Nombre.Trim(),
            Descripcion = modelo.Descripcion.Trim(),
            LugarReunion = modelo.LugarReunion.Trim(),
            FechaInicio = modelo.FechaInicio.Date,
            FechaFin = modelo.FechaFin.Date,
            Cupo = modelo.Cupo,
            ImagenUrl = imagenUrl,
            Orden = maxOrden > 0 ? maxOrden + 1 : 0,
            IdCarrera = modelo.IdCarrera,
            FechaPublicacion = DateTime.UtcNow,
            FechaSolicitud = DateTime.UtcNow,
            EstadoAprobacion = EstadoAprobacion.Pendiente
        };

        dbContext.Proyectos.Add(nuevoProyecto);
        dbContext.Notificaciones.Add(new Notificacion
        {
            IdUsuario = nuevoProyecto.IdUsuarioOrganizador.Value,
            Proyecto = nuevoProyecto,
            Titulo = $"Nueva solicitud de proyecto: {nuevoProyecto.Nombre}",
            Mensaje = $"{User.Identity?.Name ?? "Un estudiante"} envió un proyecto para tu revisión.",
            FechaCreacion = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = "Tu solicitud de proyecto se envió al organizador para su aprobación.";
        return RedirectToAction("Index", "Solicitudes");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var proyecto = await dbContext.Proyectos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdProyecto == id);

        if (proyecto is null)
        {
            return NotFound();
        }

        if (!proyecto.Activo)
        {
            return NotFound();
        }

        if (!await PuedeEditarProyectoAsync(proyecto, usuarioId))
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var responsables = await dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .Where(u =>
                u.Activo &&
                (u.IdRol == 2 || u.Rol.Nombre.Contains("Organizador")))
            .ToListAsync();

        var responsableActual = responsables.FirstOrDefault(u => u.IdUsuario == proyecto.IdUsuarioOrganizador);

        await CargarCatalogosAsync();

        return View("~/Views/Estudiante/EditarProyecto.cshtml", new EditarProyectoViewModel
        {
            IdProyecto = proyecto.IdProyecto,
            Nombre = proyecto.Nombre,
            Descripcion = proyecto.Descripcion,
            IdUsuarioOrganizador = responsableActual?.IdUsuario ?? 0,
            IdCarrera = proyecto.IdCarrera,
            LugarReunion = proyecto.LugarReunion,
            FechaInicio = proyecto.FechaInicio.Date,
            FechaFin = proyecto.FechaFin.Date,
            Cupo = proyecto.Cupo,
            ImagenUrlActual = proyecto.ImagenUrl
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(EditarProyectoViewModel modelo)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (modelo.FechaFin.Date < modelo.FechaInicio.Date)
        {
            ModelState.AddModelError(nameof(modelo.FechaFin), "La fecha de fin no puede ser anterior a la fecha de inicio.");
        }

        var proyecto = await dbContext.Proyectos.FirstOrDefaultAsync(p => p.IdProyecto == modelo.IdProyecto);
        if (proyecto is null)
        {
            return NotFound();
        }

        if (!proyecto.Activo)
        {
            return NotFound();
        }

        if (!await PuedeEditarProyectoAsync(proyecto, usuarioId))
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var carreras = await ObtenerCarrerasCacheAsync();
        if (!carreras.Any(carrera => carrera.IdCarrera == modelo.IdCarrera))
        {
            ModelState.AddModelError(nameof(modelo.IdCarrera), "Selecciona una carrera válida.");
        }

        var responsable = await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .FirstOrDefaultAsync(usuario =>
                usuario.IdUsuario == modelo.IdUsuarioOrganizador &&
                usuario.Activo &&
                (usuario.IdRol == 2 || usuario.Rol.Nombre.Contains("Organizador")));

        if (responsable is null)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioOrganizador),
                "Selecciona un usuario válido con rol Organizador.");
        }
        else if (responsable.IdUsuario == proyecto.IdUsuarioCreador)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioOrganizador),
                "El organizador responsable debe ser distinto de quien envió el proyecto.");
        }

        ValidarImagenProyecto(modelo.ImagenArchivo, nameof(modelo.ImagenArchivo));

        if (!ModelState.IsValid)
        {
            modelo.ImagenUrlActual = proyecto.ImagenUrl;
            await CargarCatalogosAsync();
            return View("~/Views/Estudiante/EditarProyecto.cshtml", modelo);
        }

        var nuevoNombre = modelo.Nombre.Trim();
        var nuevaDescripcion = modelo.Descripcion.Trim();
        var nuevoLugar = modelo.LugarReunion.Trim();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var registrosExistentes = await dbContext.RegistrosProyectos.CountAsync(registro =>
            registro.IdProyecto == proyecto.IdProyecto &&
            registro.Estado != EstadoRegistroProyecto.Rechazado);
        if (modelo.Cupo.HasValue && modelo.Cupo.Value < registrosExistentes)
        {
            ModelState.AddModelError(nameof(modelo.Cupo), "El cupo no puede ser menor que las inscripciones actuales.");
        }

        if (!ModelState.IsValid)
        {
            await transaction.RollbackAsync();
            modelo.ImagenUrlActual = proyecto.ImagenUrl;
            await CargarCatalogosAsync();
            return View("~/Views/Estudiante/EditarProyecto.cshtml", modelo);
        }

        if (modelo.ImagenArchivo is { Length: > 0 })
        {
            EliminarImagenFisica(proyecto.ImagenUrl);
            proyecto.ImagenUrl = await GuardarImagenProyectoAsync(modelo.ImagenArchivo);
        }

        var estabaAprobado = proyecto.EstadoAprobacion == EstadoAprobacion.Aprobado;
        proyecto.Nombre = nuevoNombre;
        proyecto.Descripcion = nuevaDescripcion;
        proyecto.IdUsuarioOrganizador = responsable!.IdUsuario;
        proyecto.LugarReunion = nuevoLugar;
        proyecto.FechaInicio = modelo.FechaInicio.Date;
        proyecto.FechaFin = modelo.FechaFin.Date;
        proyecto.Cupo = modelo.Cupo;
        proyecto.IdCarrera = modelo.IdCarrera;
        if (estabaAprobado)
        {
            var colaboradores = await dbContext.RegistrosProyectos
                .AsNoTracking()
                .Where(registro =>
                    registro.IdProyecto == proyecto.IdProyecto &&
                    registro.Estado != EstadoRegistroProyecto.Rechazado)
                .Select(registro => registro.IdUsuario)
                .Distinct()
                .ToListAsync();
            foreach (var idUsuario in colaboradores)
            {
                dbContext.Notificaciones.Add(new Notificacion
                {
                    IdUsuario = idUsuario,
                    IdProyecto = proyecto.IdProyecto,
                    Titulo = $"El proyecto «{proyecto.Nombre}» fue actualizado",
                    Mensaje = "El responsable actualizó la información del proyecto. Consulta la publicación para ver los cambios.",
                    FechaCreacion = DateTime.UtcNow
                });
            }
        }
        else
        {
            proyecto.EstadoAprobacion = EstadoAprobacion.Pendiente;
            proyecto.ComentarioAprobacion = null;
            proyecto.FechaRevision = null;
            proyecto.IdUsuarioRevisor = null;
            proyecto.FechaSolicitud = DateTime.UtcNow;
            if (proyecto.IdUsuarioOrganizador.HasValue)
            {
                dbContext.Notificaciones.Add(new Notificacion
                {
                    IdUsuario = proyecto.IdUsuarioOrganizador.Value,
                    IdProyecto = proyecto.IdProyecto,
                    Titulo = $"Solicitud de proyecto actualizada: {proyecto.Nombre}",
                    Mensaje = $"{User.Identity?.Name ?? "Un estudiante"} actualizó el proyecto y requiere una nueva revisión.",
                    FechaCreacion = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Mensaje"] = estabaAprobado
            ? "Los cambios se guardaron y se notificó a las personas registradas."
            : "Los cambios se enviaron al organizador para su aprobación.";

        return RedirectToAction(nameof(Detalle), new { id = proyecto.IdProyecto });
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
        var proyecto = await dbContext.Proyectos
            .FirstOrDefaultAsync(item =>
                item.IdProyecto == id &&
                item.Activo &&
                item.EstadoAprobacion == EstadoAprobacion.Aprobado);
        if (proyecto is null)
        {
            return NotFound();
        }

        if (proyecto.IdUsuarioCreador == usuarioId || proyecto.IdUsuarioOrganizador == usuarioId)
        {
            TempData["Mensaje"] = "No puedes registrarte en un proyecto que creaste o supervisas.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var registro = await dbContext.RegistrosProyectos
            .FirstOrDefaultAsync(item => item.IdUsuario == usuarioId && item.IdProyecto == id);
        var yaEstabaRegistrado = registro is not null &&
            registro.Estado != EstadoRegistroProyecto.Rechazado;
        var debeGuardarRegistro = !yaEstabaRegistrado;
        if (registro is null)
        {
            if (proyecto.Cupo.HasValue &&
                await dbContext.RegistrosProyectos.CountAsync(item =>
                    item.IdProyecto == id &&
                    item.Estado != EstadoRegistroProyecto.Rechazado) >= proyecto.Cupo.Value)
            {
                TempData["Mensaje"] = "El proyecto ya alcanzó su cupo máximo de colaboradores.";
                return RedirectToAction(nameof(Detalle), new { id });
            }

            registro = new RegistroProyecto
            {
                IdUsuario = usuarioId,
                IdProyecto = id,
                FechaRegistro = DateTime.UtcNow,
                Estado = EstadoRegistroProyecto.Aceptado
            };
            dbContext.RegistrosProyectos.Add(registro);
        }
        else if (registro.Estado == EstadoRegistroProyecto.Rechazado)
        {
            if (proyecto.Cupo.HasValue &&
                await dbContext.RegistrosProyectos.CountAsync(item =>
                    item.IdProyecto == id &&
                    item.Estado != EstadoRegistroProyecto.Rechazado) >= proyecto.Cupo.Value)
            {
                TempData["Mensaje"] = "El proyecto ya alcanzó su cupo máximo de colaboradores.";
                return RedirectToAction(nameof(Detalle), new { id });
            }

            registro.Estado = EstadoRegistroProyecto.Aceptado;
            registro.IdUsuarioRevisor = null;
            registro.FechaRevision = null;
            registro.FechaRegistro = DateTime.UtcNow;
        }
        else if (registro.Estado == EstadoRegistroProyecto.Pendiente)
        {
            registro.Estado = EstadoRegistroProyecto.Aceptado;
            registro.IdUsuarioRevisor = null;
            registro.FechaRevision = null;
            debeGuardarRegistro = true;
        }

        if (debeGuardarRegistro)
        {
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Mensaje"] = "Tu registro como colaborador quedó confirmado.";
        }
        else
        {
            TempData["Mensaje"] = "Ya estás registrado en este proyecto.";
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
        var proyecto = await dbContext.Proyectos
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.IdProyecto == id &&
                item.Activo &&
                item.EstadoAprobacion == EstadoAprobacion.Aprobado);
        if (proyecto is null)
        {
            return NotFound();
        }

        var interes = await dbContext.InteresesProyectos
            .FirstOrDefaultAsync(item => item.IdUsuario == usuarioId && item.IdProyecto == id);
        var marcado = interes is null;
        if (interes is null)
        {
            dbContext.InteresesProyectos.Add(new InteresProyecto
            {
                IdUsuario = usuarioId,
                IdProyecto = id,
                FechaInteres = DateTime.UtcNow
            });
        }
        else
        {
            dbContext.InteresesProyectos.Remove(interes);
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Mensaje"] = marcado
            ? "Marcaste que te interesa este proyecto. Esto no es una inscripción."
            : "Quitaste este proyecto de tus intereses.";

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

        await dbContext.RegistrosProyectos
            .Where(item =>
                item.IdUsuario == usuarioId &&
                (item.IdRegistroProyecto == id || item.IdProyecto == id))
            .ExecuteDeleteAsync();

        TempData["Mensaje"] = "Se canceló tu participación en el proyecto.";

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction(nameof(Index));
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

        var proyecto = await dbContext.Proyectos.FirstOrDefaultAsync(item => item.IdProyecto == id);
        if (proyecto is null)
        {
            return NotFound();
        }

        if (proyecto.IdUsuarioOrganizador != usuarioId)
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        if (!proyecto.Activo)
        {
            TempData["Mensaje"] = "El proyecto ya está inactivo.";
            return RedirectToAction("Index", "Solicitudes");
        }

        var motivoLimpio = motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivoLimpio) || motivoLimpio.Length > 1000)
        {
            TempData["Mensaje"] = "Indica un motivo de eliminación de hasta 1000 caracteres.";
            return RedirectToAction("MisEventos", "Eventos");
        }

        proyecto.Activo = false;
        proyecto.MotivoEliminacion = motivoLimpio;
        proyecto.FechaEliminacion = DateTime.UtcNow;
        proyecto.IdUsuarioEliminador = usuarioId;
        var fechaNotificacion = DateTime.UtcNow;
        if (proyecto.IdUsuarioCreador != usuarioId)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = proyecto.IdUsuarioCreador,
                IdProyecto = proyecto.IdProyecto,
                Titulo = $"El proyecto «{proyecto.Nombre}» fue eliminado",
                Mensaje = $"El organizador eliminó este proyecto. Motivo: {motivoLimpio}",
                FechaCreacion = fechaNotificacion
            });
        }

        var asistentesIds = await dbContext.RegistrosProyectos
            .AsNoTracking()
            .Where(registro =>
                registro.IdProyecto == proyecto.IdProyecto &&
                registro.Estado != EstadoRegistroProyecto.Rechazado)
            .Select(registro => registro.IdUsuario)
            .Distinct()
            .Where(asistenteId => asistenteId != proyecto.IdUsuarioCreador && asistenteId != usuarioId)
            .ToListAsync();

        foreach (var asistenteId in asistentesIds)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = asistenteId,
                IdProyecto = proyecto.IdProyecto,
                Titulo = $"El proyecto «{proyecto.Nombre}» fue dado de baja",
                Mensaje = $"El organizador dio de baja este proyecto. Motivo: {motivoLimpio}",
                FechaCreacion = fechaNotificacion
            });
        }

        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = "El proyecto fue eliminado de tu panel. Se notificó al creador y a sus asistentes.";
        return RedirectToAction("MisEventos", "Eventos");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Participacion(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var proyecto = await dbContext.Proyectos
            .AsNoTracking()
            .Include(p => p.Carrera)
            .Include(p => p.Creador)
            .Include(p => p.Organizador)
            .FirstOrDefaultAsync(p => p.IdProyecto == id);

        if (proyecto is null)
        {
            return NotFound();
        }

        if (!await PuedeEditarProyectoAsync(proyecto, usuarioId))
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var registros = await dbContext.RegistrosProyectos
            .AsNoTracking()
            .Where(r => r.IdProyecto == id && r.Estado != EstadoRegistroProyecto.Rechazado)
            .ToListAsync();

        var idsInscritos = registros.Select(r => r.IdUsuario).ToHashSet();
        var fechasAcreditacion = registros
            .Where(r => r.FechaRevision.HasValue)
            .ToDictionary(r => r.IdUsuario, r => r.FechaRevision);

        var idsInteresados = await dbContext.InteresesProyectos
            .AsNoTracking()
            .Where(i => i.IdProyecto == id)
            .Select(i => i.IdUsuario)
            .ToHashSetAsync();

        var idsParticipantes = idsInscritos.Union(idsInteresados).ToHashSet();

        var usuarios = await dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.Carrera)
            .Where(u => idsParticipantes.Contains(u.IdUsuario))
            .OrderBy(u => u.ApellidoPaterno)
            .ThenBy(u => u.Nombres)
            .ToListAsync();

        var usuariosDisponibles = await dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.Carrera)
            .Where(u =>
                u.Activo &&
                !idsParticipantes.Contains(u.IdUsuario) &&
                u.IdUsuario != proyecto.IdUsuarioCreador &&
                u.IdUsuario != proyecto.IdUsuarioOrganizador)
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

        ViewBag.UsuariosDisponibles = new SelectList(usuariosDisponibles, "IdUsuario", "Etiqueta");

        var modelo = new ListaParticipacionProyectoViewModel
        {
            Proyecto = proyecto,
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
                Asistio = fechasAcreditacion.ContainsKey(usuario.IdUsuario),
                FechaAcreditacion = fechasAcreditacion.GetValueOrDefault(usuario.IdUsuario)
            }).ToList()
        };

        return View("~/Views/Organizador/ControlParticipacionProyecto.cshtml", modelo);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarParticipacion(int id, List<int>? usuariosParticiparon)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var proyecto = await dbContext.Proyectos
            .FirstOrDefaultAsync(p => p.IdProyecto == id);
        if (proyecto is null)
        {
            return NotFound();
        }

        if (!await PuedeEditarProyectoAsync(proyecto, usuarioId))
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        if (DateTime.Today < proyecto.FechaInicio.Date)
        {
            TempData["Mensaje"] = $"El pase de participación estará disponible a partir del día de inicio del proyecto ({proyecto.FechaInicio:dd/MM/yyyy}).";
            return RedirectToAction(nameof(Participacion), new { id });
        }

        var registrosActuales = await dbContext.RegistrosProyectos
            .Where(r => r.IdProyecto == id)
            .ToListAsync();

        var idsInteresados = await dbContext.InteresesProyectos
            .Where(i => i.IdProyecto == id)
            .Select(i => i.IdUsuario)
            .ToListAsync();

        var idsParticipantesSet = registrosActuales
            .Where(r => r.Estado != EstadoRegistroProyecto.Rechazado)
            .Select(r => r.IdUsuario)
            .Union(idsInteresados)
            .ToHashSet();

        var idsQueParticiparon = (usuariosParticiparon ?? new List<int>()).ToHashSet();
        if (!idsQueParticiparon.IsSubsetOf(idsParticipantesSet))
        {
            return BadRequest();
        }

        var ahora = DateTime.UtcNow;
        var porUsuario = registrosActuales.ToDictionary(r => r.IdUsuario);

        foreach (var idParticipante in idsParticipantesSet)
        {
            var acreditado = idsQueParticiparon.Contains(idParticipante);
            if (porUsuario.TryGetValue(idParticipante, out var registroExistente))
            {
                if (acreditado)
                {
                    registroExistente.Estado = EstadoRegistroProyecto.Aceptado;
                    registroExistente.FechaRevision ??= ahora;
                    registroExistente.IdUsuarioRevisor = usuarioId;
                }
                else
                {
                    registroExistente.FechaRevision = null;
                    registroExistente.IdUsuarioRevisor = null;
                }
            }
            else if (acreditado)
            {
                dbContext.RegistrosProyectos.Add(new RegistroProyecto
                {
                    IdProyecto = id,
                    IdUsuario = idParticipante,
                    FechaRegistro = ahora,
                    Estado = EstadoRegistroProyecto.Aceptado,
                    FechaRevision = ahora,
                    IdUsuarioRevisor = usuarioId
                });
            }
        }

        await dbContext.SaveChangesAsync();
        TempData["Mensaje"] = $"Se guardó la participación acreditada de {idsQueParticiparon.Count} {(idsQueParticiparon.Count == 1 ? "persona" : "personas")}.";
        return RedirectToAction(nameof(Participacion), new { id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarParticipanteDirecto(int id, int? idUsuario, string? busquedaUsuario)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioActualId))
        {
            return Challenge();
        }

        var proyecto = await dbContext.Proyectos.FirstOrDefaultAsync(p => p.IdProyecto == id);
        if (proyecto is null)
        {
            return NotFound();
        }

        if (!await PuedeEditarProyectoAsync(proyecto, usuarioActualId))
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        if (DateTime.Today < proyecto.FechaInicio.Date)
        {
            TempData["Mensaje"] = $"El pase de participación estará disponible a partir del día de inicio del proyecto ({proyecto.FechaInicio:dd/MM/yyyy}).";
            return RedirectToAction(nameof(Participacion), new { id });
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
                    u.IdUsuario != proyecto.IdUsuarioCreador &&
                    u.IdUsuario != proyecto.IdUsuarioOrganizador)
                .ToListAsync();

            usuario = candidatos.FirstOrDefault(u =>
                string.Equals(u.Correo, termino, StringComparison.OrdinalIgnoreCase) ||
                termino.Contains(u.Correo, StringComparison.OrdinalIgnoreCase) ||
                $"{u.Nombres} {u.ApellidoPaterno} {u.ApellidoMaterno}".Trim().Contains(termino, StringComparison.OrdinalIgnoreCase));
        }

        if (usuario is null)
        {
            TempData["Mensaje"] = "No se encontró un usuario activo con ese nombre o correo. Selecciónalo de las sugerencias al escribir.";
            return RedirectToAction(nameof(Participacion), new { id });
        }

        var ahora = DateTime.UtcNow;
        var registro = await dbContext.RegistrosProyectos
            .FirstOrDefaultAsync(r => r.IdProyecto == id && r.IdUsuario == usuario.IdUsuario);

        if (registro is null)
        {
            dbContext.RegistrosProyectos.Add(new RegistroProyecto
            {
                IdProyecto = id,
                IdUsuario = usuario.IdUsuario,
                FechaRegistro = ahora,
                Estado = EstadoRegistroProyecto.Aceptado,
                FechaRevision = ahora,
                IdUsuarioRevisor = usuarioActualId
            });
        }
        else
        {
            registro.Estado = EstadoRegistroProyecto.Aceptado;
            registro.FechaRevision = ahora;
            registro.IdUsuarioRevisor = usuarioActualId;
        }

        await dbContext.SaveChangesAsync();
        TempData["Mensaje"] = $"Se agregó y acreditó la participación de {usuario.Nombres} {usuario.ApellidoPaterno}.";
        return RedirectToAction(nameof(Participacion), new { id });
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

        var usuario = await dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == usuarioId && u.Activo);

        var esAdmin = User.IsInRole("Administrador") ||
            User.IsInRole("Admin") ||
            (usuario is not null &&
             (usuario.IdRol == 3 || usuario.Rol.Nombre.Contains("Admin", StringComparison.OrdinalIgnoreCase)));

        var esOrganizador = esAdmin ||
            User.IsInRole("Organizador/Docente") ||
            User.IsInRole("Organizador") ||
            (usuario is not null &&
             (usuario.IdRol == 2 || usuario.Rol.Nombre.Contains("Organizador", StringComparison.OrdinalIgnoreCase)));

        if (!esOrganizador)
        {
            return Forbid();
        }

        var resultado = await ServicioOrdenPublicaciones.OrdenarProyectosUsuarioAsync(
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

    private async Task<bool> PuedeEditarProyectoAsync(Proyecto proyecto, int usuarioId)
    {
        if (proyecto.IdUsuarioCreador == usuarioId ||
            proyecto.IdUsuarioOrganizador == usuarioId ||
            User.IsInRole("Administrador") ||
            User.IsInRole("Admin"))
        {
            return true;
        }

        var usuario = await dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == usuarioId && u.Activo);

        if (usuario is null)
        {
            return false;
        }

        if (usuario.IdRol == 3 || usuario.Rol.Nombre.Contains("Admin"))
        {
            return true;
        }

        return false;
    }

    private void ValidarImagenProyecto(IFormFile? archivo, string nombreCampo)
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

    private async Task<string?> GuardarImagenProyectoAsync(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return null;
        }

        var carpetaDestino = Path.Combine(environment.WebRootPath, "uploads", "proyectos");
        Directory.CreateDirectory(carpetaDestino);

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
        var rutaCompleta = Path.Combine(carpetaDestino, nombreArchivo);

        await using var stream = new FileStream(rutaCompleta, FileMode.Create);
        await archivo.CopyToAsync(stream);

        return $"/uploads/proyectos/{nombreArchivo}";
    }

    private void EliminarImagenFisica(string? imagenUrl)
    {
        if (string.IsNullOrWhiteSpace(imagenUrl) || !imagenUrl.StartsWith("/uploads/proyectos/", StringComparison.OrdinalIgnoreCase))
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
                // Ignorar si el archivo está en uso temporalmente.
            }
        }
    }

    private async Task<List<Carrera>> ObtenerCarrerasCacheAsync()
    {
        return await memoryCache.GetOrCreateAsync("Catalogo_Carreras", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await dbContext.Carreras
                .AsNoTracking()
                .OrderBy(carrera => carrera.Nombre)
                .ToListAsync();
        }) ?? new List<Carrera>();
    }

    private async Task CargarCatalogosAsync()
    {
        var carreras = await ObtenerCarrerasCacheAsync();
        ViewBag.Carreras = new SelectList(
            carreras,
            nameof(Carrera.IdCarrera),
            nameof(Carrera.Nombre));

        var responsables = await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .Where(usuario =>
                usuario.Activo &&
                (usuario.IdRol == 2 || usuario.Rol.Nombre.Contains("Organizador")))
            .OrderBy(usuario => usuario.Nombres)
            .ThenBy(usuario => usuario.ApellidoPaterno)
            .Select(usuario => new
            {
                usuario.IdUsuario,
                NombreCompleto = (usuario.Nombres + " " + usuario.ApellidoPaterno +
                    (usuario.ApellidoMaterno != null ? " " + usuario.ApellidoMaterno : "")) +
                    " (" + usuario.Rol.Nombre + ")"
            })
            .ToListAsync();

        ViewBag.Responsables = new SelectList(
            responsables,
            "IdUsuario",
            "NombreCompleto");
    }
}
