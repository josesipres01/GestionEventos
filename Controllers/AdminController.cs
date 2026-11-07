using System.Security.Claims;
using GestionEventos.Data;
using GestionEventos.Models;
using GestionEventos.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionEventos.Controllers;

[Authorize]
public class AdminController(
    GestionEventosDbContext dbContext,
    IPasswordHasher<Usuario> passwordHasher,
    IMemoryCache memoryCache) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var usuarios = await dbContext.Usuarios
            .AsNoTrackingWithIdentityResolution()
            .Include(u => u.Rol)
            .Include(u => u.Carrera)
            .OrderByDescending(u => u.IdRol)
            .ThenBy(u => u.Nombres)
            .ToListAsync();

        var roles = await dbContext.Roles
            .AsNoTracking()
            .OrderBy(r => r.IdRol)
            .ToListAsync();

        var carreras = await dbContext.Carreras
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        var totalEventos = await dbContext.Eventos.AsNoTracking().CountAsync(evento => evento.Activo);
        var totalProyectos = await dbContext.Proyectos.AsNoTracking().CountAsync(proyecto => proyecto.Activo);
        var totalRegistros = await dbContext.Registros.AsNoTracking().CountAsync();

        var modelo = new AdminDashboardViewModel
        {
            TotalUsuarios = usuarios.Count,
            TotalAdministradores = usuarios.Count(u => u.IdRol == 3 || u.Rol.Nombre.Contains("Admin")),
            TotalOrganizadores = usuarios.Count(u => u.IdRol == 2 || u.Rol.Nombre.Contains("Organizador")),
            TotalEstudiantes = usuarios.Count(u => u.IdRol == 1 || u.Rol.Nombre.Contains("Estudiante")),
            TotalEventos = totalEventos,
            TotalProyectos = totalProyectos,
            TotalRegistrosEventos = totalRegistros,
            TotalCarreras = carreras.Count,
            Usuarios = usuarios,
            Roles = roles,
            Carreras = carreras
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> RegistrarUsuario()
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        await CargarCatalogosAsync();
        return View(new RegistrarOrganizadorViewModel { IdRol = 2 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarUsuario(RegistrarOrganizadorViewModel modelo)
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var correoNormalizado = modelo.Correo?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(correoNormalizado) &&
            await dbContext.Usuarios.AsNoTracking().AnyAsync(u => u.Correo == correoNormalizado))
        {
            ModelState.AddModelError(nameof(modelo.Correo), "Ya existe una cuenta registrada con ese correo electrónico.");
        }

        var rol = await dbContext.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.IdRol == modelo.IdRol);
        if (rol is null)
        {
            ModelState.AddModelError(nameof(modelo.IdRol), "Selecciona un rol válido.");
        }

        if (modelo.IdCarrera.HasValue &&
            !await dbContext.Carreras.AsNoTracking().AnyAsync(c => c.IdCarrera == modelo.IdCarrera.Value))
        {
            ModelState.AddModelError(nameof(modelo.IdCarrera), "Selecciona una carrera válida.");
        }

        if (!ModelState.IsValid)
        {
            await CargarCatalogosAsync();
            return View(modelo);
        }

        var nuevoUsuario = new Usuario
        {
            Nombres = modelo.Nombres.Trim(),
            ApellidoPaterno = modelo.ApellidoPaterno.Trim(),
            ApellidoMaterno = modelo.ApellidoMaterno?.Trim(),
            Correo = correoNormalizado,
            IdRol = rol!.IdRol,
            IdCarrera = modelo.IdCarrera,
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };
        nuevoUsuario.PasswordHash = passwordHasher.HashPassword(nuevoUsuario, modelo.Contrasena);

        dbContext.Usuarios.Add(nuevoUsuario);
        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = $"Se registró exitosamente a {nuevoUsuario.Nombres} {nuevoUsuario.ApellidoPaterno} con el rol «{rol.Nombre}».";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarRol(int idUsuario, int idRol)
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var usuario = await dbContext.Usuarios.FindAsync(idUsuario);
        var rol = await dbContext.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.IdRol == idRol);
        if (usuario is null || rol is null)
        {
            return NotFound();
        }

        usuario.IdRol = idRol;
        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = $"El rol de {usuario.Nombres} {usuario.ApellidoPaterno} se actualizó a «{rol.Nombre}».";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoUsuario(int idUsuario)
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminActualId) &&
            adminActualId == idUsuario)
        {
            TempData["Mensaje"] = "No puedes desactivar tu propia cuenta de administrador.";
            return RedirectToAction(nameof(Index));
        }

        var usuario = await dbContext.Usuarios.FindAsync(idUsuario);
        if (usuario is null)
        {
            return NotFound();
        }

        usuario.Activo = !usuario.Activo;
        await dbContext.SaveChangesAsync();

        var estadoTexto = usuario.Activo ? "activada" : "desactivada";
        TempData["Mensaje"] = $"La cuenta de {usuario.Nombres} {usuario.ApellidoPaterno} fue {estadoTexto}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Carreras()
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var carreras = await dbContext.Carreras
            .AsNoTrackingWithIdentityResolution()
            .Include(c => c.Usuarios)
            .Include(c => c.Proyectos)
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        return View(carreras);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCarrera(string nombre)
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var nombreLimpio = nombre?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombreLimpio))
        {
            TempData["Mensaje"] = "Ingresa un nombre válido para la carrera o departamento.";
            return RedirectToAction(nameof(Carreras));
        }

        if (await dbContext.Carreras.AsNoTracking().AnyAsync(c => c.Nombre == nombreLimpio))
        {
            TempData["Mensaje"] = "Ya existe una carrera registrada con ese nombre.";
            return RedirectToAction(nameof(Carreras));
        }

        dbContext.Carreras.Add(new Carrera { Nombre = nombreLimpio });
        await dbContext.SaveChangesAsync();
        memoryCache.Remove("Catalogo_Carreras");

        TempData["Mensaje"] = $"Se agregó la carrera «{nombreLimpio}» al catálogo.";
        return RedirectToAction(nameof(Carreras));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarCarrera(int id)
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var tieneDependencias = await dbContext.Proyectos.AsNoTracking().AnyAsync(p => p.IdCarrera == id)
            || await dbContext.Usuarios.AsNoTracking().AnyAsync(u => u.IdCarrera == id);

        if (tieneDependencias)
        {
            TempData["Mensaje"] = "No se puede eliminar esta carrera porque tiene usuarios o proyectos asociados.";
            return RedirectToAction(nameof(Carreras));
        }

        await dbContext.Carreras.Where(c => c.IdCarrera == id).ExecuteDeleteAsync();
        memoryCache.Remove("Catalogo_Carreras");

        TempData["Mensaje"] = "La carrera fue eliminada del catálogo.";
        return RedirectToAction(nameof(Carreras));
    }

    [HttpGet]
    public async Task<IActionResult> Publicaciones()
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var eventos = await dbContext.Eventos
            .AsNoTrackingWithIdentityResolution()
            .Include(e => e.Organizador)
            .Include(e => e.Registros)
            .OrderByDescending(e => e.FechaHora)
            .ToListAsync();

        var proyectos = await dbContext.Proyectos
            .AsNoTrackingWithIdentityResolution()
            .Include(p => p.Carrera)
            .Include(p => p.Creador)
            .Include(p => p.Organizador)
            .Include(p => p.Registros)
            .OrderByDescending(p => p.FechaPublicacion)
            .ToListAsync();

        ViewBag.Proyectos = proyectos;
        return View(eventos);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarEvento(int id, string? motivo)
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var evento = await dbContext.Eventos.FirstOrDefaultAsync(e => e.IdEvento == id);
        if (evento is null)
        {
            return NotFound();
        }

        if (!evento.Activo)
        {
            TempData["Mensaje"] = "El evento ya está inactivo.";
            return RedirectToAction(nameof(Publicaciones));
        }

        var motivoLimpio = motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivoLimpio) || motivoLimpio.Length > 1000)
        {
            TempData["Mensaje"] = "Indica un motivo de eliminación de hasta 1000 caracteres.";
            return RedirectToAction(nameof(Publicaciones));
        }

        evento.Activo = false;
        evento.MotivoEliminacion = motivoLimpio;
        evento.FechaEliminacion = DateTime.UtcNow;
        var tieneAdminId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId);
        if (tieneAdminId)
        {
            evento.IdUsuarioEliminador = adminId;
        }
        if (!tieneAdminId || evento.IdUsuarioSolicitante != adminId)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = evento.IdUsuarioSolicitante,
                IdEvento = evento.IdEvento,
                Titulo = $"El evento «{evento.Nombre}» fue eliminado",
                Mensaje = $"El administrador eliminó este evento. Motivo: {motivoLimpio}",
                FechaCreacion = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = "El evento fue eliminado de la vista pública y se notificó a su creador.";
        return RedirectToAction(nameof(Publicaciones));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarProyecto(int id, string? motivo)
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var proyecto = await dbContext.Proyectos.FirstOrDefaultAsync(p => p.IdProyecto == id);
        if (proyecto is null)
        {
            return NotFound();
        }

        if (!proyecto.Activo)
        {
            TempData["Mensaje"] = "El proyecto ya está inactivo.";
            return RedirectToAction(nameof(Publicaciones));
        }

        var motivoLimpio = motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivoLimpio) || motivoLimpio.Length > 1000)
        {
            TempData["Mensaje"] = "Indica un motivo de eliminación de hasta 1000 caracteres.";
            return RedirectToAction(nameof(Publicaciones));
        }

        proyecto.Activo = false;
        proyecto.MotivoEliminacion = motivoLimpio;
        proyecto.FechaEliminacion = DateTime.UtcNow;
        var fechaNotificacion = DateTime.UtcNow;
        var tieneAdminId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId);
        if (tieneAdminId)
        {
            proyecto.IdUsuarioEliminador = adminId;
        }
        if (!tieneAdminId || proyecto.IdUsuarioCreador != adminId)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = proyecto.IdUsuarioCreador,
                IdProyecto = proyecto.IdProyecto,
                Titulo = $"El proyecto «{proyecto.Nombre}» fue eliminado",
                Mensaje = $"El administrador eliminó este proyecto. Motivo: {motivoLimpio}",
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
            .Where(asistenteId => asistenteId != proyecto.IdUsuarioCreador &&
                (!tieneAdminId || asistenteId != adminId))
            .ToListAsync();

        foreach (var asistenteId in asistentesIds)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = asistenteId,
                IdProyecto = proyecto.IdProyecto,
                Titulo = $"El proyecto «{proyecto.Nombre}» fue dado de baja",
                Mensaje = $"El administrador dio de baja este proyecto. Motivo: {motivoLimpio}",
                FechaCreacion = fechaNotificacion
            });
        }

        await dbContext.SaveChangesAsync();

        TempData["Mensaje"] = "El proyecto fue eliminado de la vista pública y se notificó a su creador y a sus asistentes.";
        return RedirectToAction(nameof(Publicaciones));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolverReactivacion(string tipo, int id, bool aprobar)
    {
        if (!await EsAdministradorAsync())
        {
            return RedirectToAction("AccesoDenegado", "Cuenta");
        }

        var tipoNormalizado = tipo?.Trim().ToLowerInvariant();
        if (tipoNormalizado == "evento")
        {
            var evento = await dbContext.Eventos.FirstOrDefaultAsync(item => item.IdEvento == id);
            if (evento is null)
            {
                return NotFound();
            }

            if (!evento.ReactivacionSolicitada || evento.Activo)
            {
                TempData["Mensaje"] = "El evento no tiene una solicitud de reactivación pendiente.";
                return RedirectToAction(nameof(Publicaciones));
            }

            evento.ReactivacionSolicitada = false;
            evento.FechaSolicitudReactivacion = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var revisorEventoId))
            {
                evento.IdUsuarioRevisor = revisorEventoId;
            }
            if (aprobar)
            {
                evento.Activo = true;
            }

            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = evento.IdUsuarioSolicitante,
                IdEvento = evento.IdEvento,
                Titulo = aprobar ? "Evento reactivado" : "Solicitud de reactivación rechazada",
                Mensaje = aprobar
                    ? $"El administrador reactivó «{evento.Nombre}». Ya está visible para la comunidad."
                    : $"El administrador rechazó la solicitud para reactivar «{evento.Nombre}».",
                FechaCreacion = DateTime.UtcNow
            });
        }
        else if (tipoNormalizado == "proyecto")
        {
            var proyecto = await dbContext.Proyectos.FirstOrDefaultAsync(item => item.IdProyecto == id);
            if (proyecto is null)
            {
                return NotFound();
            }

            if (!proyecto.ReactivacionSolicitada || proyecto.Activo)
            {
                TempData["Mensaje"] = "El proyecto no tiene una solicitud de reactivación pendiente.";
                return RedirectToAction(nameof(Publicaciones));
            }

            proyecto.ReactivacionSolicitada = false;
            proyecto.FechaSolicitudReactivacion = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var revisorProyectoId))
            {
                proyecto.IdUsuarioRevisor = revisorProyectoId;
            }
            if (aprobar)
            {
                proyecto.Activo = true;
            }

            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = proyecto.IdUsuarioCreador,
                IdProyecto = proyecto.IdProyecto,
                Titulo = aprobar ? "Proyecto reactivado" : "Solicitud de reactivación rechazada",
                Mensaje = aprobar
                    ? $"El administrador reactivó «{proyecto.Nombre}». Ya está visible para la comunidad."
                    : $"El administrador rechazó la solicitud para reactivar «{proyecto.Nombre}».",
                FechaCreacion = DateTime.UtcNow
            });
        }
        else
        {
            return BadRequest();
        }

        await dbContext.SaveChangesAsync();
        TempData["Mensaje"] = aprobar
            ? "La publicación fue reactivada y volvió a estar disponible."
            : "La solicitud de reactivación fue rechazada.";
        return RedirectToAction(nameof(Publicaciones));
    }

    private async Task CargarCatalogosAsync()
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .OrderBy(r => r.IdRol)
            .ToListAsync();

        var carreras = await dbContext.Carreras
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        ViewBag.Roles = new SelectList(roles, nameof(Rol.IdRol), nameof(Rol.Nombre));
        ViewBag.Carreras = new SelectList(carreras, nameof(Carrera.IdCarrera), nameof(Carrera.Nombre));
    }

    private async Task<bool> EsAdministradorAsync()
    {
        if (User.IsInRole("Administrador") || User.IsInRole("Admin"))
        {
            return true;
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return false;
        }

        return await dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .AnyAsync(u =>
                u.IdUsuario == usuarioId &&
                u.Activo &&
                (u.IdRol == 3 || u.Rol.Nombre.Contains("Admin")));
    }
}
