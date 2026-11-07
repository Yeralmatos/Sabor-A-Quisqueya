using System.ComponentModel.DataAnnotations;

namespace SaborAQuisqueya.Models;

public class RecuperacionContrasena
{
    [Key]
    public int UsuarioId { get; set; }

    [Required, StringLength(256)]
    public string CodigoHash { get; set; } = string.Empty;

    public DateTime ExpiraUtc { get; set; }
    public DateTime ProximoEnvioUtc { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }

    // Cada escritura renueva esta versión para impedir consumo o envío simultáneo.
    [ConcurrencyCheck]
    public Guid Version { get; set; } = Guid.NewGuid();

    public Usuario Usuario { get; set; } = null!;
}
