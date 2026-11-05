using System.Security.Claims;
using GestionEventos.Data;
using GestionEventos.Models;
using GestionEventos.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionEventos.Controllers;

[Authorize]
public class SolicitudesController(GestionEventosDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var eventos = await dbContext.Eventos
            .AsNoTracking()
            .Include(evento => evento.Organizador)
            .Include(evento => evento.Solicitante)
            .Where(evento =>
                evento.IdUsuarioSolicitante == usuarioId ||
                evento.IdUsuarioOrganizador == usuarioId)
            .ToListAsync();

        var proyectos = await dbContext.Proyectos
            .AsNoTracking()
            .Include(proyecto => proyecto.OrganizadorResponsable)
            .Include(proyecto => proyecto.Usuario)
            .Where(proyecto =>
                proyecto.IdUsuario == usuarioId ||
                proyecto.IdUsuarioResponsable == usuarioId)
            .ToListAsync();

        var puedeRevisarSolicitudes = await EsOrganizadorActivoAsync(usuarioId);
        var modelo = new SolicitudesViewModel
        {
            PuedeRevisarSolicitudes = puedeRevisarSolicitudes,
            MisSolicitudes = eventos
                .Where(evento => evento.IdUsuarioSolicitante == usuarioId && evento.Activo)
                .Select(evento => new SolicitudPublicacionViewModel
                {
                    Id = evento.IdEvento,
                    Tipo = "Evento",
                    Nombre = evento.Nombre,
                    Organizador = NombreCompleto(evento.Organizador),
                    Estado = evento.EstadoAprobacion,
                    FechaEnvio = evento.FechaSolicitud,
                    FechaRevision = evento.FechaRevision,
                    Comentario = evento.ComentarioAprobacion,
                    UrlDetalle = Url.Action("Detalle", "Eventos", new { id = evento.IdEvento }),
                    UrlEdicion = Url.Action("Editar", "Eventos", new { id = evento.IdEvento })
                })
                .Concat(proyectos
                    .Where(proyecto => proyecto.IdUsuario == usuarioId && proyecto.Activo)
                    .Select(proyecto => new SolicitudPublicacionViewModel
                    {
                        Id = proyecto.IdProyecto,
                        Tipo = "Proyecto",
                        Nombre = proyecto.Nombre,
                        Organizador = proyecto.OrganizadorResponsable is null
                            ? proyecto.NombreResponsable
                            : NombreCompleto(proyecto.OrganizadorResponsable),
                        Estado = proyecto.EstadoAprobacion,
                        FechaEnvio = proyecto.FechaSolicitud,
                        FechaRevision = proyecto.FechaRevision,
                        Comentario = proyecto.ComentarioAprobacion,
                        UrlDetalle = Url.Action("Detalle", "Proyectos", new { id = proyecto.IdProyecto }),
                        UrlEdicion = Url.Action("Editar", "Proyectos", new { id = proyecto.IdProyecto })
                    }))
                .OrderByDescending(solicitud => solicitud.FechaEnvio)
                .ToList(),
            PublicacionesInactivas = eventos
                .Where(evento => !evento.Activo)
                .Select(evento => new SolicitudPublicacionViewModel
                {
                    Id = evento.IdEvento,
                    Tipo = "Evento",
                    Nombre = evento.Nombre,
                    Organizador = NombreCompleto(evento.Organizador),
                    Estado = evento.EstadoAprobacion,
                    FechaEnvio = evento.FechaCreacion,
                    ReactivacionSolicitada = evento.ReactivacionSolicitada,
                    PuedeSolicitarReactivacion =
                        puedeRevisarSolicitudes &&
                        evento.IdUsuarioOrganizador == usuarioId &&
                        evento.IdUsuarioSolicitante != usuarioId,
                    MotivoEliminacion = evento.MotivoEliminacion,
                    UrlDetalle = Url.Action("Detalle", "Eventos", new { id = evento.IdEvento })
                })
                .Concat(proyectos
                    .Where(proyecto => !proyecto.Activo)
                    .Select(proyecto => new SolicitudPublicacionViewModel
                    {
                        Id = proyecto.IdProyecto,
                        Tipo = "Proyecto",
                        Nombre = proyecto.Nombre,
                        Organizador = proyecto.OrganizadorResponsable is null
                            ? proyecto.NombreResponsable
                            : NombreCompleto(proyecto.OrganizadorResponsable),
                        Estado = proyecto.EstadoAprobacion,
                        FechaEnvio = proyecto.FechaPublicacion,
                        ReactivacionSolicitada = proyecto.ReactivacionSolicitada,
                        PuedeSolicitarReactivacion =
                            puedeRevisarSolicitudes &&
                            proyecto.IdUsuarioResponsable == usuarioId &&
                            proyecto.IdUsuario != usuarioId,
                        MotivoEliminacion = proyecto.MotivoEliminacion,
                        UrlDetalle = Url.Action("Detalle", "Proyectos", new { id = proyecto.IdProyecto })
                    }))
                .OrderByDescending(publicacion => publicacion.FechaEnvio)
                .ToList(),
            PendientesPorRevisar = puedeRevisarSolicitudes
                ? eventos
                .Where(evento =>
                    evento.IdUsuarioOrganizador == usuarioId &&
                    evento.Activo &&
                    evento.EstadoAprobacion == EstadoAprobacion.Pendiente)
                .Select(evento => new SolicitudPublicacionViewModel
                {
                    Id = evento.IdEvento,
                    Tipo = "Evento",
                    Nombre = evento.Nombre,
                    Organizador = NombreCompleto(evento.Solicitante),
                    Estado = evento.EstadoAprobacion,
                    FechaEnvio = evento.FechaSolicitud,
                    UrlDetalle = Url.Action("Detalle", "Eventos", new { id = evento.IdEvento })
                })
                .Concat(proyectos
                    .Where(proyecto =>
                        proyecto.IdUsuarioResponsable == usuarioId &&
                        proyecto.Activo &&
                        proyecto.EstadoAprobacion == EstadoAprobacion.Pendiente)
                    .Select(proyecto => new SolicitudPublicacionViewModel
                    {
                        Id = proyecto.IdProyecto,
                        Tipo = "Proyecto",
                        Nombre = proyecto.Nombre,
                        Organizador = proyecto.Usuario.Nombres + " " + proyecto.Usuario.ApellidoPaterno,
                        Estado = proyecto.EstadoAprobacion,
                        FechaEnvio = proyecto.FechaSolicitud,
                        UrlDetalle = Url.Action("Detalle", "Proyectos", new { id = proyecto.IdProyecto })
                    }))
                .OrderBy(solicitud => solicitud.FechaEnvio)
                .ToList()
                : new List<SolicitudPublicacionViewModel>()
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SolicitarReactivacion(string tipo, int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        var tipoNormalizado = tipo?.Trim().ToLowerInvariant();
        int? idEvento = null;
        int? idProyecto = null;
        string nombrePublicacion;

        if (tipoNormalizado == "evento")
        {
            var evento = await dbContext.Eventos.FirstOrDefaultAsync(item => item.IdEvento == id);
            if (evento is null)
            {
                return NotFound();
            }

            if (!await EsOrganizadorActivoAsync(usuarioId) ||
                evento.IdUsuarioOrganizador != usuarioId ||
                evento.IdUsuarioSolicitante == usuarioId)
            {
                return Forbid();
            }

            if (evento.Activo)
            {
                TempData["Mensaje"] = "La publicación ya está activa.";
                return RedirectToAction(nameof(Index));
            }
            if (evento.ReactivacionSolicitada)
            {
                TempData["Mensaje"] = "La solicitud de reactivación ya fue enviada al administrador.";
                return RedirectToAction(nameof(Index));
            }

            evento.ReactivacionSolicitada = true;
            evento.FechaSolicitudReactivacion = DateTime.UtcNow;
            idEvento = evento.IdEvento;
            nombrePublicacion = evento.Nombre;
        }
        else if (tipoNormalizado == "proyecto")
        {
            var proyecto = await dbContext.Proyectos.FirstOrDefaultAsync(item => item.IdProyecto == id);
            if (proyecto is null)
            {
                return NotFound();
            }

            if (!await EsOrganizadorActivoAsync(usuarioId) ||
                proyecto.IdUsuarioResponsable != usuarioId ||
                proyecto.IdUsuario == usuarioId)
            {
                return Forbid();
            }

            if (proyecto.Activo)
            {
                TempData["Mensaje"] = "La publicación ya está activa.";
                return RedirectToAction(nameof(Index));
            }
            if (proyecto.ReactivacionSolicitada)
            {
                TempData["Mensaje"] = "La solicitud de reactivación ya fue enviada al administrador.";
                return RedirectToAction(nameof(Index));
            }

            proyecto.ReactivacionSolicitada = true;
            proyecto.FechaSolicitudReactivacion = DateTime.UtcNow;
            idProyecto = proyecto.IdProyecto;
            nombrePublicacion = proyecto.Nombre;
        }
        else
        {
            return BadRequest();
        }

        var administradores = await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .Where(usuario =>
                usuario.Activo &&
                (usuario.IdRol == 3 || usuario.Rol.Nombre.Contains("Admin")))
            .Select(usuario => usuario.IdUsuario)
            .ToListAsync();

        foreach (var idAdministrador in administradores)
        {
            dbContext.Notificaciones.Add(new Notificacion
            {
                IdUsuario = idAdministrador,
                IdEvento = idEvento,
                IdProyecto = idProyecto,
                Titulo = $"Solicitud de reactivación: {nombrePublicacion}",
                Mensaje = $"El usuario {User.Identity?.Name ?? "de la comunidad"} solicita reactivar esta publicación.",
                FechaCreacion = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
        TempData["Mensaje"] = "La solicitud de reactivación se envió al administrador.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolverEvento(int id, bool aprobar, string? comentario)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (!await EsOrganizadorActivoAsync(usuarioId))
        {
            return Forbid();
        }

        var evento = await dbContext.Eventos
            .Include(item => item.Solicitante)
            .FirstOrDefaultAsync(item => item.IdEvento == id);
        if (evento is null)
        {
            return NotFound();
        }

        if (evento.IdUsuarioOrganizador != usuarioId || evento.IdUsuarioSolicitante == usuarioId)
        {
            return Forbid();
        }

        if (!evento.Activo || evento.EstadoAprobacion != EstadoAprobacion.Pendiente)
        {
            TempData["Mensaje"] = "Esta solicitud ya fue revisada.";
            return RedirectToAction(nameof(Index));
        }

        if (!aprobar && string.IsNullOrWhiteSpace(comentario))
        {
            TempData["Mensaje"] = "Agrega un motivo para rechazar la solicitud.";
            return RedirectToAction(nameof(Index));
        }
        if (comentario?.Length > 1000)
        {
            TempData["Mensaje"] = "El comentario no puede superar los 1000 caracteres.";
            return RedirectToAction(nameof(Index));
        }

        evento.EstadoAprobacion = aprobar ? EstadoAprobacion.Aprobado : EstadoAprobacion.Rechazado;
        evento.ComentarioAprobacion = comentario?.Trim();
        evento.FechaRevision = DateTime.UtcNow;
        dbContext.Notificaciones.Add(new Notificacion
        {
            IdUsuario = evento.IdUsuarioSolicitante,
            IdEvento = evento.IdEvento,
            Titulo = $"Solicitud de evento { (aprobar ? "aprobada" : "rechazada") }",
            Mensaje = aprobar
                ? $"El organizador aprobó «{evento.Nombre}». Ya aparece en el campus."
                : $"El organizador rechazó «{evento.Nombre}». Motivo: {evento.ComentarioAprobacion}",
            FechaCreacion = DateTime.UtcNow
        });
        if (aprobar)
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
                    Mensaje = "El organizador aprobó los cambios. Consulta la publicación para ver la información actualizada.",
                    FechaCreacion = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync();
        TempData["Mensaje"] = aprobar ? "El evento fue aprobado y ya es visible." : "El evento fue rechazado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolverProyecto(int id, bool aprobar, string? comentario)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return Challenge();
        }

        if (!await EsOrganizadorActivoAsync(usuarioId))
        {
            return Forbid();
        }

        var proyecto = await dbContext.Proyectos
            .FirstOrDefaultAsync(item => item.IdProyecto == id);
        if (proyecto is null)
        {
            return NotFound();
        }

        if (proyecto.IdUsuarioResponsable != usuarioId || proyecto.IdUsuario == usuarioId)
        {
            return Forbid();
        }

        if (!proyecto.Activo || proyecto.EstadoAprobacion != EstadoAprobacion.Pendiente)
        {
            TempData["Mensaje"] = "Esta solicitud ya fue revisada.";
            return RedirectToAction(nameof(Index));
        }

        if (!aprobar && string.IsNullOrWhiteSpace(comentario))
        {
            TempData["Mensaje"] = "Agrega un motivo para rechazar la solicitud.";
            return RedirectToAction(nameof(Index));
        }
        if (comentario?.Length > 1000)
        {
            TempData["Mensaje"] = "El comentario no puede superar los 1000 caracteres.";
            return RedirectToAction(nameof(Index));
        }

        proyecto.EstadoAprobacion = aprobar ? EstadoAprobacion.Aprobado : EstadoAprobacion.Rechazado;
        proyecto.ComentarioAprobacion = comentario?.Trim();
        proyecto.FechaRevision = DateTime.UtcNow;
        dbContext.Notificaciones.Add(new Notificacion
        {
            IdUsuario = proyecto.IdUsuario,
            IdProyecto = proyecto.IdProyecto,
            Titulo = $"Solicitud de proyecto { (aprobar ? "aprobada" : "rechazada") }",
            Mensaje = aprobar
                ? $"El organizador aprobó «{proyecto.Nombre}». Ya aparece en el campus."
                : $"El organizador rechazó «{proyecto.Nombre}». Motivo: {proyecto.ComentarioAprobacion}",
            FechaCreacion = DateTime.UtcNow
        });
        if (aprobar)
        {
            var colaboradores = await dbContext.RegistrosProyectos
                .AsNoTracking()
                .Where(registro => registro.IdProyecto == proyecto.IdProyecto)
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
                    Mensaje = "El organizador aprobó los cambios. Consulta la publicación para ver la información actualizada.",
                    FechaCreacion = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync();
        TempData["Mensaje"] = aprobar ? "El proyecto fue aprobado y ya es visible." : "El proyecto fue rechazado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> EsOrganizadorActivoAsync(int usuarioId)
    {
        return await dbContext.Usuarios
            .AsNoTracking()
            .Include(usuario => usuario.Rol)
            .AnyAsync(usuario =>
                usuario.IdUsuario == usuarioId &&
                usuario.Activo &&
                (usuario.IdRol == 2 || usuario.Rol.Nombre.Contains("Organizador")));
    }

    private static string NombreCompleto(Usuario usuario)
    {
        return string.Join(
            " ",
            new[] { usuario.Nombres, usuario.ApellidoPaterno, usuario.ApellidoMaterno }
                .Where(parte => !string.IsNullOrWhiteSpace(parte)));
    }
}
