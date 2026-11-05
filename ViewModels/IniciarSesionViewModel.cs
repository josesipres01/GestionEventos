using System.ComponentModel.DataAnnotations;

namespace GestionEventos.ViewModels;

public class IniciarSesionViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Contrasena { get; set; } = string.Empty;
}
