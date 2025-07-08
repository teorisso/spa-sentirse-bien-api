using SentirseWellApi.Models;

namespace SentirseWellApi.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false);
        Task<bool> SendTurnoConfirmationAsync(User cliente, Turno turno, Service servicio);
        Task<bool> SendTurnoCancellationAsync(User cliente, Turno turno, Service servicio);
        Task<bool> SendPaymentConfirmationAsync(User cliente, Payment payment, Turno turno, Service servicio);
        Task<bool> SendPasswordResetAsync(User user, string resetToken);
        Task<bool> SendWelcomeEmailAsync(User user);
        Task<bool> SendQRCodeAsync(User user, string qrCodeBase64, string action, Dictionary<string, object>? data = null);
    }
} 