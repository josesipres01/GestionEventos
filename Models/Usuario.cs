using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionEventos.Models;

[Table("USUARIOS")]
public class Usuario
{
    [Key]
    [Column("IdUsuario")]
    public int IdUsuario { get; set; }

    [Required]
    [StringLength(80)]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    public string ApellidoPaterno { get; set; } = string.Empty;

    [StringLength(60)]
    public string? ApellidoMaterno { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Correo { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    public int IdRol { get; set; } = 1;
    public int? IdCarrera { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; }

    public Rol Rol { get; set; } = null!;
    public Carrera? Carrera { get; set; }
    public ICollection<Evento> EventosOrganizados { get; set; } = new List<Evento>();
    public ICollection<Proyecto> Proyectos { get; set; } = new List<Proyecto>();
    public ICollection<Registro> Registros { get; set; } = new List<Registro>();
    public ICollection<RegistroProyecto> RegistrosProyectos { get; set; } = new List<RegistroProyecto>();
    public ICollection<Notificacion> Notificaciones { get; set; } = new List<Notificacion>();
}
