using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("PROYECTOS")]
public class Proyecto : IAprobacionPublicacion
{
    [Key]
    [Column("IdProyecto")]
    public int IdProyecto { get; set; }

    public int IdUsuarioCreador { get; set; }
    public int? IdUsuarioOrganizador { get; set; }
    public bool Activo { get; set; } = true;
    public bool ReactivacionSolicitada { get; set; }
    public DateTime? FechaSolicitudReactivacion { get; set; }
    [StringLength(1000)]
    public string? MotivoEliminacion { get; set; }
    public DateTime? FechaEliminacion { get; set; }
    public int? IdUsuarioEliminador { get; set; }

    [Required]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string LugarReunion { get; set; } = string.Empty;

    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }

    [StringLength(500)]
    public string? ImagenUrl { get; set; }

    public int IdCarrera { get; set; }
    public DateTime FechaPublicacion { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoAprobacion EstadoAprobacion { get; set; } = EstadoAprobacion.Pendiente;

    [StringLength(1000)]
    public string? ComentarioAprobacion { get; set; }
    public DateTime? FechaRevision { get; set; }
    public int? IdUsuarioRevisor { get; set; }

    public Usuario Creador { get; set; } = null!;
    public Usuario? Organizador { get; set; }
    public Carrera Carrera { get; set; } = null!;
    public ICollection<RegistroProyecto> Registros { get; set; } = new List<RegistroProyecto>();
}
