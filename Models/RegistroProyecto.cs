using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("REGISTROS_PROYECTOS")]
public class RegistroProyecto
{
    [Key]
    [Column("IdRegistroProyecto")]
    public int IdRegistroProyecto { get; set; }

    public int IdUsuario { get; set; }
    public int IdProyecto { get; set; }
    public DateTime FechaRegistro { get; set; }
    public EstadoRegistroProyecto Estado { get; set; } = EstadoRegistroProyecto.Aceptado;
    public int? IdUsuarioRevisor { get; set; }
    public DateTime? FechaRevision { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Proyecto Proyecto { get; set; } = null!;
}
