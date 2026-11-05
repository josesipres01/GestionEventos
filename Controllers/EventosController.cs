using System.Security.Claims;
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
            .Where(evento => evento.Activo && evento.EstadoAprobacion == EstadoAprobacion.Aprobado)
            .OrderBy(evento => evento.FechaHora)
            .ToListAsync();

        var proyectos = await dbContext.Proyectos
            .AsNoTrackingWithIdentityResolution()
            .Include(proyecto => proyecto.Carrera)
            .Include(proyecto => proyecto.Usuario)
            .Include(proyecto => proyecto.Registros)
            .Where(proyecto => proyecto.Activo && proyecto.EstadoAprobacion == EstadoAprobacion.Aprobado)
            .OrderByDescending(proyecto => proyecto.FechaPublicacion)
            .ToListAsync();

        var eventosRegistradosIds = new HashSet<int>();
        var proyectosRegistradosIds = new HashSet<int>();
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
                .Where(p => p.Registros.Any(r => r.IdUsuario == usuarioId))
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
                .ExecuteDeleteAsync();
        }

        ViewBag.EstaRegistrado = registroActual is not null;
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
            .Where(registro => registro.IdUsuario == usuarioId)
            .Include(registro => registro.Proyecto)
                .ThenInclude(proyecto => proyecto.Carrera)
            .OrderByDescending(registro => registro.FechaRegistro)
            .ToListAsync();

        var notificaciones = await dbContext.Notificaciones
            .AsNoTracking()
            .Where(n => n.IdUsuario == usuarioId && !n.Leida)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(20)
            .ToListAsync();

        await dbContext.Notificaciones
            .Where(n => n.IdUsuario == usuarioId)
            .ExecuteDeleteAsync();

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
            .ExecuteDeleteAsync();

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
            .Where(n => n.IdUsuario == usuarioId)
            .ExecuteDeleteAsync();

        TempData["Mensaje"] = "Se eliminaron los avisos de cambios.";

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
        var nombreCompleto = string.Join(
            " ",
            new[] { usuarioActual.Nombres, usuarioActual.ApellidoPaterno, usuarioActual.ApellidoMaterno }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

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
            .Include(p => p.Usuario)
            .Include(p => p.Registros)
            .Where(p => p.Activo)
            .Where(p =>
                esAdmin ||
                p.IdUsuarioResponsable == usuarioId ||
                p.IdUsuario == usuarioId ||
                (p.IdUsuarioResponsable == null && p.NombreResponsable == nombreCompleto))
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

        // Detectar cambios para informar a los usuarios registrados
        var nuevoNombre = modelo.Nombre.Trim();
        var nuevaDescripcion = modelo.Descripcion.Trim();
        var nuevoLugar = modelo.Lugar.Trim();

        if (modelo.ImagenArchivo is { Length: > 0 })
        {
            EliminarImagenFisica(evento.ImagenUrl);
            evento.ImagenUrl = await GuardarImagenEventoAsync(modelo.ImagenArchivo);
        }

        evento.Nombre = nuevoNombre;
        evento.Descripcion = nuevaDescripcion;
        evento.FechaHora = modelo.FechaHora;
        evento.Lugar = nuevoLugar;
        evento.EstadoAprobacion = EstadoAprobacion.Pendiente;
        evento.ComentarioAprobacion = null;
        evento.FechaRevision = null;
        evento.FechaSolicitud = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = "Los cambios se enviaron al organizador para su aprobación.";

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

        if (!await dbContext.Eventos.AsNoTracking().AnyAsync(evento =>
                evento.IdEvento == id && evento.Activo && evento.EstadoAprobacion == EstadoAprobacion.Aprobado))
        {
            return NotFound();
        }

        var yaRegistrado = await dbContext.Registros
            .AsNoTracking()
            .AnyAsync(registro => registro.IdUsuario == usuarioId && registro.IdEvento == id);
        if (!yaRegistrado)
        {
            dbContext.Registros.Add(new Registro
            {
                IdUsuario = usuarioId,
                IdEvento = id,
                FechaRegistro = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
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
