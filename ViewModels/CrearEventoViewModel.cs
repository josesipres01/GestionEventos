using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace GestionEventos.ViewModels;

public class CrearEventoViewModel
{
    [Required(ErrorMessage = "El nombre del evento es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Nombre del evento")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha y hora son obligatorias.")]
    [Display(Name = "Fecha y hora")]
    public DateTime FechaHora { get; set; } = DateTime.Now.AddDays(7).Date.AddHours(10);

    [Required(ErrorMessage = "El lugar es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Lugar donde se realizará")]
    public string Lugar { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un organizador responsable.")]
    [Display(Name = "Organizador que aprobará la solicitud")]
    public int IdUsuarioOrganizador { get; set; }

    [Display(Name = "Imagen de miniatura (opcional)")]
    public IFormFile? ImagenArchivo { get; set; }
}
