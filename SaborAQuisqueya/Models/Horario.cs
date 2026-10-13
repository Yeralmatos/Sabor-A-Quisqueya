using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaborAQuisqueya.Models
{
    public class Horario
    {
        public int Id { get; set; }

        [Required]
        public int RestauranteId { get; set; }

        [ForeignKey("RestauranteId")]
        public Restaurante Restaurante { get; set; } = null!;

        [Required(ErrorMessage = "El día de la semana es obligatorio")]
        public DayOfWeek DiaSemana { get; set; }

        [Required(ErrorMessage = "La hora de apertura es obligatoria")]
        public TimeSpan HoraApertura { get; set; }

        [Required(ErrorMessage = "La hora de cierre es obligatoria")]
        public TimeSpan HoraCierre { get; set; }

        public bool Activo { get; set; } = true;
    }
}