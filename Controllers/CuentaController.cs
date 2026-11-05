using System.Security.Claims;
using GestionEventos.Data;
using GestionEventos.Models;
using GestionEventos.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GestionEventos.Controllers;

public class CuentaController(
    GestionEventosDbContext dbContext,
    IPasswordHasher<Usuario> passwordHasher,
    IMemoryCache memoryCache) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Registrar()
    {
        await CargarCarrerasAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(RegistroUsuarioViewModel modelo)
    {
        var correoNormalizado = modelo.Correo?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(correoNormalizado) &&
            await dbContext.Usuarios.AsNoTracking().AnyAsync(usuario => usuario.Correo == correoNormalizado))
        {
            ModelState.AddModelError(nameof(modelo.Correo), "Ya existe una cuenta con ese correo.");
        }

        var carreras = await ObtenerCarrerasCacheAsync();
        if (modelo.IdCarrera.HasValue &&
            !carreras.Any(carrera => carrera.IdCarrera == modelo.IdCarrera.Value))
        {
            ModelState.AddModelError(nameof(modelo.IdCarrera), "Selecciona una carrera válida.");
        }

        if (!ModelState.IsValid)
        {
            await CargarCarrerasAsync();
            return View(modelo);
        }

        var rolEstudiante = await dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(rol => rol.IdRol == 1)
            ?? throw new InvalidOperationException("No existe el rol Estudiante. Ejecuta el script documentado de inicialización.");

        var usuario = new Usuario
        {
            Nombres = modelo.Nombres.Trim(),
            ApellidoPaterno = modelo.ApellidoPaterno.Trim(),
            ApellidoMaterno = modelo.ApellidoMaterno?.Trim(),
            Correo = correoNormalizado,
            IdRol = rolEstudiante.IdRol,
            IdCarrera = modelo.IdCarrera,
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, modelo.Contrasena);

        dbContext.Usuarios.Add(usuario);
        await dbContext.SaveChangesAsync();

        usuario.Rol = rolEstudiante;
        await IniciarSesionAsync(usuario);
        return RedirectToAction("Index", "Eventos");
    }

    [HttpGet]
    public IActionResult IniciarSesion(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IniciarSesion(IniciarSesionViewModel modelo, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var correo = modelo.Correo.Trim().ToLowerInvariant();
        var usuario = await dbContext.Usuarios
            .AsNoTracking()
            .Include(item => item.Rol)
            .SingleOrDefaultAsync(item => item.Correo == correo && item.Activo);

        if (usuario is null ||
            passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, modelo.Contrasena)
                == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(modelo);
        }

        await IniciarSesionAsync(usuario);

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction("Index", "Eventos");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarSesion()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(IniciarSesion));
    }

    public IActionResult AccesoDenegado() => View();

    private async Task<List<Carrera>> ObtenerCarrerasCacheAsync()
    {
        return await memoryCache.GetOrCreateAsync("Catalogo_Carreras", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await dbContext.Carreras
                .AsNoTracking()
                .OrderBy(carrera => carrera.Nombre)
                .ToListAsync();
        }) ?? new List<Carrera>();
    }

    private async Task CargarCarrerasAsync()
    {
        ViewBag.Carreras = new SelectList(
            await ObtenerCarrerasCacheAsync(),
            nameof(Carrera.IdCarrera),
            nameof(Carrera.Nombre));
    }

    private async Task IniciarSesionAsync(Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, $"{usuario.Nombres} {usuario.ApellidoPaterno}"),
            new(ClaimTypes.Email, usuario.Correo),
            new(ClaimTypes.Role, usuario.Rol.Nombre)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
