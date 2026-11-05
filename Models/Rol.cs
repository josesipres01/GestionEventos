using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("ROLES")]
public class Rol
{
    [Key]
    [Column("IdRol")]
    public int IdRol { get; set; }

    [Required]
    [StringLength(30)]
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
