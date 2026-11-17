using GestionEventos.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionEventos.Servicios;

public enum ResultadoOrdenamiento
{
    Correcto,
    SolicitudInvalida,
    Prohibido
}

public static class ServicioOrdenPublicaciones
{
    public static async Task<ResultadoOrdenamiento> OrdenarEventosGlobalAsync(
        GestionEventosDbContext dbContext,
        int[]? ids)
    {
        if (ids is null || ids.Length == 0 || ids.Distinct().Count() != ids.Length)
        {
            return ResultadoOrdenamiento.SolicitudInvalida;
        }

        var eventos = await dbContext.Eventos
            .OrderBy(e => e.Orden == 0 ? int.MaxValue : e.Orden)
            .ThenByDescending(e => e.FechaHora)
            .ThenBy(e => e.IdEvento)
            .ToListAsync();

        var idsExistentes = eventos.Select(e => e.IdEvento).ToHashSet();
        if (ids.Except(idsExistentes).Any())
        {
            return ResultadoOrdenamiento.Prohibido;
        }

        var porId = eventos.ToDictionary(e => e.IdEvento);
        var idsSet = ids.ToHashSet();
        for (var indice = 0; indice < ids.Length; indice++)
        {
            porId[ids[indice]].Orden = indice + 1;
        }

        var siguienteOrden = ids.Length + 1;
        foreach (var eventoRestante in eventos.Where(e => !idsSet.Contains(e.IdEvento)))
        {
            eventoRestante.Orden = siguienteOrden++;
        }

        await dbContext.SaveChangesAsync();
        return ResultadoOrdenamiento.Correcto;
    }

    public static async Task<ResultadoOrdenamiento> OrdenarProyectosGlobalAsync(
        GestionEventosDbContext dbContext,
        int[]? ids)
    {
        if (ids is null || ids.Length == 0 || ids.Distinct().Count() != ids.Length)
        {
            return ResultadoOrdenamiento.SolicitudInvalida;
        }

        var proyectos = await dbContext.Proyectos
            .OrderBy(p => p.Orden == 0 ? int.MaxValue : p.Orden)
            .ThenByDescending(p => p.FechaPublicacion)
            .ThenBy(p => p.IdProyecto)
            .ToListAsync();

        var idsExistentes = proyectos.Select(p => p.IdProyecto).ToHashSet();
        if (ids.Except(idsExistentes).Any())
        {
            return ResultadoOrdenamiento.Prohibido;
        }

        var porId = proyectos.ToDictionary(p => p.IdProyecto);
        var idsSet = ids.ToHashSet();
        for (var indice = 0; indice < ids.Length; indice++)
        {
            porId[ids[indice]].Orden = indice + 1;
        }

        var siguienteOrden = ids.Length + 1;
        foreach (var proyectoRestante in proyectos.Where(p => !idsSet.Contains(p.IdProyecto)))
        {
            proyectoRestante.Orden = siguienteOrden++;
        }

        await dbContext.SaveChangesAsync();
        return ResultadoOrdenamiento.Correcto;
    }

    public static async Task<ResultadoOrdenamiento> OrdenarEventosUsuarioAsync(
        GestionEventosDbContext dbContext,
        int usuarioId,
        bool esAdmin,
        int[]? ids)
    {
        if (ids is null || ids.Length == 0 || ids.Distinct().Count() != ids.Length)
        {
            return ResultadoOrdenamiento.SolicitudInvalida;
        }

        var todosEventos = await dbContext.Eventos
            .OrderBy(e => e.Orden == 0 ? int.MaxValue : e.Orden)
            .ThenByDescending(e => e.FechaHora)
            .ThenBy(e => e.IdEvento)
            .ToListAsync();

        var idsPermitidos = (esAdmin
                ? todosEventos
                : todosEventos.Where(e => e.IdUsuarioOrganizador == usuarioId || e.IdUsuarioSolicitante == usuarioId))
            .Select(e => e.IdEvento)
            .ToHashSet();

        var idsNoPertenecenAlUsuario = ids.Except(idsPermitidos).ToList();
        if (idsNoPertenecenAlUsuario.Count > 0)
        {
            return ResultadoOrdenamiento.Prohibido;
        }

        var requiereNormalizacion = todosEventos.Any(e => e.Orden <= 0) ||
            todosEventos.Select(e => e.Orden).Distinct().Count() != todosEventos.Count;
        if (requiereNormalizacion)
        {
            for (var indice = 0; indice < todosEventos.Count; indice++)
            {
                todosEventos[indice].Orden = indice + 1;
            }
        }

        var porId = todosEventos.ToDictionary(e => e.IdEvento);
        var posicionesAsignadas = ids
            .Select(id => porId[id].Orden)
            .OrderBy(orden => orden)
            .ToList();

        for (var indice = 0; indice < ids.Length; indice++)
        {
            porId[ids[indice]].Orden = posicionesAsignadas[indice];
        }

        await dbContext.SaveChangesAsync();
        return ResultadoOrdenamiento.Correcto;
    }

    public static async Task<ResultadoOrdenamiento> OrdenarProyectosUsuarioAsync(
        GestionEventosDbContext dbContext,
        int usuarioId,
        bool esAdmin,
        int[]? ids)
    {
        if (ids is null || ids.Length == 0 || ids.Distinct().Count() != ids.Length)
        {
            return ResultadoOrdenamiento.SolicitudInvalida;
        }

        var todosProyectos = await dbContext.Proyectos
            .OrderBy(p => p.Orden == 0 ? int.MaxValue : p.Orden)
            .ThenByDescending(p => p.FechaPublicacion)
            .ThenBy(p => p.IdProyecto)
            .ToListAsync();

        var idsPermitidos = (esAdmin
                ? todosProyectos
                : todosProyectos.Where(p => p.IdUsuarioOrganizador == usuarioId || p.IdUsuarioCreador == usuarioId))
            .Select(p => p.IdProyecto)
            .ToHashSet();

        var idsNoPertenecenAlUsuario = ids.Except(idsPermitidos).ToList();
        if (idsNoPertenecenAlUsuario.Count > 0)
        {
            return ResultadoOrdenamiento.Prohibido;
        }

        var requiereNormalizacion = todosProyectos.Any(p => p.Orden <= 0) ||
            todosProyectos.Select(p => p.Orden).Distinct().Count() != todosProyectos.Count;
        if (requiereNormalizacion)
        {
            for (var indice = 0; indice < todosProyectos.Count; indice++)
            {
                todosProyectos[indice].Orden = indice + 1;
            }
        }

        var porId = todosProyectos.ToDictionary(p => p.IdProyecto);
        var posicionesAsignadas = ids
            .Select(id => porId[id].Orden)
            .OrderBy(orden => orden)
            .ToList();

        for (var indice = 0; indice < ids.Length; indice++)
        {
            porId[ids[indice]].Orden = posicionesAsignadas[indice];
        }

        await dbContext.SaveChangesAsync();
        return ResultadoOrdenamiento.Correcto;
    }
}
