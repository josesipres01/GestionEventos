using System.Security.Claims;
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
            .Include(proyecto => proyecto.Usuario)
            .Include(proyecto => proyecto.Registros)
            .Where(proyecto => proyecto.Activo && proyecto.EstadoAprobacion == EstadoAprobacion.Aprobado)
            .OrderByDescending(proyecto => proyecto.FechaPublicacion)
            .ToListAsync();

        var registradosIds = new HashSet<int>();
        var usuarioActualId = 0;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            usuarioActualId = usuarioId;
            registradosIds = proyectos
                .Where(p => p.Registros.Any(r => r.IdUsuario == usuarioId))
                .Select(p => p.IdProyecto)
                .ToHashSet();
        }

        ViewBag.ProyectosRegistradosIds = registradosIds;
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
            .Include(item => item.Usuario)
                .ThenInclude(usuario => usuario.Carrera)
            .Include(item => item.Registros)
                .ThenInclude(registro => registro.Usuario)
                    .ThenInclude(usuario => usuario.Carrera)
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
             (proyecto.IdUsuario != lectorId &&
              proyecto.IdUsuarioResponsable != lectorId &&
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
                .ExecuteDeleteAsync();
        }

        ViewBag.EstaRegistrado = registroActual is not null;
        ViewBag.IdRegistroProyectoActual = registroActual?.IdRegistroProyecto;
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
                usuario.IdUsuario == modelo.IdUsuarioResponsable &&
                usuario.Activo &&
                (usuario.IdRol == 2 || usuario.Rol.Nombre.Contains("Organizador")));

        if (responsable is null)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioResponsable),
                "Selecciona un usuario válido con rol Organizador.");
        }
        else if (responsable.IdUsuario == usuarioId)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioResponsable),
                "Selecciona a otro organizador para que revise tu solicitud.");
        }

        ValidarImagenProyecto(modelo.ImagenArchivo, nameof(modelo.ImagenArchivo));

        if (!ModelState.IsValid)
        {
            await CargarCatalogosAsync();
            return View(modelo);
        }

        var nombreResponsable = string.Join(
            " ",
            new[] { responsable!.Nombres, responsable.ApellidoPaterno, responsable.ApellidoMaterno }
                .Where(parte => !string.IsNullOrWhiteSpace(parte)));

        var imagenUrl = await GuardarImagenProyectoAsync(modelo.ImagenArchivo);

        var nuevoProyecto = new Proyecto
        {
            IdUsuario = usuarioId,
            IdUsuarioResponsable = responsable!.IdUsuario,
            Nombre = modelo.Nombre.Trim(),
            Descripcion = modelo.Descripcion.Trim(),
            NombreResponsable = nombreResponsable,
            LugarReunion = modelo.LugarReunion.Trim(),
            FechaInicio = modelo.FechaInicio.Date,
            FechaFin = modelo.FechaFin.Date,
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

        var responsableActual = responsables.FirstOrDefault(u => u.IdUsuario == proyecto.IdUsuarioResponsable)
            ?? responsables.FirstOrDefault(u =>
                string.Join(" ", new[] { u.Nombres, u.ApellidoPaterno, u.ApellidoMaterno }.Where(s => !string.IsNullOrWhiteSpace(s))) == proyecto.NombreResponsable);

        await CargarCatalogosAsync();

        return View(new EditarProyectoViewModel
        {
            IdProyecto = proyecto.IdProyecto,
            Nombre = proyecto.Nombre,
            Descripcion = proyecto.Descripcion,
            IdUsuarioResponsable = responsableActual?.IdUsuario ?? 0,
            IdCarrera = proyecto.IdCarrera,
            LugarReunion = proyecto.LugarReunion,
            FechaInicio = proyecto.FechaInicio.Date,
            FechaFin = proyecto.FechaFin.Date,
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
                usuario.IdUsuario == modelo.IdUsuarioResponsable &&
                usuario.Activo &&
                (usuario.IdRol == 2 || usuario.Rol.Nombre.Contains("Organizador")));

        if (responsable is null)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioResponsable),
                "Selecciona un usuario válido con rol Organizador.");
        }
        else if (responsable.IdUsuario == proyecto.IdUsuario)
        {
            ModelState.AddModelError(
                nameof(modelo.IdUsuarioResponsable),
                "El organizador responsable debe ser distinto de quien envió el proyecto.");
        }

        ValidarImagenProyecto(modelo.ImagenArchivo, nameof(modelo.ImagenArchivo));

        if (!ModelState.IsValid)
        {
            modelo.ImagenUrlActual = proyecto.ImagenUrl;
            await CargarCatalogosAsync();
            return View(modelo);
        }

        var nombreResponsable = string.Join(
            " ",
            new[] { responsable!.Nombres, responsable.ApellidoPaterno, responsable.ApellidoMaterno }
                .Where(parte => !string.IsNullOrWhiteSpace(parte)));

        var nuevoNombre = modelo.Nombre.Trim();
        var nuevaDescripcion = modelo.Descripcion.Trim();
        var nuevoLugar = modelo.LugarReunion.Trim();

        if (modelo.ImagenArchivo is { Length: > 0 })
        {
            EliminarImagenFisica(proyecto.ImagenUrl);
            proyecto.ImagenUrl = await GuardarImagenProyectoAsync(modelo.ImagenArchivo);
        }

        proyecto.Nombre = nuevoNombre;
        proyecto.Descripcion = nuevaDescripcion;
        proyecto.NombreResponsable = nombreResponsable;
        proyecto.IdUsuarioResponsable = responsable!.IdUsuario;
        proyecto.LugarReunion = nuevoLugar;
        proyecto.FechaInicio = modelo.FechaInicio.Date;
        proyecto.FechaFin = modelo.FechaFin.Date;
        proyecto.IdCarrera = modelo.IdCarrera;
        proyecto.EstadoAprobacion = EstadoAprobacion.Pendiente;
        proyecto.ComentarioAprobacion = null;
        proyecto.FechaRevision = null;
        proyecto.FechaSolicitud = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = "Los cambios se enviaron al organizador para su aprobación.";

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

        if (!await dbContext.Proyectos.AsNoTracking().AnyAsync(proyecto =>
                proyecto.IdProyecto == id && proyecto.Activo && proyecto.EstadoAprobacion == EstadoAprobacion.Aprobado))
        {
            return NotFound();
        }

        var yaRegistrado = await dbContext.RegistrosProyectos
            .AsNoTracking()
            .AnyAsync(registro => registro.IdUsuario == usuarioId && registro.IdProyecto == id);
        if (!yaRegistrado)
        {
            dbContext.RegistrosProyectos.Add(new RegistroProyecto
            {
                IdUsuario = usuarioId,
                IdProyecto = id,
                FechaRegistro = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
            TempData["Mensaje"] = "Te has sumado exitosamente a este proyecto académico.";
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
        var fechaNotificacion = DateTime.UtcNow;
        if (proyecto.IdUsuario != usuarioId)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = proyecto.IdUsuario,
                IdProyecto = proyecto.IdProyecto,
                Titulo = $"El proyecto «{proyecto.Nombre}» fue eliminado",
                Mensaje = $"El organizador eliminó este proyecto. Motivo: {motivoLimpio}",
                FechaCreacion = fechaNotificacion
            });
        }

        var asistentesIds = await dbContext.RegistrosProyectos
            .AsNoTracking()
            .Where(registro => registro.IdProyecto == proyecto.IdProyecto)
            .Select(registro => registro.IdUsuario)
            .Distinct()
            .Where(asistenteId => asistenteId != proyecto.IdUsuario && asistenteId != usuarioId)
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
        if (proyecto.IdUsuario == usuarioId ||
            proyecto.IdUsuarioResponsable == usuarioId ||
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

        var nombreCompleto = string.Join(
            " ",
            new[] { usuario.Nombres, usuario.ApellidoPaterno, usuario.ApellidoMaterno }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        return string.Equals(proyecto.NombreResponsable, nombreCompleto, StringComparison.OrdinalIgnoreCase);
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
