using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("EVENTOS")]
public class Evento : IAprobacionPublicacion
{
    [Key]
    [Column("IdEvento")]
    public int IdEvento { get; set; }

    [Required]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    public string Descripcion { get; set; } = string.Empty;

    public DateTime FechaHora { get; set; }
    public DateTime FechaFin { get; set; }
    [Range(1, int.MaxValue)]
    public int? Cupo { get; set; }

    [Required]
    [StringLength(150)]
    public string Lugar { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ImagenUrl { get; set; }

    public int IdUsuarioOrganizador { get; set; }
    public int IdUsuarioSolicitante { get; set; }
    public bool Activo { get; set; } = true;
    public bool ReactivacionSolicitada { get; set; }
    public DateTime? FechaSolicitudReactivacion { get; set; }
    [StringLength(1000)]
    public string? MotivoEliminacion { get; set; }
    public DateTime? FechaEliminacion { get; set; }
    public int? IdUsuarioEliminador { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoAprobacion EstadoAprobacion { get; set; } = EstadoAprobacion.Pendiente;

    [StringLength(1000)]
    public string? ComentarioAprobacion { get; set; }
    public DateTime? FechaRevision { get; set; }
    public int? IdUsuarioRevisor { get; set; }

    [ForeignKey(nameof(IdUsuarioOrganizador))]
    public Usuario Organizador { get; set; } = null!;
    public Usuario Solicitante { get; set; } = null!;

    public ICollection<Registro> Registros { get; set; } = new List<Registro>();
    public ICollection<InteresEvento> Intereses { get; set; } = new List<InteresEvento>();
}
