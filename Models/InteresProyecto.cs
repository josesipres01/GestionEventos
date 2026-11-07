using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("INTERESES_PROYECTOS")]
public class InteresProyecto
{
    [Key]
    [Column("IdInteresProyecto")]
    public int IdInteresProyecto { get; set; }

    public int IdUsuario { get; set; }
    public int IdProyecto { get; set; }
    public DateTime FechaInteres { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Proyecto Proyecto { get; set; } = null!;
}
