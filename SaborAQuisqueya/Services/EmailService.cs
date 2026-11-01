using System.Net;
using System.Net.Mail;

namespace SaborAQuisqueya.Services
{
    public class EmailService
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

        public async Task EnviarCorreoAsync(
            string destinatario,
            string asunto,
            string mensaje)
        {
            string servidor = _configuration["EmailSettings:SmtpServer"]
                ?? throw new InvalidOperationException(
                    "No se configuró el servidor SMTP.");

            int puerto = int.Parse(
                _configuration["EmailSettings:Port"]
                ?? throw new InvalidOperationException(
                    "No se configuró el puerto SMTP."));

            string correo = _configuration["EmailSettings:SenderEmail"]
                ?? throw new InvalidOperationException(
                    "No se configuró el correo remitente.");

            string clave = _configuration["EmailSettings:Password"]
                ?? throw new InvalidOperationException(
                    "No se configuró la contraseña de aplicación.");

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
                Credentials = new NetworkCredential(correo, clave),
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