using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaborAQuisqueya.Models
{
    public class Mesa
    {
        public int Id { get; set; }

        [Required]
        public int RestauranteId { get; set; }

        [ForeignKey("RestauranteId")]
        public Restaurante Restaurante { get; set; } = null!;

        [Required(ErrorMessage = "El número de mesa es obligatorio")]
        [Range(1, 1000, ErrorMessage = "Ingrese un número de mesa válido")]
        public int Numero { get; set; }

        [Required(ErrorMessage = "La capacidad es obligatoria")]
        [Range(1, 50, ErrorMessage = "La capacidad debe estar entre 1 y 50 personas")]
        public int Capacidad { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Disponible";

        public bool Activa { get; set; } = true;

        // Relación con las reservas
        public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
    }
}