using System.Data;
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SaborAQuisqueya.Data;
using SaborAQuisqueya.Models;
using SaborAQuisqueya.Services;

namespace SaborAQuisqueya.Controllers
{
    [Authorize]
    public class ReservasController : Controller
    {
        // ==========================================
        // SERVICIOS
        // ==========================================

        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<ReservasController> _logger;

        private static readonly TimeSpan DuracionReserva =
            TimeSpan.FromHours(2);

        public ReservasController(
            ApplicationDbContext context,
            EmailService emailService,
            ILogger<ReservasController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        private int UsuarioActualId =>
            int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );


        // ==========================================
        // CLIENTE - MIS RESERVACIONES
        // ==========================================

        [Authorize(Roles = "Cliente")]
        [HttpGet]
        public async Task<IActionResult> MisReservas()
        {
            var reservas = await _context.Reservas
                .AsNoTracking()
                .Include(r => r.Restaurante)
                .Include(r => r.Mesa)
                .Where(r => r.UsuarioId == UsuarioActualId)
                .OrderByDescending(r => r.FechaReserva)
                .ThenByDescending(r => r.HoraReserva)
                .ToListAsync();

            return View(reservas);
        }


        // ==========================================
        // CLIENTE - FORMULARIO DE RESERVACIÓN
        // ==========================================

        [Authorize(Roles = "Cliente")]
        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }


        // ==========================================
        // CLIENTE - CREAR RESERVACIÓN
        // ==========================================

        [Authorize(Roles = "Cliente")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            DateTime fechaReserva,
            TimeSpan horaReserva,
            int cantidadPersonas,
            string? observaciones)
        {
            // 1. Validar fecha

            if (fechaReserva == default ||
                fechaReserva.Date < DateTime.Today)
            {
                ModelState.AddModelError(
                    "",
                    "Selecciona una fecha válida."
                );
            }

            // 2. Validar hora

            if (horaReserva < TimeSpan.Zero ||
                horaReserva >= TimeSpan.FromDays(1))
            {
                ModelState.AddModelError(
                    "",
                    "Selecciona una hora válida."
                );
            }

            if (fechaReserva.Date == DateTime.Today &&
                horaReserva <= DateTime.Now.TimeOfDay)
            {
                ModelState.AddModelError(
                    "",
                    "No puedes reservar una hora que ya pasó."
                );
            }

            // 3. Validar cantidad de personas

            if (cantidadPersonas < 1 ||
                cantidadPersonas > 20)
            {
                ModelState.AddModelError(
                    "",
                    "La cantidad debe estar entre 1 y 20 personas."
                );
            }

            // 4. Validar observaciones

            if (observaciones?.Length > 500)
            {
                ModelState.AddModelError(
                    "",
                    "Las observaciones no pueden superar 500 caracteres."
                );
            }

            var horaFin = horaReserva + DuracionReserva;

            if (horaFin > TimeSpan.FromDays(1))
            {
                ModelState.AddModelError(
                    "",
                    "La reservación no puede terminar después de medianoche."
                );
            }

            if (!ModelState.IsValid)
            {
                return View();
            }


            // ======================================
            // BUSCAR SABOR A QUISQUEYA
            // ======================================

            var restaurante = await _context.Restaurantes
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.Nombre == "Sabor a Quisqueya" &&
                    r.Activo);

            if (restaurante == null)
            {
                ModelState.AddModelError(
                    "",
                    "Sabor a Quisqueya no está configurado o está inactivo."
                );

                return View();
            }


            // ======================================
            // VALIDAR HORARIO
            // ======================================

