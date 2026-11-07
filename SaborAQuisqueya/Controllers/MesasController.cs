using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Data;

namespace SaborAQuisqueya.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class MesasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MesasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ======================================
        // LISTAR MESAS DE SABOR A QUISQUEYA
        // ======================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var restaurante = await _context.Restaurantes
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.Nombre == "Sabor a Quisqueya");

            if (restaurante == null)
            {
                TempData["Mensaje"] =
                    "No se encontró Sabor a Quisqueya en la base de datos.";

                return RedirectToAction("Index", "Admin");
            }

            var mesas = await _context.Mesas
                .AsNoTracking()
                .Where(m => m.RestauranteId == restaurante.Id)
                .OrderBy(m => m.Numero)
                .ToListAsync();

            ViewBag.TotalMesas = mesas.Count;
            ViewBag.MesasActivas = mesas.Count(m => m.Activa);
            ViewBag.MesasInactivas = mesas.Count(m => !m.Activa);
            ViewBag.CapacidadTotal = mesas
                .Where(m => m.Activa)
                .Sum(m => m.Capacidad);

            return View(mesas);
        }

        // ======================================
        // REGISTRAR NUEVA MESA
        // ======================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(int numero, int capacidad)
        {
            if (numero < 1 || numero > 1000)
            {
                TempData["Mensaje"] =
                    "El número de mesa debe estar entre 1 y 1000.";

                return RedirectToAction(nameof(Index));
            }

            if (capacidad < 1 || capacidad > 8)
            {
                TempData["Mensaje"] =
                    "La capacidad máxima permitida es de 8 personas.";

                return RedirectToAction(nameof(Index));
            }

            var restaurante = await _context.Restaurantes
                .FirstOrDefaultAsync(r =>
                    r.Nombre == "Sabor a Quisqueya");

            if (restaurante == null)
            {
                return NotFound();
            }

            bool existeMesa = await _context.Mesas.AnyAsync(m =>
                m.RestauranteId == restaurante.Id &&
                m.Numero == numero);

            if (existeMesa)
            {
                TempData["Mensaje"] =
                    $"Ya existe una mesa con el número {numero}.";

                return RedirectToAction(nameof(Index));
            }

            var mesa = new SaborAQuisqueya.Models.Mesa
            {
                RestauranteId = restaurante.Id,
                Numero = numero,
                Capacidad = capacidad,
                Estado = "Disponible",
                Activa = true
            };

            _context.Mesas.Add(mesa);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                $"Mesa {numero} registrada correctamente.";

            return RedirectToAction(nameof(Index));
        }

        // ======================================
        // MODIFICAR MESA
        // ======================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int id,
            int numero,
            int capacidad)
        {
            var mesa = await _context.Mesas
                .Include(m => m.Restaurante)
                .FirstOrDefaultAsync(m => m.Id == id &&
                    m.Restaurante.Nombre == "Sabor a Quisqueya");

            if (mesa == null)
            {
                return NotFound();
            }

            if (numero < 1 || numero > 1000 ||
                capacidad < 1 || capacidad > 8)
            {
                TempData["Mensaje"] =
                    "Verifica el número y la capacidad de la mesa.";

                return RedirectToAction(nameof(Index));
            }

            bool numeroDuplicado = await _context.Mesas.AnyAsync(m =>
                m.RestauranteId == mesa.RestauranteId &&
                m.Numero == numero &&
                m.Id != id);

            if (numeroDuplicado)
            {
                TempData["Mensaje"] =
                    $"Ya existe otra mesa con el número {numero}.";

                return RedirectToAction(nameof(Index));
            }

            // No permitir reducir la capacidad por debajo
            // de una reservación pendiente o aceptada.
            bool tieneReservasIncompatibles =
                await _context.Reservas.AnyAsync(r =>
                    r.MesaId == mesa.Id &&
                    r.FechaReserva >= DateTime.Today &&
                    (r.Estado == "Pendiente" ||
                     r.Estado == "Aceptada") &&
                    r.CantidadPersonas > capacidad);

            if (tieneReservasIncompatibles)
            {
                TempData["Mensaje"] =
                    "No puedes reducir la capacidad porque existen reservaciones futuras que requieren más asientos.";

                return RedirectToAction(nameof(Index));
            }

            mesa.Numero = numero;
            mesa.Capacidad = capacidad;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                $"Mesa {numero} actualizada correctamente.";

            return RedirectToAction(nameof(Index));
        }

        // ======================================
        // ACTIVAR O DESACTIVAR MESA
        // ======================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id)
        {
            var mesa = await _context.Mesas
                .Include(m => m.Restaurante)
                .FirstOrDefaultAsync(m => m.Id == id &&
                    m.Restaurante.Nombre == "Sabor a Quisqueya");

            if (mesa == null)
            {
                return NotFound();
            }

            // No desactivar mesas con reservas futuras
            // pendientes o aceptadas.
            if (mesa.Activa)
            {
                bool tieneReservasFuturas =
                    await _context.Reservas.AnyAsync(r =>
                        r.MesaId == mesa.Id &&
                        r.FechaReserva >= DateTime.Today &&
                        (r.Estado == "Pendiente" ||
                         r.Estado == "Aceptada"));

                if (tieneReservasFuturas)
                {
                    TempData["Mensaje"] =
                        "No puedes desactivar esta mesa porque tiene reservaciones pendientes o aceptadas.";

                    return RedirectToAction(nameof(Index));
                }
            }

            mesa.Activa = !mesa.Activa;

            mesa.Estado = mesa.Activa
                ? "Disponible"
                : "Inactiva";

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = mesa.Activa
                ? "Mesa activada correctamente."
                : "Mesa desactivada correctamente.";

            return RedirectToAction(nameof(Index));
        }
    }
}