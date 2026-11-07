using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Data;
using SaborAQuisqueya.Models;

namespace SaborAQuisqueya.Services;

public class RecuperacionContrasenaService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _email;
    private readonly TimeProvider _time;
    private readonly ILogger<RecuperacionContrasenaService> _logger;
    private readonly PasswordHasher<RecuperacionContrasena> _codeHasher = new();
    private readonly PasswordHasher<Usuario> _passwordHasher = new();

    public RecuperacionContrasenaService(ApplicationDbContext context, IEmailService email,
        TimeProvider time, ILogger<RecuperacionContrasenaService> logger)
    {
        _context = context;
        _email = email;
        _time = time;
        _logger = logger;
    }

    public async Task SolicitarCodigoAsync(string correo)
    {
        correo = NormalizarCorreo(correo);
        if (correo.Length is 0 or > 150)
            return;

        var usuario = await _context.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Correo == correo && u.Activo);
        if (usuario == null)
            return;

        var ahora = _time.GetUtcNow().UtcDateTime;
        var recuperacion = await _context.RecuperacionesContrasena
            .SingleOrDefaultAsync(r => r.UsuarioId == usuario.Id);
        if (recuperacion != null && (recuperacion.ProximoEnvioUtc > ahora ||
            recuperacion.BloqueadoHastaUtc > ahora))
            return;

        if (recuperacion == null)
        {
            recuperacion = new RecuperacionContrasena { UsuarioId = usuario.Id };
            _context.RecuperacionesContrasena.Add(recuperacion);
        }
        else if (recuperacion.ExpiraUtc <= ahora)
        {
            recuperacion.IntentosFallidos = 0;
            recuperacion.BloqueadoHastaUtc = null;
        }

        string codigo = RandomNumberGenerator.GetInt32(1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
        recuperacion.CodigoHash = _codeHasher.HashPassword(recuperacion, codigo);
        recuperacion.ExpiraUtc = ahora.AddMinutes(10);
        recuperacion.ProximoEnvioUtc = ahora.AddSeconds(60);
        recuperacion.Version = Guid.NewGuid();
        string hashEnviado = recuperacion.CodigoHash;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // La PK única y Version impiden que dos solicitudes envíen códigos distintos.
            _context.Entry(recuperacion).State = EntityState.Detached;
            _logger.LogWarning(ex, "No se pudo guardar la solicitud de recuperación.");
            return;
        }

        try
        {
            await _email.EnviarCorreoAsync(usuario.Correo,
                "Código para recuperar tu contraseña - Sabor a Quisqueya",
                $"Tu código de recuperación es: {codigo}\n\n" +
                "Es válido durante 10 minutos y solo se puede utilizar una vez.\n" +
                "Ingresa este código en la pantalla de recuperación para crear una nueva contraseña.\n\n" +
                "Si no solicitaste este cambio, ignora este correo y no compartas el código.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible enviar el código de recuperación.");
            await InvalidarEnvioFallidoAsync(recuperacion, hashEnviado);
        }
    }

    public async Task<bool> RestablecerAsync(string correo, string codigo, string nuevaContrasena)
    {
        correo = NormalizarCorreo(correo);
        if (correo.Length is 0 or > 150 || string.IsNullOrEmpty(codigo) ||
            codigo.Length != 6 || codigo.Any(c => c < '0' || c > '9') ||
            string.IsNullOrEmpty(nuevaContrasena) || nuevaContrasena.Length is < 8 or > 128)
            return false;

        // Reintentar conflictos permite contar intentos paralelos sobre la versión vigente.
        for (int intento = 0; intento < 3; intento++)
        {
            var recuperacion = await _context.RecuperacionesContrasena
                .Include(r => r.Usuario)
                .SingleOrDefaultAsync(r => r.Usuario.Correo == correo && r.Usuario.Activo);
            var ahora = _time.GetUtcNow().UtcDateTime;
            if (recuperacion == null || recuperacion.ExpiraUtc <= ahora ||
                recuperacion.BloqueadoHastaUtc > ahora || recuperacion.IntentosFallidos >= 5 ||
                string.IsNullOrEmpty(recuperacion.CodigoHash))
                return false;

            var usuario = recuperacion.Usuario;
            bool correcto = _codeHasher.VerifyHashedPassword(recuperacion,
                recuperacion.CodigoHash, codigo) != PasswordVerificationResult.Failed;

            if (!correcto)
            {
                recuperacion.IntentosFallidos++;
                if (recuperacion.IntentosFallidos >= 5)
                    recuperacion.BloqueadoHastaUtc = ahora.AddMinutes(15);
                recuperacion.Version = Guid.NewGuid();
            }
            else
            {
                usuario.ContrasenaHash = _passwordHasher.HashPassword(usuario, nuevaContrasena);
                _context.RecuperacionesContrasena.Remove(recuperacion);
            }

            try
            {
                // EF guarda el cambio de contraseña y el consumo en una sola transacción.
                await _context.SaveChangesAsync();
                return correcto;
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.Entry(recuperacion).State = EntityState.Detached;
                _context.Entry(usuario).State = EntityState.Detached;
            }
        }

        return false;
    }
    
    private async Task InvalidarEnvioFallidoAsync(RecuperacionContrasena recuperacion, string hashEnviado)
    {
        for (int intento = 0; intento < 3; intento++)
        {
            await _context.Entry(recuperacion).ReloadAsync();
            if (_context.Entry(recuperacion).State == EntityState.Detached ||
                recuperacion.CodigoHash != hashEnviado)
                return;

            recuperacion.CodigoHash = string.Empty;
            recuperacion.ExpiraUtc = _time.GetUtcNow().UtcDateTime;
            recuperacion.Version = Guid.NewGuid();
            try
            {
                await _context.SaveChangesAsync();
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                // No invalidar el código de un reenvío que haya ganado la carrera.
            }
        }
    }

    private static string NormalizarCorreo(string? correo) =>
        correo?.Trim().ToLowerInvariant() ?? string.Empty;
}
