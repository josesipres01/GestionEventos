using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("REGISTROS")]
public class Registro
{
    [Key]
    [Column("IdRegistro")]
    public int IdRegistro { get; set; }

    public int IdUsuario { get; set; }
    public int IdEvento { get; set; }
    public DateTime FechaRegistro { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Evento Evento { get; set; } = null!;
}
