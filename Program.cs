using GestionEventos.Data;
using GestionEventos.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddDbContextPool<GestionEventosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GestionEventos")));
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Cuenta/IniciarSesion";
        options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<GestionEventosDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.EVENTOS', 'ImagenUrl') IS NULL
            BEGIN
                ALTER TABLE EVENTOS ADD ImagenUrl NVARCHAR(500) NULL;
            END

            IF COL_LENGTH('dbo.EVENTOS', 'IdUsuarioSolicitante') IS NULL
            BEGIN
                ALTER TABLE EVENTOS ADD IdUsuarioSolicitante INT NULL;
            END

            IF COL_LENGTH('dbo.EVENTOS', 'EstadoAprobacion') IS NULL
            BEGIN
                ALTER TABLE dbo.EVENTOS ADD EstadoAprobacion NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_EVENTOS_EstadoAprobacion DEFAULT N'Aprobado';
            END

            IF COL_LENGTH('dbo.EVENTOS', 'ComentarioAprobacion') IS NULL
            BEGIN
                ALTER TABLE dbo.EVENTOS ADD ComentarioAprobacion NVARCHAR(1000) NULL;
            END

            IF COL_LENGTH('dbo.EVENTOS', 'FechaRevision') IS NULL
            BEGIN
                ALTER TABLE dbo.EVENTOS ADD FechaRevision DATETIME2 NULL;
            END

            IF COL_LENGTH('dbo.EVENTOS', 'FechaSolicitud') IS NULL
            BEGIN
                ALTER TABLE dbo.EVENTOS ADD FechaSolicitud DATETIME2 NOT NULL
                    CONSTRAINT DF_EVENTOS_FechaSolicitud DEFAULT CURRENT_TIMESTAMP;
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'ImagenUrl') IS NULL
            BEGIN
                ALTER TABLE PROYECTOS ADD ImagenUrl NVARCHAR(500) NULL;
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'IdUsuarioResponsable') IS NULL
            BEGIN
                ALTER TABLE dbo.PROYECTOS ADD IdUsuarioResponsable INT NULL;
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'EstadoAprobacion') IS NULL
            BEGIN
                ALTER TABLE dbo.PROYECTOS ADD EstadoAprobacion NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_PROYECTOS_EstadoAprobacion DEFAULT N'Aprobado';
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'ComentarioAprobacion') IS NULL
            BEGIN
                ALTER TABLE dbo.PROYECTOS ADD ComentarioAprobacion NVARCHAR(1000) NULL;
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaRevision') IS NULL
            BEGIN
                ALTER TABLE dbo.PROYECTOS ADD FechaRevision DATETIME2 NULL;
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaSolicitud') IS NULL
            BEGIN
                ALTER TABLE dbo.PROYECTOS ADD FechaSolicitud DATETIME2 NOT NULL
                    CONSTRAINT DF_PROYECTOS_FechaSolicitud DEFAULT CURRENT_TIMESTAMP;
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'LugarReunion') IS NULL
            BEGIN
                ALTER TABLE PROYECTOS ADD LugarReunion NVARCHAR(150) NOT NULL CONSTRAINT DF_PROYECTOS_LugarReunion DEFAULT N'Por definir';
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaInicio') IS NULL
            BEGIN
                ALTER TABLE PROYECTOS ADD FechaInicio DATETIME2 NOT NULL CONSTRAINT DF_PROYECTOS_FechaInicio DEFAULT CURRENT_TIMESTAMP;
            END

            IF COL_LENGTH('dbo.PROYECTOS', 'FechaFin') IS NULL
            BEGIN
                ALTER TABLE PROYECTOS ADD FechaFin DATETIME2 NOT NULL CONSTRAINT DF_PROYECTOS_FechaFin DEFAULT DATEADD(MONTH, 1, CURRENT_TIMESTAMP);
            END

            IF OBJECT_ID('dbo.REGISTROS_PROYECTOS', 'U') IS NULL
            BEGIN
                CREATE TABLE REGISTROS_PROYECTOS (
                    IdRegistroProyecto INT IDENTITY(1,1) NOT NULL,
                    IdUsuario INT NOT NULL,
                    IdProyecto INT NOT NULL,
                    FechaRegistro DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT PK_REGISTROS_PROYECTOS PRIMARY KEY (IdRegistroProyecto),
                    CONSTRAINT FK_REGISTROS_PROYECTOS_USUARIOS FOREIGN KEY (IdUsuario) REFERENCES USUARIOS(IdUsuario),
                    CONSTRAINT FK_REGISTROS_PROYECTOS_PROYECTOS FOREIGN KEY (IdProyecto) REFERENCES PROYECTOS(IdProyecto),
                    CONSTRAINT UQ_Usuario_Proyecto UNIQUE (IdUsuario, IdProyecto)
                );
            END

            IF OBJECT_ID('dbo.NOTIFICACIONES', 'U') IS NULL
            BEGIN
                CREATE TABLE NOTIFICACIONES (
                    IdNotificacion INT IDENTITY(1,1) NOT NULL,
                    IdUsuario INT NOT NULL,
                    IdEvento INT NULL,
                    IdProyecto INT NULL,
                    Titulo NVARCHAR(200) NOT NULL,
                    Mensaje NVARCHAR(MAX) NOT NULL,
                    Leida BIT NOT NULL DEFAULT 0,
                    FechaCreacion DATETIME2 NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT PK_NOTIFICACIONES PRIMARY KEY (IdNotificacion),
                    CONSTRAINT FK_NOTIFICACIONES_USUARIOS FOREIGN KEY (IdUsuario) REFERENCES USUARIOS(IdUsuario) ON DELETE CASCADE
                );
            END
            """);

        await InicializadorBaseDatos.PrepararEsquemaAsync(db);

        var correoAdministradorInicial = builder.Configuration["BootstrapUsers:AdminEmail"];
        var contrasenaAdministradorInicial = builder.Configuration["BootstrapUsers:AdminPassword"];
        if (await db.Roles.AsNoTracking().AnyAsync(r => r.IdRol == 3) &&
            !await db.Usuarios.AsNoTracking().AnyAsync(u => u.IdRol == 3))
        {
            if (string.IsNullOrWhiteSpace(correoAdministradorInicial) &&
                string.IsNullOrWhiteSpace(contrasenaAdministradorInicial))
            {
                app.Logger.LogWarning(
                    "No existe una cuenta de administrador. Configura BootstrapUsers__AdminEmail y BootstrapUsers__AdminPassword para crearla.");
            }
            else if (string.IsNullOrWhiteSpace(correoAdministradorInicial) ||
                     string.IsNullOrWhiteSpace(contrasenaAdministradorInicial))
            {
                throw new InvalidOperationException(
                    "Configura tanto BootstrapUsers__AdminEmail como BootstrapUsers__AdminPassword para crear la cuenta inicial de administrador.");
            }
            else
            {
                var admin = new Usuario
                {
                    Nombres = "Coordinación",
                    ApellidoPaterno = "General",
                    ApellidoMaterno = "UNISON",
                    Correo = correoAdministradorInicial.Trim(),
                    IdRol = 3,
                    Activo = true,
                    FechaRegistro = DateTime.UtcNow
                };
                admin.PasswordHash = passwordHasher.HashPassword(admin, contrasenaAdministradorInicial);
                db.Usuarios.Add(admin);
                await db.SaveChangesAsync();
            }
        }

        var correoOrganizadorInicial = builder.Configuration["BootstrapUsers:OrganizerEmail"];
        var contrasenaOrganizadorInicial = builder.Configuration["BootstrapUsers:OrganizerPassword"];
        if (await db.Roles.AsNoTracking().AnyAsync(r => r.IdRol == 2) &&
            !await db.Usuarios.AsNoTracking().AnyAsync(u => u.IdRol == 2))
        {
            if (string.IsNullOrWhiteSpace(correoOrganizadorInicial) &&
                string.IsNullOrWhiteSpace(contrasenaOrganizadorInicial))
            {
                app.Logger.LogWarning(
                    "No existe una cuenta de organizador. Configura BootstrapUsers__OrganizerEmail y BootstrapUsers__OrganizerPassword para crearla.");
            }
            else if (string.IsNullOrWhiteSpace(correoOrganizadorInicial) ||
                     string.IsNullOrWhiteSpace(contrasenaOrganizadorInicial))
            {
                throw new InvalidOperationException(
                    "Configura tanto BootstrapUsers__OrganizerEmail como BootstrapUsers__OrganizerPassword para crear la cuenta inicial de organizador.");
            }
            else
            {
                var organizador = new Usuario
                {
                    Nombres = "Organizador",
                    ApellidoPaterno = "Inicial",
                    ApellidoMaterno = "UNISON",
                    Correo = correoOrganizadorInicial.Trim(),
                    IdRol = 2,
                    Activo = true,
                    FechaRegistro = DateTime.UtcNow
                };
                organizador.PasswordHash = passwordHasher.HashPassword(organizador, contrasenaOrganizadorInicial);
                db.Usuarios.Add(organizador);
                await db.SaveChangesAsync();
            }
        }
    }
    catch (SqlException exception)
    {
        app.Logger.LogError(exception, "No se pudo preparar la base de datos de Gestión de Eventos.");
        throw;
    }
    catch (InvalidOperationException exception)
    {
        app.Logger.LogError(exception, "No se pudo inicializar la base de datos de Gestión de Eventos.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/Home/NoEncontrado");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Eventos}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
