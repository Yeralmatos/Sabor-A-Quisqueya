using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaborAQuisqueya.Models
{
    public class Plato
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del plato es obligatorio")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio")]
        [Range(0.01, 100000, ErrorMessage = "Ingrese un precio válido")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Precio { get; set; }

        [StringLength(250)]
        public string? ImagenUrl { get; set; }

        public bool Disponible { get; set; } = true;

        // Relación con la categoría
        [Required]
        public int CategoriaPlatoId { get; set; }

        [ForeignKey("CategoriaPlatoId")]
        public CategoriaPlato CategoriaPlato { get; set; } = null!;

        // Relación con el restaurante
        [Required]
        public int RestauranteId { get; set; }

        [ForeignKey("RestauranteId")]
        public Restaurante Restaurante { get; set; } = null!;
    }
}