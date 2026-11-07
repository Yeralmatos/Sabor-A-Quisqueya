using System.Net;
using System.Net.Mail;

namespace SaborAQuisqueya.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string ObtenerConfiguracion(string clave)
        {
            string? valor = _configuration[$"EmailSettings:{clave}"];
            if (string.IsNullOrWhiteSpace(valor))
            {
                throw new InvalidOperationException($"No se configuró EmailSettings:{clave}.");
            }
            return valor;
        }

        public async Task EnviarCorreoAsync(
            string destinatario,
            string asunto,
            string mensaje)
        {
            string servidor = ObtenerConfiguracion("SmtpServer");
            string correo = ObtenerConfiguracion("SenderEmail");
            string clave = ObtenerConfiguracion("Password");
            if (!int.TryParse(_configuration["EmailSettings:Port"], out int puerto) ||
                puerto < 1 || puerto > 65535)
            {
                throw new InvalidOperationException("Configure un puerto SMTP válido.");
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(correo, "Sabor a Quisqueya"),
                Subject = asunto,
                Body = mensaje,
                IsBodyHtml = false
            };

            mail.To.Add(destinatario);

            using var smtp = new SmtpClient(servidor, puerto)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _configuration["EmailSettings:Username"] ?? correo, clave),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            try
            {
                await smtp.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al enviar el correo de Sabor a Quisqueya.");

                throw;
            }
        }
    }
}
