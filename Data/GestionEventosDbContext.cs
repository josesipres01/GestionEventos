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
    public DbSet<InteresEvento> InteresesEventos => Set<InteresEvento>();
    public DbSet<InteresProyecto> InteresesProyectos => Set<InteresProyecto>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>()
            .HasOne(usuario => usuario.Rol)
            .WithMany(rol => rol.Usuarios)
            .HasForeignKey(usuario => usuario.IdRol)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_USUARIOS_ROLES");

        modelBuilder.Entity<Usuario>()
            .HasOne(usuario => usuario.Carrera)
            .WithMany(carrera => carrera.Usuarios)
            .HasForeignKey(usuario => usuario.IdCarrera)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Usuario>()
            .HasIndex(usuario => usuario.Correo)
            .IsUnique()
            .HasDatabaseName("UQ_USUARIOS_Correo");
        modelBuilder.Entity<Carrera>()
            .HasIndex(carrera => carrera.Nombre)
            .IsUnique()
            .HasDatabaseName("UQ_CARRERAS_Nombre");
        modelBuilder.Entity<Rol>()
            .HasIndex(rol => rol.Nombre)
            .IsUnique()
            .HasDatabaseName("UQ_ROLES_Nombre");

        modelBuilder.Entity<Evento>()
            .HasOne(evento => evento.Organizador)
            .WithMany(usuario => usuario.EventosOrganizados)
            .HasForeignKey(evento => evento.IdUsuarioOrganizador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_EVENTOS_USUARIOS_Organizador");

        modelBuilder.Entity<Evento>()
            .HasOne(evento => evento.Solicitante)
            .WithMany()
            .HasForeignKey(evento => evento.IdUsuarioSolicitante)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_EVENTOS_USUARIOS_Solicitante");

        modelBuilder.Entity<Evento>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(evento => evento.IdUsuarioRevisor)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_EVENTOS_USUARIOS_Revisor");

        modelBuilder.Entity<Evento>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(evento => evento.IdUsuarioEliminador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_EVENTOS_USUARIOS_Eliminador");

        modelBuilder.Entity<Evento>()
            .HasIndex(evento => evento.IdUsuarioOrganizador)
            .HasDatabaseName("IX_EVENTOS_IdUsuarioOrganizador");
        modelBuilder.Entity<Evento>()
            .HasIndex(evento => evento.IdUsuarioSolicitante)
            .HasDatabaseName("IX_EVENTOS_IdUsuarioSolicitante");
        modelBuilder.Entity<Evento>()
            .HasIndex(evento => new { evento.Activo, evento.EstadoAprobacion })
            .HasDatabaseName("IX_EVENTOS_Activo_EstadoAprobacion");
        modelBuilder.Entity<Evento>()
            .HasIndex(evento => evento.IdUsuarioRevisor)
            .HasDatabaseName("IX_EVENTOS_IdUsuarioRevisor");
        modelBuilder.Entity<Evento>()
            .HasIndex(evento => evento.IdUsuarioEliminador)
            .HasDatabaseName("IX_EVENTOS_IdUsuarioEliminador");
        modelBuilder.Entity<Evento>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_EVENTOS_EstadoAprobacion",
                    "[EstadoAprobacion] IN ('Pendiente', 'Aprobado', 'Rechazado')");
                table.HasCheckConstraint("CK_EVENTOS_Cupo", "[Cupo] IS NULL OR [Cupo] > 0");
                table.HasCheckConstraint("CK_EVENTOS_Fechas", "[FechaFin] > [FechaHora]");
            });

        modelBuilder.Entity<Evento>()
            .Property(evento => evento.EstadoAprobacion)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Evento>()
            .Property(evento => evento.ComentarioAprobacion)
            .HasMaxLength(1000);

        modelBuilder.Entity<Proyecto>()
            .HasOne(proyecto => proyecto.Creador)
            .WithMany(usuario => usuario.Proyectos)
            .HasForeignKey(proyecto => proyecto.IdUsuarioCreador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PROYECTOS_USUARIOS_Creador");

        modelBuilder.Entity<Proyecto>()
            .HasOne(proyecto => proyecto.Organizador)
            .WithMany()
            .HasForeignKey(proyecto => proyecto.IdUsuarioOrganizador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PROYECTOS_USUARIOS_Organizador");

        modelBuilder.Entity<Proyecto>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(proyecto => proyecto.IdUsuarioRevisor)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PROYECTOS_USUARIOS_Revisor");

        modelBuilder.Entity<Proyecto>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(proyecto => proyecto.IdUsuarioEliminador)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_PROYECTOS_USUARIOS_Eliminador");

        modelBuilder.Entity<Proyecto>()
            .HasIndex(proyecto => proyecto.IdUsuarioCreador)
            .HasDatabaseName("IX_PROYECTOS_IdUsuarioCreador");
        modelBuilder.Entity<Proyecto>()
            .HasIndex(proyecto => proyecto.IdUsuarioOrganizador)
            .HasDatabaseName("IX_PROYECTOS_IdUsuarioOrganizador");
        modelBuilder.Entity<Proyecto>()
            .HasIndex(proyecto => new { proyecto.Activo, proyecto.EstadoAprobacion })
            .HasDatabaseName("IX_PROYECTOS_Activo_EstadoAprobacion");
        modelBuilder.Entity<Proyecto>()
            .HasIndex(proyecto => proyecto.IdCarrera)
            .HasDatabaseName("IX_PROYECTOS_IdCarrera");
        modelBuilder.Entity<Proyecto>()
            .HasIndex(proyecto => proyecto.IdUsuarioRevisor)
            .HasDatabaseName("IX_PROYECTOS_IdUsuarioRevisor");
        modelBuilder.Entity<Proyecto>()
            .HasIndex(proyecto => proyecto.IdUsuarioEliminador)
            .HasDatabaseName("IX_PROYECTOS_IdUsuarioEliminador");

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
            .HasForeignKey(proyecto => proyecto.IdCarrera)
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Proyecto>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_PROYECTOS_EstadoAprobacion",
                    "[EstadoAprobacion] IN ('Pendiente', 'Aprobado', 'Rechazado')");
                table.HasCheckConstraint("CK_PROYECTOS_Cupo", "[Cupo] IS NULL OR [Cupo] > 0");
            });

        modelBuilder.Entity<Registro>()
            .HasOne(registro => registro.Usuario)
            .WithMany(usuario => usuario.Registros)
            .HasForeignKey(registro => registro.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Registro>()
            .HasOne(registro => registro.Evento)
            .WithMany(evento => evento.Registros)
            .HasForeignKey(registro => registro.IdEvento)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Registro>()
            .HasIndex(registro => new { registro.IdUsuario, registro.IdEvento })
            .IsUnique()
            .HasDatabaseName("UQ_REGISTROS_IdUsuario_IdEvento");
        modelBuilder.Entity<Registro>()
            .HasIndex(registro => registro.IdEvento)
            .HasDatabaseName("IX_REGISTROS_IdEvento");
        modelBuilder.Entity<Registro>()
            .HasIndex(registro => registro.IdUsuario)
            .HasDatabaseName("IX_REGISTROS_IdUsuario");

        modelBuilder.Entity<RegistroProyecto>()
            .HasOne(registro => registro.Usuario)
            .WithMany(usuario => usuario.RegistrosProyectos)
            .HasForeignKey(registro => registro.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_REGISTROS_PROYECTOS_USUARIOS");

        modelBuilder.Entity<RegistroProyecto>()
            .HasOne(registro => registro.Proyecto)
            .WithMany(proyecto => proyecto.Registros)
            .HasForeignKey(registro => registro.IdProyecto)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_REGISTROS_PROYECTOS_PROYECTOS");

        modelBuilder.Entity<RegistroProyecto>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(registro => registro.IdUsuarioRevisor)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_REGISTROS_PROYECTOS_Revisor");

        modelBuilder.Entity<RegistroProyecto>()
            .Property(registro => registro.Estado)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<RegistroProyecto>()
            .HasIndex(registro => new { registro.IdUsuario, registro.IdProyecto })
            .IsUnique()
            .HasDatabaseName("UQ_REGISTROS_PROYECTOS_IdUsuario_IdProyecto");
        modelBuilder.Entity<RegistroProyecto>()
            .HasIndex(registro => registro.IdProyecto)
            .HasDatabaseName("IX_REGISTROS_PROYECTOS_IdProyecto");
        modelBuilder.Entity<RegistroProyecto>()
            .HasIndex(registro => new { registro.IdProyecto, registro.Estado })
            .HasDatabaseName("IX_REGISTROS_PROYECTOS_IdProyecto_Estado");
        modelBuilder.Entity<RegistroProyecto>()
            .HasIndex(registro => registro.IdUsuario)
            .HasDatabaseName("IX_REGISTROS_PROYECTOS_IdUsuario");
        modelBuilder.Entity<RegistroProyecto>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_REGISTROS_PROYECTOS_Estado",
                "[Estado] IN ('Pendiente', 'Aceptado', 'Rechazado')"));

        modelBuilder.Entity<InteresEvento>()
            .HasOne(interes => interes.Usuario)
            .WithMany(usuario => usuario.InteresesEventos)
            .HasForeignKey(interes => interes.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_INTERESES_EVENTOS_USUARIOS");

        modelBuilder.Entity<InteresEvento>()
            .HasOne(interes => interes.Evento)
            .WithMany(evento => evento.Intereses)
            .HasForeignKey(interes => interes.IdEvento)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_INTERESES_EVENTOS_EVENTOS");

        modelBuilder.Entity<InteresEvento>()
            .HasIndex(interes => new { interes.IdUsuario, interes.IdEvento })
            .IsUnique()
            .HasDatabaseName("UQ_INTERESES_EVENTOS_IdUsuario_IdEvento");
        modelBuilder.Entity<InteresEvento>()
            .HasIndex(interes => interes.IdEvento)
            .HasDatabaseName("IX_INTERESES_EVENTOS_IdEvento");

        modelBuilder.Entity<InteresProyecto>()
            .HasOne(interes => interes.Usuario)
            .WithMany(usuario => usuario.InteresesProyectos)
            .HasForeignKey(interes => interes.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_INTERESES_PROYECTOS_USUARIOS");

        modelBuilder.Entity<InteresProyecto>()
            .HasOne(interes => interes.Proyecto)
            .WithMany(proyecto => proyecto.Intereses)
            .HasForeignKey(interes => interes.IdProyecto)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_INTERESES_PROYECTOS_PROYECTOS");

        modelBuilder.Entity<InteresProyecto>()
            .HasIndex(interes => new { interes.IdUsuario, interes.IdProyecto })
            .IsUnique()
            .HasDatabaseName("UQ_INTERESES_PROYECTOS_IdUsuario_IdProyecto");
        modelBuilder.Entity<InteresProyecto>()
            .HasIndex(interes => interes.IdProyecto)
            .HasDatabaseName("IX_INTERESES_PROYECTOS_IdProyecto");

        modelBuilder.Entity<Notificacion>()
            .HasOne(notificacion => notificacion.Usuario)
            .WithMany(usuario => usuario.Notificaciones)
            .HasForeignKey(notificacion => notificacion.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Notificacion>()
            .HasOne(notificacion => notificacion.Evento)
            .WithMany()
            .HasForeignKey(notificacion => notificacion.IdEvento)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_NOTIFICACIONES_EVENTOS");

        modelBuilder.Entity<Notificacion>()
            .HasOne(notificacion => notificacion.Proyecto)
            .WithMany()
            .HasForeignKey(notificacion => notificacion.IdProyecto)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_NOTIFICACIONES_PROYECTOS");

        modelBuilder.Entity<Notificacion>()
            .HasIndex(notificacion => notificacion.IdUsuario)
            .HasDatabaseName("IX_NOTIFICACIONES_IdUsuario");
        modelBuilder.Entity<Notificacion>()
            .HasIndex(notificacion => notificacion.IdEvento)
            .HasDatabaseName("IX_NOTIFICACIONES_IdEvento");
        modelBuilder.Entity<Notificacion>()
            .HasIndex(notificacion => notificacion.IdProyecto)
            .HasDatabaseName("IX_NOTIFICACIONES_IdProyecto");
        modelBuilder.Entity<Notificacion>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_NOTIFICACIONES_UnaPublicacion",
                "[IdEvento] IS NULL OR [IdProyecto] IS NULL"));
    }
}
