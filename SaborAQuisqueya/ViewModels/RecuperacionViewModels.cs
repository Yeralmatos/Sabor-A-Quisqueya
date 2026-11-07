using System.ComponentModel.DataAnnotations;

namespace SaborAQuisqueya.ViewModels;

public class SolicitarRecuperacionViewModel
{
    [Required(ErrorMessage = "Ingrese su correo electrónico.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;
}

public class RestablecerContrasenaViewModel
{
    [Required(ErrorMessage = "Ingrese su correo electrónico.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el código recibido por correo.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "El código debe contener exactamente seis dígitos.")]
    [Display(Name = "Código de recuperación")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese su nueva contraseña.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 128 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NuevaContrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme su nueva contraseña.")]
    [Compare(nameof(NuevaContrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarContrasena { get; set; } = string.Empty;
}
