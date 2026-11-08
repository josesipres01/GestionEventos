using GestionEventos.Models;

namespace GestionEventos.ViewModels;

public class ListaAsistenciaViewModel
{
    public Evento Evento { get; set; } = null!;
    public List<ParticipanteAsistenciaViewModel> Participantes { get; set; } = new();
}

public class ListaParticipacionProyectoViewModel
{
    public Proyecto Proyecto { get; set; } = null!;
    public List<ParticipanteAsistenciaViewModel> Participantes { get; set; } = new();
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
