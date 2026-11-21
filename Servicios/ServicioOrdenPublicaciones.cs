using System.Data;
using GestionEventos.Data;
using GestionEventos.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GestionEventos.Servicios;

public enum ResultadoOrdenamiento
{
    Correcto,
    SolicitudInvalida,
    Prohibido
}

public static class ServicioOrdenPublicaciones
{
    public static async Task CrearEventoAsync(
        GestionEventosDbContext dbContext,
        Evento nuevoEvento)
    {
        if (dbContext.Database.IsSqlServer())
        {
            var connection = dbContext.Database.GetDbConnection();
            var cerrarConexion = connection.State != ConnectionState.Open;
            if (cerrarConexion)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.Eventos_Insertar";
                command.CommandType = CommandType.StoredProcedure;
                command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

                AgregarParametro(command, "@Nombre", nuevoEvento.Nombre);
                AgregarParametro(command, "@Descripcion", nuevoEvento.Descripcion);
                AgregarParametro(command, "@FechaHora", nuevoEvento.FechaHora);
                AgregarParametro(command, "@FechaFin", nuevoEvento.FechaFin);
                AgregarParametro(command, "@Cupo", (object?)nuevoEvento.Cupo ?? DBNull.Value);
                AgregarParametro(command, "@Lugar", nuevoEvento.Lugar);
                AgregarParametro(command, "@ImagenUrl", (object?)nuevoEvento.ImagenUrl ?? DBNull.Value);
                AgregarParametro(command, "@IdUsuarioOrganizador", nuevoEvento.IdUsuarioOrganizador);
                AgregarParametro(command, "@IdUsuarioSolicitante", nuevoEvento.IdUsuarioSolicitante);
                AgregarParametro(command, "@EstadoAprobacion", nuevoEvento.EstadoAprobacion.ToString());
                AgregarParametro(command, "@FechaSolicitud", nuevoEvento.FechaSolicitud);
                AgregarParametro(command, "@FechaCreacion", nuevoEvento.FechaCreacion);

                var resultado = await command.ExecuteScalarAsync();
                nuevoEvento.IdEvento = Convert.ToInt32(resultado);
                return;
            }
            finally
            {
                if (cerrarConexion)
                {
                    await connection.CloseAsync();
                }
            }
        }

        // Respaldo para proveedores en memoria / SQLite en pruebas automatizadas:
        // Equivalente a SELECT @Orden = COALESCE(MAX(Orden), 0) + 1
        var maxOrden = await dbContext.Eventos
            .AsNoTracking()
            .Select(e => (int?)e.Orden)
            .MaxAsync() ?? 0;

