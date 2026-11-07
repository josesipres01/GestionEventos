using System.Security.Claims;
using System.Data;
using GestionEventos.Data;
using GestionEventos.Models;
using GestionEventos.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionEventos.Controllers;

public class EventosController(
    GestionEventosDbContext dbContext,
    IWebHostEnvironment environment) : Controller
{
    private static readonly HashSet<string> ExtensionesImagenPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private const long TamanoMaximoImagenBytes = 5 * 1024 * 1024; // 5 MB

    public async Task<IActionResult> Index()
    {
        var eventos = await dbContext.Eventos
            .AsNoTrackingWithIdentityResolution()
            .Include(evento => evento.Organizador)
            .Include(evento => evento.Registros)
            .Include(evento => evento.Intereses)
            .Where(evento => evento.Activo && evento.EstadoAprobacion == EstadoAprobacion.Aprobado)
            .OrderBy(evento => evento.FechaHora)
            .ToListAsync();

        var proyectos = await dbContext.Proyectos
            .AsNoTrackingWithIdentityResolution()
            .Include(proyecto => proyecto.Carrera)
            .Include(proyecto => proyecto.Creador)
            .Include(proyecto => proyecto.Organizador)
            .Include(proyecto => proyecto.Registros)
            .Include(proyecto => proyecto.Intereses)
            .Where(proyecto => proyecto.Activo && proyecto.EstadoAprobacion == EstadoAprobacion.Aprobado)
            .OrderByDescending(proyecto => proyecto.FechaPublicacion)
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

        return View(eventos);
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
            .FirstOrDefaultAsync(item => item.IdEvento == id);

        if (evento is null)
        {
            return NotFound();
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

            await dbContext.Notificaciones
                .Where(n => n.IdUsuario == usuarioId && n.IdEvento == id)
                .ExecuteUpdateAsync(update => update.SetProperty(n => n.Leida, true));
        }

        ViewBag.EstaRegistrado = registroActual is not null;
        ViewBag.EstaInteresado = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioInteresadoId) &&
            evento.Intereses.Any(interes => interes.IdUsuario == usuarioInteresadoId);
        ViewBag.IdRegistroActual = registroActual?.IdRegistro;
        ViewBag.PuedeEditar = puedeEditar;

        return View(evento);
    }

    [Authorize]
    public async Task<IActionResult> MisRegistros()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var registros = await dbContext.Registros
            .AsNoTrackingWithIdentityResolution()
            .Where(registro => registro.IdUsuario == usuarioId)
            .Include(registro => registro.Evento)
                .ThenInclude(evento => evento.Organizador)
            .OrderByDescending(registro => registro.FechaRegistro)
            .ToListAsync();

        var registrosProyectos = await dbContext.RegistrosProyectos
            .AsNoTrackingWithIdentityResolution()
            .Where(registro =>
                registro.IdUsuario == usuarioId &&
                registro.Estado != EstadoRegistroProyecto.Rechazado)
            .Include(registro => registro.Proyecto)
                .ThenInclude(proyecto => proyecto.Carrera)
            .Include(registro => registro.Proyecto)
                .ThenInclude(proyecto => proyecto.Creador)
            .Include(registro => registro.Proyecto)
                .ThenInclude(proyecto => proyecto.Organizador)
            .OrderByDescending(registro => registro.FechaRegistro)
            .ToListAsync();

        var notificaciones = await dbContext.Notificaciones
            .AsNoTracking()
            .Where(n => n.IdUsuario == usuarioId)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(20)
            .ToListAsync();

        ViewBag.RegistrosProyectos = registrosProyectos;
        ViewBag.Notificaciones = notificaciones;

        return View(registros);
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
        var consultaEventos = dbContext.Eventos
            .AsNoTrackingWithIdentityResolution()
            .Include(e => e.Organizador)
            .Include(e => e.Registros)
            .Where(e => e.Activo)
            .AsQueryable();

        if (!esAdmin)
        {
            consultaEventos = consultaEventos.Where(e => e.IdUsuarioOrganizador == usuarioId);
        }

        var misEventos = await consultaEventos
            .OrderByDescending(e => e.FechaHora)
            .ToListAsync();

        var proyectosResponsable = await dbContext.Proyectos
            .AsNoTrackingWithIdentityResolution()
            .Include(p => p.Carrera)
            .Include(p => p.Creador)
            .Include(p => p.Organizador)
            .Include(p => p.Registros)
            .Where(p => p.Activo)
            .Where(p =>
                esAdmin ||
                p.IdUsuarioOrganizador == usuarioId ||
                p.IdUsuarioCreador == usuarioId)
            .OrderByDescending(p => p.FechaPublicacion)
            .ToListAsync();

        ViewBag.ProyectosACargo = proyectosResponsable;
        ViewBag.EsAdministrador = esAdmin;
        return View(misEventos);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Inscritos(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (!await EsOrganizadorOAdminAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var evento = await dbContext.Eventos
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(e => e.Organizador)
            .Include(e => e.Registros)
                .ThenInclude(r => r.Usuario)
                    .ThenInclude(u => u.Carrera)
            .FirstOrDefaultAsync(e => e.IdEvento == id);

        if (evento is null)
        {
            return NotFound();
        }

        return View(evento);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        await CargarOrganizadoresAsync();
        return View(new CrearEventoViewModel());
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

        var organizador = await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .FirstOrDefaultAsync(usuario =>
                usuario.IdUsuario == modelo.IdUsuarioOrganizador &&
                usuario.Activo &&
                (usuario.IdRol == 2 || usuario.Rol.Nombre.Contains("Organizador")));
        if (organizador is null)
        {
            ModelState.AddModelError(nameof(modelo.IdUsuarioOrganizador), "Selecciona un organizador activo.");
        }
        else if (organizador.IdUsuario == usuarioId)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioOrganizador),
                "Selecciona a otro organizador para que revise tu solicitud.");
        }

        ValidarImagenEvento(modelo.ImagenArchivo, nameof(modelo.ImagenArchivo));

        if (!ModelState.IsValid)
        {
            await CargarOrganizadoresAsync();
            return View(modelo);
        }

        var imagenUrl = await GuardarImagenEventoAsync(modelo.ImagenArchivo);

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
            EstadoAprobacion = EstadoAprobacion.Pendiente,
            FechaSolicitud = DateTime.UtcNow,
            FechaCreacion = DateTime.UtcNow
        };

        dbContext.Eventos.Add(nuevoEvento);
        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = "Tu solicitud de evento se envió al organizador para su aprobación.";
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

        return View(new EditarEventoViewModel
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
            return View(modelo);
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
            return View(modelo);
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

    private async Task CargarOrganizadoresAsync()
    {
        var organizadores = await dbContext.Usuarios
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
                NombreCompleto = usuario.Nombres + " " + usuario.ApellidoPaterno +
                    (usuario.ApellidoMaterno != null ? " " + usuario.ApellidoMaterno : "") +
                    " (" + usuario.Rol.Nombre + ")"
            })
            .ToListAsync();

        ViewBag.Organizadores = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            organizadores,
            "IdUsuario",
            "NombreCompleto");
    }
}
