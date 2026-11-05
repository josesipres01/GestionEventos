using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("NOTIFICACIONES")]
public class Notificacion
{
    [Key]
    [Column("IdNotificacion")]
    public int IdNotificacion { get; set; }

    public int IdUsuario { get; set; }
    public int? IdEvento { get; set; }
    public int? IdProyecto { get; set; }

    [Required]
    [StringLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required]
    public string Mensaje { get; set; } = string.Empty;

    public bool Leida { get; set; }
    public DateTime FechaCreacion { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Evento? Evento { get; set; }
    public Proyecto? Proyecto { get; set; }
}

