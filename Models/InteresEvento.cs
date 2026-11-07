using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("INTERESES_EVENTOS")]
public class InteresEvento
{
    [Key]
    [Column("IdInteresEvento")]
    public int IdInteresEvento { get; set; }

    public int IdUsuario { get; set; }
    public int IdEvento { get; set; }
    public DateTime FechaInteres { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Evento Evento { get; set; } = null!;
}
