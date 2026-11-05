using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("CARRERAS")]
public class Carrera
{
    [Key]
    [Column("IdCarrera")]
    public int IdCarrera { get; set; }

    [Required]
    [StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Proyecto> Proyectos { get; set; } = new List<Proyecto>();
}
