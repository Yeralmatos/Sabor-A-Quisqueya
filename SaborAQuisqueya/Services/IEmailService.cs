namespace SaborAQuisqueya.Services;

public interface IEmailService
{
    Task EnviarCorreoAsync(string destinatario, string asunto, string mensaje);
}
