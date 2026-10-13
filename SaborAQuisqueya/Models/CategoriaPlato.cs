using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SaborAQuisqueya.Models
{
    public class CategoriaPlato
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de la categoría es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Descripcion { get; set; }

        public bool Activa { get; set; } = true;

        // Relación con los platos
        public ICollection<Plato> Platos { get; set; } = new List<Plato>();
    }
}