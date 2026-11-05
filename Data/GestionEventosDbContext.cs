using GestionEventos.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionEventos.Data;

public class GestionEventosDbContext(DbContextOptions<GestionEventosDbContext> options) : DbContext(options)
{
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Carrera> Carreras => Set<Carrera>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<Registro> Registros => Set<Registro>();
    public DbSet<RegistroProyecto> RegistrosProyectos => Set<RegistroProyecto>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>()
            .HasOne(usuario => usuario.Rol)
            .WithMany(rol => rol.Usuarios)
            .HasForeignKey(usuario => usuario.IdRol);

        modelBuilder.Entity<Usuario>()
            .HasOne(usuario => usuario.Carrera)
            .WithMany(carrera => carrera.Usuarios)
            .HasForeignKey(usuario => usuario.IdCarrera);

        modelBuilder.Entity<Evento>()
            .HasOne(evento => evento.Organizador)
            .WithMany(usuario => usuario.EventosOrganizados)
            .HasForeignKey(evento => evento.IdUsuarioOrganizador);

        modelBuilder.Entity<Evento>()
            .HasOne(evento => evento.Solicitante)
            .WithMany()
            .HasForeignKey(evento => evento.IdUsuarioSolicitante)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Evento>()
            .Property(evento => evento.EstadoAprobacion)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Evento>()
            .Property(evento => evento.ComentarioAprobacion)
            .HasMaxLength(1000);

        modelBuilder.Entity<Proyecto>()
            .HasOne(proyecto => proyecto.Usuario)
            .WithMany(usuario => usuario.Proyectos)
            .HasForeignKey(proyecto => proyecto.IdUsuario);

        modelBuilder.Entity<Proyecto>()
            .HasOne(proyecto => proyecto.OrganizadorResponsable)
            .WithMany()
            .HasForeignKey(proyecto => proyecto.IdUsuarioResponsable)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Proyecto>()
            .Property(proyecto => proyecto.EstadoAprobacion)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Proyecto>()
            .Property(proyecto => proyecto.ComentarioAprobacion)
            .HasMaxLength(1000);

        modelBuilder.Entity<Proyecto>()
            .HasOne(proyecto => proyecto.Carrera)
            .WithMany(carrera => carrera.Proyectos)
            .HasForeignKey(proyecto => proyecto.IdCarrera);

        modelBuilder.Entity<Registro>()
            .HasOne(registro => registro.Usuario)
            .WithMany(usuario => usuario.Registros)
            .HasForeignKey(registro => registro.IdUsuario);

        modelBuilder.Entity<Registro>()
            .HasOne(registro => registro.Evento)
            .WithMany(evento => evento.Registros)
            .HasForeignKey(registro => registro.IdEvento);

        modelBuilder.Entity<Registro>()
            .HasIndex(registro => new { registro.IdUsuario, registro.IdEvento })
            .IsUnique();

        modelBuilder.Entity<RegistroProyecto>()
            .HasOne(registro => registro.Usuario)
            .WithMany(usuario => usuario.RegistrosProyectos)
            .HasForeignKey(registro => registro.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<RegistroProyecto>()
            .HasOne(registro => registro.Proyecto)
            .WithMany(proyecto => proyecto.Registros)
            .HasForeignKey(registro => registro.IdProyecto)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RegistroProyecto>()
            .HasIndex(registro => new { registro.IdUsuario, registro.IdProyecto })
            .IsUnique();

        modelBuilder.Entity<Notificacion>()
            .HasOne(notificacion => notificacion.Usuario)
            .WithMany(usuario => usuario.Notificaciones)
            .HasForeignKey(notificacion => notificacion.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notificacion>()
            .HasOne(notificacion => notificacion.Evento)
            .WithMany()
            .HasForeignKey(notificacion => notificacion.IdEvento)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Notificacion>()
            .HasOne(notificacion => notificacion.Proyecto)
            .WithMany()
            .HasForeignKey(notificacion => notificacion.IdProyecto)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
