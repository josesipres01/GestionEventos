using System.Security.Claims;
using System.Data;
using GestionEventos.Data;
using GestionEventos.Models;
using GestionEventos.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionEventos.Controllers;

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

    public async Task<IActionResult> Index()
    {
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

        var registradosIds = new HashSet<int>();
        var interesadosIds = new HashSet<int>();
        var usuarioActualId = 0;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            usuarioActualId = usuarioId;
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
        return View(proyectos);
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
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            registroActual = proyecto.Registros.FirstOrDefault(r => r.IdUsuario == usuarioId);
            puedeEditar = await PuedeEditarProyectoAsync(proyecto, usuarioId);

            await dbContext.Notificaciones
                .Where(n => n.IdUsuario == usuarioId && n.IdProyecto == id)
                .ExecuteUpdateAsync(update => update.SetProperty(n => n.Leida, true));
        }

        ViewBag.EstaRegistrado = registroActual is not null &&
            registroActual.Estado != EstadoRegistroProyecto.Rechazado;
        ViewBag.EstaInteresado = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioInteresadoId) &&
            proyecto.Intereses.Any(interes => interes.IdUsuario == usuarioInteresadoId);
        ViewBag.PuedeEditar = puedeEditar;

        return View(proyecto);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        await CargarCatalogosAsync();
        return View(new CrearProyectoViewModel());
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
            return View(modelo);
        }

        var imagenUrl = await GuardarImagenProyectoAsync(modelo.ImagenArchivo);

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
            IdCarrera = modelo.IdCarrera,
            FechaPublicacion = DateTime.UtcNow,
            FechaSolicitud = DateTime.UtcNow,
            EstadoAprobacion = EstadoAprobacion.Pendiente
        };

        dbContext.Proyectos.Add(nuevoProyecto);
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

        return View(new EditarProyectoViewModel
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
            return View(modelo);
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
            return View(modelo);
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

        if (!await PuedeEditarProyectoAsync(proyecto, usuarioId))
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
        return RedirectToAction("Index", "Solicitudes");
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
