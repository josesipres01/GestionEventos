using System.ComponentModel.DataAnnotations;

namespace GestionEventos.ViewModels;

public class RegistroUsuarioViewModel
{
    [Required]
    [StringLength(80)]
    [Display(Name = "Nombres")]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    [Display(Name = "Apellido paterno")]
    public string ApellidoPaterno { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Apellido materno")]
    public string? ApellidoMaterno { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(8)]
    public string Contrasena { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarContrasena { get; set; } = string.Empty;

    [Display(Name = "Carrera")]
    public int? IdCarrera { get; set; }
}
