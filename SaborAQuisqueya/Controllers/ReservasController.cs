using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Data;
using SaborAQuisqueya.Models;

namespace SaborAQuisqueya.Controllers;

[Authorize]
public class ReservasController : Controller
{
    private readonly ApplicationDbContext _context;
    private static readonly TimeSpan DuracionReserva = TimeSpan.FromHours(2);

    public ReservasController(ApplicationDbContext context) => _context = context;
    private int UsuarioActualId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> MisReservas()
    {
        var reservas = await _context.Reservas.AsNoTracking()
            .Include(r => r.Restaurante).Include(r => r.Mesa)
            .Where(r => r.UsuarioId == UsuarioActualId)
            .OrderByDescending(r => r.FechaReserva).ThenByDescending(r => r.HoraReserva)
            .ToListAsync();
        return View(reservas);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        await CargarRestaurantes();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(int restauranteId, DateTime fechaReserva, TimeSpan horaReserva,
        int cantidadPersonas, string? observaciones)
    {
        if (fechaReserva.Date < DateTime.Today || horaReserva < TimeSpan.Zero || horaReserva >= TimeSpan.FromDays(1))
            ModelState.AddModelError("", "La fecha o la hora no son válidas.");
        if (cantidadPersonas < 1 || cantidadPersonas > 50)
            ModelState.AddModelError("", "La cantidad debe estar entre 1 y 50 personas.");
        if (observaciones?.Length > 500)
            ModelState.AddModelError("", "Las observaciones no pueden superar 500 caracteres.");

        var restaurante = await _context.Restaurantes.FirstOrDefaultAsync(r => r.Id == restauranteId && r.Activo);
        if (restaurante == null) ModelState.AddModelError("", "Selecciona un restaurante activo.");
        if (!ModelState.IsValid)
        {
            await CargarRestaurantes();
            return View();
        }

        var horaFin = horaReserva + DuracionReserva;
        if (horaFin > TimeSpan.FromDays(1))
        {
            ModelState.AddModelError("", "La reserva no puede terminar después de la medianoche.");
            await CargarRestaurantes();
            return View();
        }

        // Validar horario de atención cuando el restaurante tiene horarios configurados.
        var horario = await _context.Horarios.FirstOrDefaultAsync(h =>
            h.RestauranteId == restauranteId && h.DiaSemana == fechaReserva.DayOfWeek && h.Activo);
        if (horario != null && (horaReserva < horario.HoraApertura || horaFin > horario.HoraCierre))
        {
            ModelState.AddModelError("", "La hora seleccionada está fuera del horario de atención.");
            await CargarRestaurantes();
            return View();
        }

        var reservasDelDia = await _context.Reservas
            .Where(r => r.RestauranteId == restauranteId && r.FechaReserva == fechaReserva.Date &&
                        r.Estado != "Cancelada" && r.Estado != "Rechazada" && r.MesaId != null)
            .ToListAsync();
        var mesas = await _context.Mesas
            .Where(m => m.RestauranteId == restauranteId && m.Activa && m.Capacidad >= cantidadPersonas)
            .OrderBy(m => m.Capacidad).ToListAsync();
        var mesa = mesas.FirstOrDefault(m => !reservasDelDia.Any(r => r.MesaId == m.Id &&
            r.HoraReserva < horaFin && r.HoraReserva + DuracionReserva > horaReserva));

        if (mesa == null)
        {
            ModelState.AddModelError("", "No hay mesas disponibles para esa fecha, hora y cantidad de personas.");
            await CargarRestaurantes();
            return View();
        }

        var reserva = new Reserva
        {
            UsuarioId = UsuarioActualId, RestauranteId = restauranteId, MesaId = mesa.Id,
            FechaReserva = fechaReserva.Date, HoraReserva = horaReserva,
            CantidadPersonas = cantidadPersonas, Observaciones = observaciones?.Trim(),
            Estado = "Pendiente", FechaRegistro = DateTime.Now
        };
        _context.Reservas.Add(reserva);
        await _context.SaveChangesAsync();
        TempData["Mensaje"] = "Solicitud de reserva registrada correctamente.";
        return RedirectToAction(nameof(MisReservas));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var reserva = await _context.Reservas.FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == UsuarioActualId);
        if (reserva == null) return NotFound();
        if (reserva.Estado != "Pendiente" && reserva.Estado != "Aceptada")
        {
            TempData["Mensaje"] = "Esta reserva no se puede cancelar en su estado actual.";
            return RedirectToAction(nameof(MisReservas));
        }
        reserva.Estado = "Cancelada";
        await _context.SaveChangesAsync();
        TempData["Mensaje"] = "Reserva cancelada correctamente.";
        return RedirectToAction(nameof(MisReservas));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Administrar()
    {
        var reservas = await _context.Reservas.AsNoTracking()
            .Include(r => r.Usuario).Include(r => r.Restaurante).Include(r => r.Mesa)
            .OrderBy(r => r.FechaReserva).ThenBy(r => r.HoraReserva).ToListAsync();
        return View(reservas);
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, string estado)
    {
        if (estado is not ("Aceptada" or "Rechazada" or "Cancelada")) return BadRequest("Estado no válido.");
        var reserva = await _context.Reservas.FindAsync(id);
        if (reserva == null) return NotFound();
        if (reserva.Estado != "Pendiente")
        {
            TempData["Mensaje"] = "Solo se pueden cambiar reservas pendientes.";
            return RedirectToAction(nameof(Administrar));
        }
        reserva.Estado = estado;
        await _context.SaveChangesAsync();
        TempData["Mensaje"] = $"Reserva actualizada: {estado}.";
        return RedirectToAction(nameof(Administrar));
    }

    private async Task CargarRestaurantes()
    {
        ViewBag.Restaurantes = await _context.Restaurantes.Where(r => r.Activo)
            .OrderBy(r => r.Nombre).ToListAsync();
    }
}
