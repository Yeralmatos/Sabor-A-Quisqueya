using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaborAQuisqueya.Models
{
    public class Reserva
    {
        public int Id { get; set; }

        // Cliente que realiza la reserva
        [Required]
        public int UsuarioId { get; set; }

        [ForeignKey("UsuarioId")]
        public Usuario Usuario { get; set; } = null!;

        // Restaurante
        [Required]
        public int RestauranteId { get; set; }

        [ForeignKey("RestauranteId")]
        public Restaurante Restaurante { get; set; } = null!;

        // Mesa asignada
        public int? MesaId { get; set; }

        [ForeignKey("MesaId")]
        public Mesa? Mesa { get; set; }

        // Fecha y hora de la reserva
        [Required(ErrorMessage = "La fecha es obligatoria")]
        [DataType(DataType.Date)]
        public DateTime FechaReserva { get; set; }

        [Required(ErrorMessage = "La hora es obligatoria")]
        [DataType(DataType.Time)]
        public TimeSpan HoraReserva { get; set; }

        // Cantidad de personas
        [Required(ErrorMessage = "Indique la cantidad de personas")]
        [Range(1, 50, ErrorMessage = "La cantidad debe estar entre 1 y 50 personas")]
        public int CantidadPersonas { get; set; }

        // Estado de la reserva
        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Pendiente";

        // Comentarios adicionales
        [StringLength(500)]
        public string? Observaciones { get; set; }

        // Fecha en que se realizó la solicitud
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}