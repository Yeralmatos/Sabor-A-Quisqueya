using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SaborAQuisqueya.Models
{
    public class Restaurante
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(150)]
        public string Nombre { get; set; } = "Sabor a Quisqueya";

        [Required(ErrorMessage = "La dirección es obligatoria")]
        [StringLength(250)]
        public string Direccion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [StringLength(20)]
        public string Telefono { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [StringLength(250)]
        public string? ImagenUrl { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string? CorreoContacto { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Relaciones con las demás tablas
        public ICollection<Horario> Horarios { get; set; } = new List<Horario>();

        public ICollection<Mesa> Mesas { get; set; } = new List<Mesa>();

        public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();

        public ICollection<Plato> Platos { get; set; } = new List<Plato>();
    }
}