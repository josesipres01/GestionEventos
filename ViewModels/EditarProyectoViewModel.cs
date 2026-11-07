using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace GestionEventos.ViewModels;

public class EditarProyectoViewModel
{
    public int IdProyecto { get; set; }

    [Required(ErrorMessage = "El nombre del proyecto es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Nombre del proyecto")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un responsable.")]
    [Display(Name = "Organizador responsable")]
    public int IdUsuarioOrganizador { get; set; }

    [Required(ErrorMessage = "Selecciona una carrera.")]
    [Display(Name = "Carrera")]
    public int IdCarrera { get; set; }

    [Required(ErrorMessage = "El lugar de reunión es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Lugar de reunión")]
    public string LugarReunion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de inicio")]
    public DateTime FechaInicio { get; set; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de fin")]
    public DateTime FechaFin { get; set; }

    [Required(ErrorMessage = "Indica el cupo máximo de colaboradores.")]
    [Range(1, int.MaxValue, ErrorMessage = "El cupo debe ser mayor que cero.")]
    [Display(Name = "Cupo máximo de colaboradores")]
    public int? Cupo { get; set; }

    public string? ImagenUrlActual { get; set; }

    [Display(Name = "Reemplazar imagen de miniatura (opcional)")]
    public IFormFile? ImagenArchivo { get; set; }
}
