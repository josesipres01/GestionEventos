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

        await InicializadorBaseDatos.AdoptarEsquemaExistenteAsync(db);
        await db.Database.MigrateAsync();
        await InicializadorBaseDatos.PrepararDatosSemillaAsync(db);

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
        app.Logger.LogError(exception, "No se pudo migrar la base de datos de Gestión de Eventos.");
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
