using GestionEventos.Models;

namespace GestionEventos.ViewModels;

public class SolicitudesViewModel
{
    public bool PuedeRevisarSolicitudes { get; set; }
    public List<SolicitudPublicacionViewModel> MisSolicitudes { get; set; } = new();
    public List<SolicitudPublicacionViewModel> PublicacionesInactivas { get; set; } = new();
    public List<SolicitudPublicacionViewModel> PendientesPorRevisar { get; set; } = new();
}

public class SolicitudPublicacionViewModel
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Organizador { get; set; } = string.Empty;
    public EstadoAprobacion Estado { get; set; }
    public DateTime FechaEnvio { get; set; }
    public DateTime? FechaRevision { get; set; }
    public string? Comentario { get; set; }
    public string? UrlDetalle { get; set; }
    public string? UrlEdicion { get; set; }
    public bool ReactivacionSolicitada { get; set; }
    public bool PuedeSolicitarReactivacion { get; set; }
    public string? MotivoEliminacion { get; set; }
}
