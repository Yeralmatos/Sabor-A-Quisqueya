using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SaborAQuisqueya.Data;

namespace SaborAQuisqueya.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ========================================
        // DASHBOARD PRINCIPAL DEL ADMINISTRADOR
        // ========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Total de reservaciones
            ViewBag.TotalReservas = await _context.Reservas.CountAsync();

            // Reservaciones pendientes
            ViewBag.ReservasPendientes = await _context.Reservas
                .CountAsync(r => r.Estado == "Pendiente");

            // Reservaciones aceptadas
            ViewBag.ReservasAceptadas = await _context.Reservas
                .CountAsync(r => r.Estado == "Aceptada");

            // Reservaciones rechazadas
            ViewBag.ReservasRechazadas = await _context.Reservas
                .CountAsync(r => r.Estado == "Rechazada");

            // Reservaciones canceladas
            ViewBag.ReservasCanceladas = await _context.Reservas
                .CountAsync(r => r.Estado == "Cancelada");

            // Total de mesas activas
            ViewBag.TotalMesas = await _context.Mesas
                .CountAsync(m => m.Activa);

            // Total de clientes registrados
            ViewBag.TotalClientes = await _context.Usuarios
                .CountAsync(u => u.Rol == "Cliente");

            // Próximas reservaciones
            var proximasReservas = await _context.Reservas
                .AsNoTracking()
                .Include(r => r.Usuario)
                .Include(r => r.Mesa)
                .Where(r =>
                    r.FechaReserva >= DateTime.Today &&
                    (r.Estado == "Pendiente" || r.Estado == "Aceptada"))
                .OrderBy(r => r.FechaReserva)
                .ThenBy(r => r.HoraReserva)
                .Take(10)
                .ToListAsync();

            return View(proximasReservas);
        }
    }
}