using System.ComponentModel.DataAnnotations;
using GestionEventos.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestionEventos.ViewModels;

public class ListaAsistenciaViewModel
{
    public Evento Evento { get; set; } = null!;
    [Display(Name = "Evento seleccionado (ordenado según la BD)")]
    public int IdEventoSeleccionado { get; set; }
    public IEnumerable<SelectListItem> Eventos { get; set; } = Enumerable.Empty<SelectListItem>();
    public List<ParticipanteAsistenciaViewModel> Participantes { get; set; } = new();
}

public class ListaParticipacionProyectoViewModel
{
    public Proyecto Proyecto { get; set; } = null!;
    [Display(Name = "Proyecto seleccionado (ordenado según la BD)")]
    public int IdProyectoSeleccionado { get; set; }
    public IEnumerable<SelectListItem> Proyectos { get; set; } = Enumerable.Empty<SelectListItem>();
    public List<ParticipanteAsistenciaViewModel> Participantes { get; set; } = new();
}

public class InscripcionEventoViewModel
{
    [Required(ErrorMessage = "Selecciona un evento de la lista.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un evento de la lista.")]
    [Display(Name = "Evento universitario")]
    public int IdEvento { get; set; }

    public IEnumerable<SelectListItem> Eventos { get; set; } = Enumerable.Empty<SelectListItem>();

    [Display(Name = "Nota o comentario de inscripción (opcional)")]
    [StringLength(300)]
    public string? Comentario { get; set; }
}

public class InscripcionProyectoViewModel
{
    [Required(ErrorMessage = "Selecciona un proyecto de la lista.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un proyecto de la lista.")]
    [Display(Name = "Proyecto académico")]
    public int IdProyecto { get; set; }

    public IEnumerable<SelectListItem> Proyectos { get; set; } = Enumerable.Empty<SelectListItem>();

    [Display(Name = "Carrera (ordenada según la BD)")]
    public int? IdCarrera { get; set; }

    public IEnumerable<SelectListItem> Carreras { get; set; } = Enumerable.Empty<SelectListItem>();

    [Display(Name = "Motivo de colaboración (opcional)")]
    [StringLength(300)]
    public string? Comentario { get; set; }
}

public class ParticipanteAsistenciaViewModel
{
    public int IdUsuario { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Carrera { get; set; } = string.Empty;
    public bool Inscrito { get; set; }
    public bool Interesado { get; set; }
    public bool Asistio { get; set; }
    public DateTime? FechaAcreditacion { get; set; }
}
