using System.ComponentModel.DataAnnotations;

namespace GestionEventos.ViewModels;

public class RegistrarOrganizadorViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80)]
    [Display(Name = "Nombre(s)")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido paterno es obligatorio.")]
    [StringLength(60)]
    [Display(Name = "Apellido paterno")]
    public string ApellidoPaterno { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Apellido materno")]
    public string? ApellidoMaterno { get; set; }

    [Required(ErrorMessage = "El correo institucional es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido.")]
    [StringLength(256)]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el rol del usuario.")]
    [Display(Name = "Rol asignado")]
    public int IdRol { get; set; } = 2; // Por defecto 2 = Organizador/Docente

    [Display(Name = "Carrera o departamento (opcional)")]
    public int? IdCarrera { get; set; }

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarContrasena { get; set; } = string.Empty;
}