        nuevoEvento.Orden = maxOrden + 1;
        dbContext.Eventos.Add(nuevoEvento);
        await dbContext.SaveChangesAsync();
    }

    public static async Task CrearProyectoAsync(
        GestionEventosDbContext dbContext,
        Proyecto nuevoProyecto)
    {
        if (dbContext.Database.IsSqlServer())
        {
            var connection = dbContext.Database.GetDbConnection();
            var cerrarConexion = connection.State != ConnectionState.Open;
            if (cerrarConexion)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.Proyectos_Insertar";
                command.CommandType = CommandType.StoredProcedure;
                command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

                AgregarParametro(command, "@IdUsuarioCreador", nuevoProyecto.IdUsuarioCreador);
                AgregarParametro(command, "@IdUsuarioOrganizador", (object?)nuevoProyecto.IdUsuarioOrganizador ?? DBNull.Value);
                AgregarParametro(command, "@Nombre", nuevoProyecto.Nombre);
                AgregarParametro(command, "@Descripcion", nuevoProyecto.Descripcion);
                AgregarParametro(command, "@LugarReunion", nuevoProyecto.LugarReunion);
                AgregarParametro(command, "@FechaInicio", nuevoProyecto.FechaInicio);
                AgregarParametro(command, "@FechaFin", nuevoProyecto.FechaFin);
                AgregarParametro(command, "@Cupo", (object?)nuevoProyecto.Cupo ?? DBNull.Value);
                AgregarParametro(command, "@ImagenUrl", (object?)nuevoProyecto.ImagenUrl ?? DBNull.Value);
                AgregarParametro(command, "@IdCarrera", nuevoProyecto.IdCarrera);
                AgregarParametro(command, "@EstadoAprobacion", nuevoProyecto.EstadoAprobacion.ToString());
                AgregarParametro(command, "@FechaPublicacion", nuevoProyecto.FechaPublicacion);
                AgregarParametro(command, "@FechaSolicitud", nuevoProyecto.FechaSolicitud);

                var resultado = await command.ExecuteScalarAsync();
                nuevoProyecto.IdProyecto = Convert.ToInt32(resultado);
                return;
            }
            finally
            {
                if (cerrarConexion)
                {
                    await connection.CloseAsync();
                }
            }
        }

        // Respaldo para proveedores en memoria / SQLite en pruebas automatizadas:
        // Equivalente a SELECT @Orden = COALESCE(MAX(Orden), 0) + 1
        var maxOrden = await dbContext.Proyectos
            .AsNoTracking()
            .Select(p => (int?)p.Orden)
            .MaxAsync() ?? 0;

        nuevoProyecto.Orden = maxOrden + 1;
        dbContext.Proyectos.Add(nuevoProyecto);
        await dbContext.SaveChangesAsync();
    }

    public static async Task CrearCarreraAsync(
        GestionEventosDbContext dbContext,
        Carrera nuevaCarrera)
    {
        if (dbContext.Database.IsSqlServer())
        {
            var connection = dbContext.Database.GetDbConnection();
            var cerrarConexion = connection.State != ConnectionState.Open;
            if (cerrarConexion)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.Carreras_Insertar";
                command.CommandType = CommandType.StoredProcedure;
                command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

                AgregarParametro(command, "@Nombre", nuevaCarrera.Nombre);

                var resultado = await command.ExecuteScalarAsync();
                nuevaCarrera.IdCarrera = Convert.ToInt32(resultado);
                return;
            }
            finally
            {
                if (cerrarConexion)
                {
                    await connection.CloseAsync();
                }
            }
        }

        var maxOrden = await dbContext.Carreras
            .AsNoTracking()
            .Select(c => (int?)c.Orden)
            .MaxAsync() ?? 0;

        nuevaCarrera.Orden = maxOrden + 1;
        dbContext.Carreras.Add(nuevaCarrera);
        await dbContext.SaveChangesAsync();
    }

    public static async Task<ResultadoOrdenamiento> OrdenarCarrerasGlobalAsync(
        GestionEventosDbContext dbContext,
        int[]? ids)
    {
        if (ids is null || ids.Length == 0 || ids.Distinct().Count() != ids.Length)
        {
            return ResultadoOrdenamiento.SolicitudInvalida;
        }

        var carreras = await dbContext.Carreras
            .OrderBy(c => c.Orden == 0 ? int.MaxValue : c.Orden)
            .ThenBy(c => c.Nombre)
            .ToListAsync();

        var idsExistentes = carreras.Select(c => c.IdCarrera).ToHashSet();
        if (ids.Except(idsExistentes).Any())
        {
            return ResultadoOrdenamiento.Prohibido;
        }

        var porId = carreras.ToDictionary(c => c.IdCarrera);
        var idsSet = ids.ToHashSet();
        for (var indice = 0; indice < ids.Length; indice++)
        {
            porId[ids[indice]].Orden = indice + 1;
        }

        var siguienteOrden = ids.Length + 1;
        foreach (var carreraRestante in carreras.Where(c => !idsSet.Contains(c.IdCarrera)))
        {
            carreraRestante.Orden = siguienteOrden++;
        }

        await dbContext.SaveChangesAsync();
        return ResultadoOrdenamiento.Correcto;
    }

    private static void AgregarParametro(IDbCommand command, string nombre, object valor)
    {
        var parametro = command.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = valor;
        command.Parameters.Add(parametro);
    }

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
