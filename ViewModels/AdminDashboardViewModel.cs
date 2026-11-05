using GestionEventos.Models;

namespace GestionEventos.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalUsuarios { get; set; }
    public int TotalOrganizadores { get; set; }
    public int TotalEstudiantes { get; set; }
    public int TotalAdministradores { get; set; }
    public int TotalEventos { get; set; }
    public int TotalProyectos { get; set; }
    public int TotalRegistrosEventos { get; set; }
    public int TotalCarreras { get; set; }

    public List<Usuario> Usuarios { get; set; } = new();
    public List<Rol> Roles { get; set; } = new();
    public List<Carrera> Carreras { get; set; } = new();
    public List<Evento> EventosRecientes { get; set; } = new();
    public List<Proyecto> ProyectosRecientes { get; set; } = new();
}

