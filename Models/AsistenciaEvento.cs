using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("ASISTENCIAS_EVENTOS")]
public class AsistenciaEvento
{
    [Key]
    [Column("IdAsistenciaEvento")]
    public int IdAsistenciaEvento { get; set; }

    public int IdUsuario { get; set; }
    public int IdEvento { get; set; }
    public DateTime FechaAsistencia { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Evento Evento { get; set; } = null!;
}
