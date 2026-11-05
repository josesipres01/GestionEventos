namespace GestionEventos.Models;

public interface IAprobacionPublicacion
{
    EstadoAprobacion EstadoAprobacion { get; set; }
    string? ComentarioAprobacion { get; set; }
    DateTime FechaSolicitud { get; set; }
    DateTime? FechaRevision { get; set; }
    int? IdUsuarioRevisor { get; set; }

}

public static class AprobacionPublicacionExtensions
{
    public static void RegistrarRevision(
        this IAprobacionPublicacion publicacion,
        EstadoAprobacion estado,
        string? comentario,
        int idRevisor,
        DateTime fecha)
    {
        publicacion.EstadoAprobacion = estado;
        publicacion.ComentarioAprobacion = comentario;
        publicacion.IdUsuarioRevisor = idRevisor;
        publicacion.FechaRevision = fecha;
    }
}
