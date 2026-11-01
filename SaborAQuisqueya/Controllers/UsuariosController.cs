using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Data;
using SaborAQuisqueya.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SaborAQuisqueya.Controllers
{
    public class UsuariosController : Controller
    {
        // ==============================================
        // DEPENDENCIAS
        // ==============================================

        private readonly ApplicationDbContext _context;

        private readonly PasswordHasher<Usuario> _hasher = new();

        public UsuariosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==============================================
        // REGISTRO - GET
        // ==============================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Registro()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirigirSegunRol();
            }

            return View();
        }

        // ==============================================
        // REGISTRO - POST
        // ==============================================

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(
            string nombre,
            string correo,
            string telefono,
            string contrasena)
        {
            // Normalizar los datos
            nombre = nombre?.Trim() ?? "";
            correo = correo?.Trim().ToLowerInvariant() ?? "";
            telefono = telefono?.Trim() ?? "";

            // Validar campos obligatorios
            if (string.IsNullOrWhiteSpace(nombre) ||
                string.IsNullOrWhiteSpace(correo) ||
                string.IsNullOrWhiteSpace(telefono) ||
                string.IsNullOrWhiteSpace(contrasena))
            {
                ModelState.AddModelError(
                    "",
                    "Todos los campos son obligatorios."
                );

                return View();
            }

            // Validar longitud de los campos
            if (nombre.Length > 100 ||
                correo.Length > 150 ||
                telefono.Length > 20)
            {
                ModelState.AddModelError(
                    "",
                    "Uno o varios campos superan la longitud permitida."
                );

                return View();
            }

            // Validar correo electrónico
            var emailValidator = new EmailAddressAttribute();

            if (!emailValidator.IsValid(correo))
            {
                ModelState.AddModelError(
                    "",
                    "Ingrese un correo electrónico válido."
                );

                return View();
            }

            // Validar contraseña
            if (contrasena.Length < 8)
            {
                ModelState.AddModelError(
                    "",
                    "La contraseña debe tener al menos 8 caracteres."
                );

                return View();
            }

            // Comprobar correo duplicado
            bool correoExiste = await _context.Usuarios
                .AnyAsync(u => u.Correo == correo);

            if (correoExiste)
            {
                ModelState.AddModelError(
                    "",
                    "Este correo electrónico ya está registrado."
                );

                return View();
            }

            // Crear usuario con rol Cliente
            var usuario = new Usuario
            {
                Nombre = nombre,
                Correo = correo,
                Telefono = telefono,

                // El registro público nunca crea administradores
                Rol = "Cliente",

                Activo = true,
                FechaRegistro = DateTime.Now
            };

            // Proteger contraseña
            usuario.ContrasenaHash = _hasher.HashPassword(
                usuario,
                contrasena
            );

            // Guardar usuario
            _context.Usuarios.Add(usuario);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible completar el registro. " +
                    "Intente nuevamente."
                );

                return View();
            }

            // Confirmación
            TempData["Mensaje"] =
                "Cuenta creada correctamente. Ya puedes iniciar sesión.";

            return RedirectToAction(nameof(Login));
        }

        // ==============================================
        // INICIO DE SESIÓN - GET
        // ==============================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirigirSegunRol(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;

            return View();
        }

        // ==============================================
        // INICIO DE SESIÓN - POST
        // ==============================================

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string correo,
            string contrasena,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Validar campos
            if (string.IsNullOrWhiteSpace(correo) ||
                string.IsNullOrWhiteSpace(contrasena))
            {
                ModelState.AddModelError(
                    "",
                    "Debe ingresar su correo y contraseña."
                );

                return View();
            }

            // Normalizar correo
            string email = correo.Trim().ToLowerInvariant();

            // Buscar usuario
            var usuario = await _context.Usuarios
                .SingleOrDefaultAsync(u => u.Correo == email);

            // Validar cuenta
            if (usuario == null || !usuario.Activo)
            {
                ModelState.AddModelError(
                    "",
                    "Correo o contraseña incorrectos, o cuenta inactiva."
                );

                return View();
            }

            // Comprobar contraseña
            var resultado = _hasher.VerifyHashedPassword(
                usuario,
                usuario.ContrasenaHash,
                contrasena
            );

            if (resultado == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    "",
                    "Correo o contraseña incorrectos, o cuenta inactiva."
                );

                return View();
            }

            // ==============================================
            // CREAR IDENTIDAD
            // ==============================================

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    usuario.Id.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    usuario.Nombre
                ),

                new Claim(
                    ClaimTypes.Email,
                    usuario.Correo
                ),

                new Claim(
                    ClaimTypes.Role,
                    usuario.Rol
                )
            };

            var identidad = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var principal = new ClaimsPrincipal(identidad);

            // Crear sesión
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal
            );

            // ==============================================
            // REDIRECCIÓN SEGÚN EL ROL
            // ==============================================

            return RedirigirSegunRol(returnUrl, usuario.Rol);
        }

        // ==============================================
        // PERFIL DEL USUARIO - GET
        // ==============================================

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Perfil()
        {
            var id = ObtenerUsuarioActualId();

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
            {
                return NotFound();
            }

            return View(usuario);
        }

        // ==============================================
        // ACTUALIZAR PERFIL - POST
        // ==============================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Perfil(
            string nombre,
            string telefono)
        {
            var id = ObtenerUsuarioActualId();

            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
            {
                return NotFound();
            }

            // Validar datos
            if (string.IsNullOrWhiteSpace(nombre) ||
                string.IsNullOrWhiteSpace(telefono))
            {
                ModelState.AddModelError(
                    "",
                    "El nombre y el teléfono son obligatorios."
                );

                return View(usuario);
            }

            nombre = nombre.Trim();
            telefono = telefono.Trim();

            if (nombre.Length > 100 || telefono.Length > 20)
            {
                ModelState.AddModelError(
                    "",
                    "Los datos superan la longitud permitida."
                );

                return View(usuario);
            }

            // Actualizar información
            usuario.Nombre = nombre;
            usuario.Telefono = telefono;

            await _context.SaveChangesAsync();

            // Actualizar la identidad de la sesión
            await CrearSesionActualizada(usuario);

            TempData["Mensaje"] =
                "Perfil actualizado correctamente.";

            return RedirectToAction(nameof(Perfil));
        }

        // ==============================================
        // ADMINISTRADOR - LISTADO DE USUARIOS
        // ==============================================

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            return View(usuarios);
        }

        // ==============================================
        // ADMINISTRADOR - ACTIVAR / DESACTIVAR USUARIO
        // ==============================================

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarActivo(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
            {
                return NotFound();
            }

            // Impedir que el administrador se desactive
            if (usuario.Id == ObtenerUsuarioActualId())
            {
                TempData["Mensaje"] =
                    "No puedes desactivar tu propia cuenta.";

                return RedirectToAction(nameof(Index));
            }

            usuario.Activo = !usuario.Activo;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                "Estado del usuario actualizado correctamente.";

            return RedirectToAction(nameof(Index));
        }

        // ==============================================
        // CERRAR SESIÓN
        // ==============================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            return RedirectToAction("Index", "Home");
        }

        // ==============================================
        // ACCESO DENEGADO
        // ==============================================

        [HttpGet]
        public IActionResult AccesoDenegado()
        {
            return View();
        }

        // ==============================================
        // MÉTODOS PRIVADOS
        // ==============================================

        // Obtener el ID del usuario autenticado
        private int ObtenerUsuarioActualId()
        {
            return int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );
        }

        // Redirigir al usuario según su rol
        private IActionResult RedirigirSegunRol(
            string? returnUrl = null,
            string? rol = null)
        {
            rol ??= User.FindFirstValue(ClaimTypes.Role);

            // El administrador utiliza una interfaz independiente
            if (rol == "Administrador")
            {
                return RedirectToAction(
                    "Administrar",
                    "Reservas"
                );
            }

            // Permitir al cliente volver a su reserva
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl) &&
                returnUrl.StartsWith(
                    "/Reservas/Crear",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return LocalRedirect(returnUrl);
            }

            // Página principal del restaurante
            return RedirectToAction(
                "Index",
                "Home"
            );
        }

        // Actualizar los datos de la sesión
        private async Task CrearSesionActualizada(Usuario usuario)
        {
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    usuario.Id.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    usuario.Nombre
                ),

                new Claim(
                    ClaimTypes.Email,
                    usuario.Correo
                ),

                new Claim(
                    ClaimTypes.Role,
                    usuario.Rol
                )
            };

            var identidad = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidad)
            );
        }
    }
}