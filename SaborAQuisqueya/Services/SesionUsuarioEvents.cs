using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Data;
using SaborAQuisqueya.Models;

namespace SaborAQuisqueya.Services;

// Un cambio de contraseña invalida también las sesiones abiertas en otros equipos.
public sealed class SesionUsuarioEvents(ApplicationDbContext context) : CookieAuthenticationEvents
{
    public const string VersionClaim = "ContrasenaVersion";

    public static string ObtenerVersion(Usuario usuario) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(usuario.ContrasenaHash)));

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext cookieContext)
    {
        var principal = cookieContext.Principal;
        bool idValido = int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out int id);
        var usuario = idValido
            ? await context.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id)
            : null;

        if (usuario == null || !usuario.Activo ||
            principal?.FindFirstValue(VersionClaim) != ObtenerVersion(usuario))
        {
            cookieContext.RejectPrincipal();
            await cookieContext.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
