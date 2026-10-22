using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Data;
using SaborAQuisqueya.Models;

namespace SaborAQuisqueya.Controllers;

public class UsuariosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher<Usuario> _hasher = new();

    public UsuariosController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public IActionResult Registro() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registro(string nombre, string correo, string telefono, string contrasena)
    {
        nombre = nombre?.Trim() ?? "";
        correo = correo?.Trim().ToLowerInvariant() ?? "";
        telefono = telefono?.Trim() ?? "";

        if (nombre.Length == 0 || correo.Length == 0 || telefono.Length == 0 || string.IsNullOrWhiteSpace(contrasena))
        {
            ModelState.AddModelError("", "Completa todos los campos.");
            return View();
        }
        if (contrasena.Length < 8)
        {
            ModelState.AddModelError("", "La contraseña debe tener al menos 8 caracteres.");
            return View();
        }
        if (await _context.Usuarios.AnyAsync(u => u.Correo == correo))
        {
            ModelState.AddModelError("", "Ese correo ya está registrado.");
            return View();
        }

        var usuario = new Usuario
        {
            Nombre = nombre,
            Correo = correo,
            Telefono = telefono,
            Rol = "Cliente",
            Activo = true
        };
        usuario.ContrasenaHash = _hasher.HashPassword(usuario, contrasena);
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        TempData["Mensaje"] = "Cuenta creada. Ya puedes iniciar sesión.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string correo, string contrasena)
    {
        var email = correo?.Trim().ToLowerInvariant() ?? "";
        var usuario = await _context.Usuarios.SingleOrDefaultAsync(u => u.Correo == email);
        if (usuario == null || !usuario.Activo || string.IsNullOrEmpty(contrasena) ||
            _hasher.VerifyHashedPassword(usuario, usuario.ContrasenaHash, contrasena) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Correo o contraseña incorrectos, o cuenta inactiva.");
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Email, usuario.Correo),
            new(ClaimTypes.Role, usuario.Rol)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Perfil()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return usuario == null ? NotFound() : View(usuario);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Perfil(string nombre, string telefono)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(telefono))
        {
            ModelState.AddModelError("", "El nombre y el teléfono son obligatorios.");
            return View(usuario);
        }
        usuario.Nombre = nombre.Trim();
        usuario.Telefono = telefono.Trim();
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Email, usuario.Correo),
            new(ClaimTypes.Role, usuario.Rol)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        TempData["Mensaje"] = "Perfil actualizado correctamente.";
        return RedirectToAction(nameof(Perfil));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Index()
    {
        var usuarios = await _context.Usuarios.AsNoTracking().OrderBy(u => u.Nombre).ToListAsync();
        return View(usuarios);
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarActivo(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();
        if (usuario.Id == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
        {
            TempData["Mensaje"] = "No puedes desactivar tu propia cuenta desde esta pantalla.";
            return RedirectToAction(nameof(Index));
        }
        usuario.Activo = !usuario.Activo;
        await _context.SaveChangesAsync();
        TempData["Mensaje"] = "Estado del usuario actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccesoDenegado() => View();
}