            var horario = await _context.Horarios
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.RestauranteId == restaurante.Id &&
                    h.DiaSemana == fechaReserva.DayOfWeek &&
                    h.Activo);

            if (horario == null)
            {
                ModelState.AddModelError(
                    "",
                    "El restaurante no tiene un horario de atención configurado para ese día."
                );

                return View();
            }

            if (horaReserva < horario.HoraApertura ||
                horaFin > horario.HoraCierre)
            {
                ModelState.AddModelError(
                    "",
                    "La hora seleccionada está fuera del horario de atención."
                );

                return View();
            }


            // ======================================
            // BUSCAR MESA DISPONIBLE
            // ======================================

            await using var transaccion =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable
                );

            var mesas = await _context.Mesas
                .Where(m =>
                    m.RestauranteId == restaurante.Id &&
                    m.Activa &&
                    m.Capacidad >= cantidadPersonas)
                .OrderBy(m => m.Capacidad)
                .ThenBy(m => m.Numero)
                .ToListAsync();

            if (mesas.Count == 0)
            {
                ModelState.AddModelError(
                    "",
                    "No existen mesas activas con capacidad para esa cantidad de personas."
                );

                return View();
            }

            var reservasDelDia = await _context.Reservas
                .Where(r =>
                    r.RestauranteId == restaurante.Id &&
                    r.FechaReserva == fechaReserva.Date &&
                    r.MesaId != null &&
                    r.Estado != "Cancelada" &&
                    r.Estado != "Rechazada")
                .ToListAsync();

            var mesaDisponible = mesas.FirstOrDefault(m =>
                !reservasDelDia.Any(r =>
                    r.MesaId == m.Id &&
                    r.HoraReserva < horaFin &&
                    r.HoraReserva + DuracionReserva > horaReserva
                )
            );

            if (mesaDisponible == null)
            {
                ModelState.AddModelError(
                    "",
                    "No hay mesas disponibles para esa fecha, hora y cantidad de personas."
                );

                return View();
            }


            // ======================================
            // REGISTRAR RESERVACIÓN
            // ======================================

            var reserva = new Reserva
            {
                UsuarioId = UsuarioActualId,

                RestauranteId = restaurante.Id,

                MesaId = mesaDisponible.Id,

                FechaReserva = fechaReserva.Date,

                HoraReserva = horaReserva,

                CantidadPersonas = cantidadPersonas,

                Observaciones = observaciones?.Trim(),

                Estado = "Pendiente",

                FechaRegistro = DateTime.Now
            };

            _context.Reservas.Add(reserva);

            await _context.SaveChangesAsync();

            await transaccion.CommitAsync();

            TempData["Mensaje"] =
                "Tu reservación fue registrada correctamente. Está pendiente de aprobación.";

            return RedirectToAction(nameof(MisReservas));
        }


        // ==========================================
        // CLIENTE - CANCELAR RESERVACIÓN
        // ==========================================

        [Authorize(Roles = "Cliente")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var reserva = await _context.Reservas
                .FirstOrDefaultAsync(r =>
                    r.Id == id &&
                    r.UsuarioId == UsuarioActualId);

            if (reserva == null)
            {
                return NotFound();
            }

            if (reserva.Estado != "Pendiente" &&
                reserva.Estado != "Aceptada")
            {
                TempData["Mensaje"] =
                    "Esta reservación no se puede cancelar en su estado actual.";

                return RedirectToAction(nameof(MisReservas));
            }

            reserva.Estado = "Cancelada";

            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                "Reservación cancelada correctamente.";

            return RedirectToAction(nameof(MisReservas));
        }


        // ==========================================
        // ADMINISTRADOR - VER RESERVACIONES
        // ==========================================

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<IActionResult> Administrar()
        {
            var reservas = await _context.Reservas
                .AsNoTracking()
                .Include(r => r.Usuario)
                .Include(r => r.Restaurante)
                .Include(r => r.Mesa)
                .OrderBy(r => r.FechaReserva)
                .ThenBy(r => r.HoraReserva)
                .ToListAsync();

            return View(reservas);
        }


        // ==========================================
        // ADMINISTRADOR - CAMBIAR ESTADO
        // Y ENVIAR NOTIFICACIÓN POR CORREO
        // ==========================================

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(
            int id,
            string estado)
        {
            // Validar estados permitidos

            if (estado is not (
                "Aceptada" or
                "Rechazada" or
                "Cancelada"))
            {
                return BadRequest("Estado no válido.");
            }

            // Buscar reservación y cliente

            var reserva = await _context.Reservas
                .Include(r => r.Usuario)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reserva == null)
            {
                return NotFound();
            }

            // Validar estado actual

            if (reserva.Estado != "Pendiente")
            {
                TempData["Mensaje"] =
                    "Solamente se pueden procesar reservaciones pendientes.";

                return RedirectToAction(nameof(Administrar));
            }


            // ======================================
            // VERIFICAR MESA AL ACEPTAR
            // ======================================

            if (estado == "Aceptada")
            {
                if (reserva.MesaId == null)
                {
                    TempData["Mensaje"] =
                        "La reservación no tiene una mesa asignada.";

                    return RedirectToAction(nameof(Administrar));
                }

                var mesaActiva = await _context.Mesas
                    .AnyAsync(m =>
                        m.Id == reserva.MesaId &&
                        m.RestauranteId == reserva.RestauranteId &&
                        m.Activa &&
                        m.Capacidad >= reserva.CantidadPersonas);

                if (!mesaActiva)
                {
                    TempData["Mensaje"] =
                        "No se puede aceptar: la mesa asignada está inactiva o no tiene capacidad suficiente.";

                    return RedirectToAction(nameof(Administrar));
                }

                // Comprobar conflictos de horario.
                // Primero consultamos SQL Server y luego
                // comparamos los intervalos en memoria.

                var otrasReservas = await _context.Reservas
                    .AsNoTracking()
                    .Where(r =>
                        r.Id != reserva.Id &&
                        r.MesaId == reserva.MesaId &&
                        r.FechaReserva == reserva.FechaReserva &&
                        r.Estado != "Cancelada" &&
                        r.Estado != "Rechazada")
                    .ToListAsync();

                var horaFin = reserva.HoraReserva +
                              DuracionReserva;

                bool existeConflicto = otrasReservas.Any(r =>
                    r.HoraReserva < horaFin &&
                    r.HoraReserva + DuracionReserva >
                    reserva.HoraReserva
                );

                if (existeConflicto)
                {
                    TempData["Mensaje"] =
                        "No se puede aceptar esta reservación porque existe un conflicto de horario.";

                    return RedirectToAction(nameof(Administrar));
                }
            }


            // ======================================
            // ACTUALIZAR ESTADO EN SQL SERVER
            // ======================================

            reserva.Estado = estado;

            await _context.SaveChangesAsync();


            // ======================================
            // PREPARAR NOTIFICACIÓN
            // ======================================

            var cliente = reserva.Usuario;

            if (cliente == null ||
                string.IsNullOrWhiteSpace(cliente.Correo))
            {
                TempData["Mensaje"] =
                    $"Reservación actualizada a {estado}, pero el cliente no tiene un correo registrado.";

                return RedirectToAction(nameof(Administrar));
            }

            string asunto = estado switch
            {
                "Aceptada" =>
                    "Reservación confirmada - Sabor a Quisqueya",

                "Rechazada" =>
                    "Respuesta a tu reservación - Sabor a Quisqueya",

                "Cancelada" =>
                    "Reservación cancelada - Sabor a Quisqueya",

                _ =>
                    "Actualización de reservación - Sabor a Quisqueya"
            };


            // ======================================
            // MENSAJE SEGÚN EL ESTADO
            // ======================================

            string respuesta = estado switch
            {
                "Aceptada" =>
                    "¡Buenas noticias! Tu reservación ha sido aceptada y confirmada.",

                "Rechazada" =>
                    "Lamentamos informarte que tu solicitud de reservación no ha sido aceptada.",

                "Cancelada" =>
                    "Te informamos que tu reservación ha sido cancelada por el restaurante.",

                _ =>
                    "El estado de tu reservación ha sido actualizado."
            };


            string fecha = reserva.FechaReserva.ToString(
                "dd/MM/yyyy"
            );

            string hora = DateTime.Today
                .Add(reserva.HoraReserva)
                .ToString("hh:mm tt");

            string mensaje = $"""
                SABOR A QUISQUEYA
                Notificación de reservación

                Hola, {cliente.Nombre}:

                {respuesta}

                DETALLES DE TU RESERVACIÓN

                Número de reservación: {reserva.Id}
                Fecha: {fecha}
                Hora: {hora}
                Cantidad de personas: {reserva.CantidadPersonas}
                Estado: {estado}

                Para cualquier consulta puedes comunicarte
                con nosotros al 809-267-7160.

                Gracias por elegir Sabor a Quisqueya.
                ¡El auténtico sabor dominicano!
                """;


            // ======================================
            // ENVIAR CORREO REAL
            // ======================================

            try
            {
                await _emailService.EnviarCorreoAsync(
                    cliente.Correo,
                    asunto,
                    mensaje
                );

                TempData["Mensaje"] =
                    $"Reservación {estado.ToLower()} correctamente. " +
                    "Se envió un correo electrónico al cliente.";
            }
            catch (Exception ex)
            {
                // Registrar el problema sin deshacer
                // la decisión del administrador.

                _logger.LogError(
                    ex,
                    "Error al enviar notificación de la reservación {ReservaId}",
                    reserva.Id
                );

                TempData["Mensaje"] =
                    $"Reservación actualizada a {estado}, " +
                    "pero ocurrió un error al enviar el correo electrónico.";
            }

            return RedirectToAction(nameof(Administrar));
        }
    }
}