using SentirseWellApi.Models;
using System.Net.Mail;
using System.Net;
using System.Text;

namespace SentirseWellApi.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            try
            {
                _logger.LogInformation("Intentando enviar email a: {Email}", to);
                
                // Usar Gmail SMTP directamente
                var smtpServer = Environment.GetEnvironmentVariable("EMAIL_SMTP_SERVER") ?? "smtp.gmail.com";
                var smtpPort = int.Parse(Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT") ?? "587");
                var senderEmail = Environment.GetEnvironmentVariable("EMAIL_SENDER_EMAIL");
                var senderName = Environment.GetEnvironmentVariable("EMAIL_SENDER_NAME") ?? "Spa Sentirse Bien";

                _logger.LogInformation("Configuración SMTP: Server={Server}, Port={Port}, Sender={Sender}", smtpServer, smtpPort, senderEmail);

                if (string.IsNullOrEmpty(senderEmail))
                {
                    _logger.LogError("EMAIL_SENDER_EMAIL no está configurado en el archivo .env");
                    return false;
                }

                var password = await GetEmailPasswordAsync();
                _logger.LogInformation("Contraseña de aplicación obtenida: {Length} caracteres", password?.Length ?? 0);

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(senderEmail, password),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                mailMessage.To.Add(to);

                _logger.LogInformation("Enviando email...");
                await client.SendMailAsync(mailMessage);
                
                _logger.LogInformation("Email enviado exitosamente a: {Email}", to);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email a: {Email}. Error: {Message}", to, ex.Message);
                return false;
            }
        }

        private async Task<bool> SendEmailWithResendAsync(string to, string subject, string body, bool isHtml = false)
        {
            try
            {
                var resendApiKey = Environment.GetEnvironmentVariable("ResendEmail__ApiKey");
                var resendFromEmail = Environment.GetEnvironmentVariable("ResendEmail__FromEmail");
                
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {resendApiKey}");
                
                var emailData = new
                {
                    from = resendFromEmail,
                    to = new[] { to },
                    subject = subject,
                    html = body
                };
                
                var response = await client.PostAsJsonAsync("https://api.resend.com/emails", emailData);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email enviado exitosamente con Resend a: {Email}", to);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error enviando email con Resend: {Error}", errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email con Resend a: {Email}", to);
                return false;
            }
        }

        public async Task<bool> SendTurnoConfirmationAsync(User cliente, Turno turno, Service servicio)
        {
            try
            {
                var subject = "✅ Confirmación de Turno - Spa Sentirse Bien";
                var body = GenerateTurnoConfirmationTemplate(cliente, turno, servicio);

                return await SendEmailAsync(cliente.Email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar confirmación de turno: {TurnoId}", turno.Id);
                return false;
            }
        }

        public async Task<bool> SendTurnoCancellationAsync(User cliente, Turno turno, Service servicio)
        {
            try
            {
                var subject = "❌ Cancelación de Turno - Spa Sentirse Bien";
                var body = GenerateTurnoCancellationTemplate(cliente, turno, servicio);

                return await SendEmailAsync(cliente.Email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar cancelación de turno: {TurnoId}", turno.Id);
                return false;
            }
        }

        public async Task<bool> SendPaymentConfirmationAsync(User cliente, Payment payment, Turno turno, Service servicio)
        {
            try
            {
                var subject = "💳 Confirmación de Pago - Spa Sentirse Bien";
                var body = GeneratePaymentConfirmationTemplate(cliente, payment, turno, servicio);

                return await SendEmailAsync(cliente.Email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar confirmación de pago: {PaymentId}", payment.Id);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetAsync(User user, string resetToken)
        {
            try
            {
                var subject = "🔐 Recuperación de Contraseña - Spa Sentirse Bien";
                var body = GeneratePasswordResetTemplate(user, resetToken);

                return await SendEmailAsync(user.Email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar recuperación de contraseña: {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendWelcomeEmailAsync(User user)
        {
            try
            {
                var subject = "🌟 ¡Bienvenido a Spa Sentirse Bien!";
                var body = GenerateWelcomeTemplate(user);

                return await SendEmailAsync(user.Email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email de bienvenida: {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> SendQRCodeAsync(User user, string qrCodeBase64, string action, Dictionary<string, object>? data = null)
        {
            try
            {
                var subject = "📱 Tu Código QR - Spa Sentirse Bien";
                var body = GenerateQRCodeTemplate(user, qrCodeBase64, action, data);

                return await SendEmailAsync(user.Email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar QR Code: {UserId}", user.Id);
                return false;
            }
        }

        private async Task<string> GetEmailPasswordAsync()
        {
            // Usar la contraseña de aplicación de Gmail desde variables de entorno
            var password = Environment.GetEnvironmentVariable("EMAIL_APP_PASSWORD");
            
            if (string.IsNullOrEmpty(password))
            {
                _logger.LogError("EMAIL_APP_PASSWORD no está configurado en el archivo .env");
                throw new InvalidOperationException("Credenciales de email no configuradas");
            }
            
            return password;
        }

        #region Email Templates

        private string GenerateTurnoConfirmationTemplate(User cliente, Turno turno, Service servicio)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; margin: 0; padding: 20px; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; margin: -20px -20px 20px -20px; }}
        .content {{ padding: 20px 0; }}
        .turno-details {{ background: #f8f9fa; padding: 15px; border-radius: 8px; margin: 15px 0; }}
        .footer {{ text-align: center; padding-top: 20px; border-top: 1px solid #eee; color: #666; }}
        .btn {{ background: #667eea; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Turno Confirmado</h1>
            <p>Tu cita ha sido confirmada exitosamente</p>
        </div>
        
        <div class='content'>
            <p>Hola <strong>{cliente.FirstName} {cliente.LastName}</strong>,</p>
            
            <p>Tu turno ha sido <strong>confirmado</strong> con los siguientes detalles:</p>
            
            <div class='turno-details'>
                <h3>📋 Detalles del Turno</h3>
                <p><strong>Servicio:</strong> {servicio.Nombre}</p>
                <p><strong>Fecha:</strong> {turno.Fecha:dddd, dd/MM/yyyy}</p>
                <p><strong>Hora:</strong> {turno.Hora}</p>
                <p><strong>Precio:</strong> ${servicio.Precio:N2}</p>
                {(!string.IsNullOrEmpty(turno.Notas) ? $"<p><strong>Notas:</strong> {turno.Notas}</p>" : "")}
            </div>
            
            <p>📍 <strong>Ubicación:</strong> Spa Sentirse Bien<br>
            📞 <strong>Contacto:</strong> (011) 1234-5678</p>
            
            <p>Por favor, llega 10 minutos antes de tu cita. Si necesitas cancelar o reprogramar, contáctanos con al menos 24 horas de anticipación.</p>
            
            <center>
                <a href='#' class='btn'>Ver Mis Turnos</a>
            </center>
        </div>
        
        <div class='footer'>
            <p>Spa Sentirse Bien - Tu bienestar es nuestra prioridad</p>
            <p>Este email fue enviado automáticamente, por favor no responder.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateTurnoCancellationTemplate(User cliente, Turno turno, Service servicio)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; margin: 0; padding: 20px; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #ff6b6b 0%, #ee5a24 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; margin: -20px -20px 20px -20px; }}
        .content {{ padding: 20px 0; }}
        .turno-details {{ background: #f8f9fa; padding: 15px; border-radius: 8px; margin: 15px 0; }}
        .footer {{ text-align: center; padding-top: 20px; border-top: 1px solid #eee; color: #666; }}
        .btn {{ background: #667eea; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>❌ Turno Cancelado</h1>
            <p>Tu cita ha sido cancelada</p>
        </div>
        
        <div class='content'>
            <p>Hola <strong>{cliente.FirstName} {cliente.LastName}</strong>,</p>
            
            <p>Tu turno ha sido <strong>cancelado</strong>:</p>
            
            <div class='turno-details'>
                <h3>📋 Turno Cancelado</h3>
                <p><strong>Servicio:</strong> {servicio.Nombre}</p>
                <p><strong>Fecha:</strong> {turno.Fecha:dddd, dd/MM/yyyy}</p>
                <p><strong>Hora:</strong> {turno.Hora}</p>
            </div>
            
            <p>Si deseas reagendar, puedes hacerlo a través de nuestra plataforma o contactándonos directamente.</p>
            
            <center>
                <a href='#' class='btn'>Agendar Nuevo Turno</a>
            </center>
        </div>
        
        <div class='footer'>
            <p>Spa Sentirse Bien - Tu bienestar es nuestra prioridad</p>
            <p>Este email fue enviado automáticamente, por favor no responder.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GeneratePaymentConfirmationTemplate(User cliente, Payment payment, Turno turno, Service servicio)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; margin: 0; padding: 20px; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #26de81 0%, #20bf6b 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; margin: -20px -20px 20px -20px; }}
        .content {{ padding: 20px 0; }}
        .payment-details {{ background: #f8f9fa; padding: 15px; border-radius: 8px; margin: 15px 0; }}
        .footer {{ text-align: center; padding-top: 20px; border-top: 1px solid #eee; color: #666; }}
        .amount {{ font-size: 24px; font-weight: bold; color: #26de81; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>💳 Pago Confirmado</h1>
            <p>Tu pago ha sido procesado exitosamente</p>
        </div>
        
        <div class='content'>
            <p>Hola <strong>{cliente.FirstName} {cliente.LastName}</strong>,</p>
            
            <p>Tu pago ha sido <strong>confirmado</strong> con los siguientes detalles:</p>
            
            <div class='payment-details'>
                <h3>💰 Detalles del Pago</h3>
                <p><strong>Monto:</strong> <span class='amount'>${payment.Monto:N2}</span></p>
                <p><strong>Método:</strong> {payment.MetodoPago}</p>
                <p><strong>Fecha:</strong> {payment.ProcessedAt:dd/MM/yyyy HH:mm}</p>
                {(!string.IsNullOrEmpty(payment.TransactionId) ? $"<p><strong>ID Transacción:</strong> {payment.TransactionId}</p>" : "")}
                
                <hr style='margin: 15px 0;'>
                
                <h3>📋 Servicio Pagado</h3>
                <p><strong>Servicio:</strong> {servicio.Nombre}</p>
                <p><strong>Fecha del Turno:</strong> {turno.Fecha:dddd, dd/MM/yyyy}</p>
                <p><strong>Hora:</strong> {turno.Hora}</p>
            </div>
            
            <p>Guarda este email como comprobante de pago. Tu turno está confirmado y te esperamos en la fecha programada.</p>
        </div>
        
        <div class='footer'>
            <p>Spa Sentirse Bien - Tu bienestar es nuestra prioridad</p>
            <p>Este email fue enviado automáticamente, por favor no responder.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GeneratePasswordResetTemplate(User user, string resetToken)
        {
            var frontendBaseUrl = Environment.GetEnvironmentVariable("FRONTEND_BASE_URL") ?? "http://localhost:3000";
            var resetUrl = $"{frontendBaseUrl}/reset-password?token={resetToken}";
            
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; margin: 0; padding: 20px; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; margin: -20px -20px 20px -20px; }}
        .content {{ padding: 20px 0; }}
        .footer {{ text-align: center; padding-top: 20px; border-top: 1px solid #eee; color: #666; }}
        .btn {{ background: #667eea; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 10px 0; }}
        .warning {{ background: #fff3cd; border: 1px solid #ffeaa7; padding: 10px; border-radius: 5px; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Recuperación de Contraseña</h1>
            <p>Solicitud para restablecer tu contraseña</p>
        </div>
        
        <div class='content'>
            <p>Hola <strong>{user.FirstName} {user.LastName}</strong>,</p>
            
            <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta en Spa Sentirse Bien.</p>
            
            <center>
                <a href='{resetUrl}' class='btn'>Restablecer Contraseña</a>
            </center>
            
            <div class='warning'>
                <p><strong>⚠️ Importante:</strong></p>
                <ul>
                    <li>Este enlace expira en 1 hora</li>
                    <li>Solo puede ser usado una vez</li>
                    <li>Si no solicitaste este cambio, ignora este email</li>
                </ul>
            </div>
            
            <p>Si tienes problemas con el botón, copia y pega esta URL en tu navegador:<br>
            <small>{resetUrl}</small></p>
        </div>
        
        <div class='footer'>
            <p>Spa Sentirse Bien - Tu bienestar es nuestra prioridad</p>
            <p>Este email fue enviado automáticamente, por favor no responder.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateWelcomeTemplate(User user)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; margin: 0; padding: 20px; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; margin: -20px -20px 20px -20px; }}
        .content {{ padding: 20px 0; }}
        .footer {{ text-align: center; padding-top: 20px; border-top: 1px solid #eee; color: #666; }}
        .btn {{ background: #667eea; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 10px 0; }}
        .features {{ background: #f8f9fa; padding: 15px; border-radius: 8px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🌟 ¡Bienvenido a Spa Sentirse Bien!</h1>
            <p>Tu cuenta ha sido creada exitosamente</p>
        </div>
        
        <div class='content'>
            <p>Hola <strong>{user.FirstName} {user.LastName}</strong>,</p>
            
            <p>¡Nos alegra tenerte como parte de nuestra comunidad! Tu cuenta ha sido creada exitosamente.</p>
            
            <div class='features'>
                <h3>✨ ¿Qué puedes hacer ahora?</h3>
                <ul>
                    <li>🗓️ Reservar turnos para nuestros servicios</li>
                    <li>💳 Pagar tus citas de forma segura</li>
                    <li>📱 Generar códigos QR para check-in</li>
                    <li>📊 Ver el historial de tus turnos</li>
                    <li>🎯 Acceder a ofertas exclusivas</li>
                </ul>
            </div>
            
            <p>Nuestro equipo está aquí para ayudarte a relajarte y sentirte bien. ¡Agenda tu primera cita!</p>
            
            <center>
                <a href='#' class='btn'>Explorar Servicios</a>
            </center>
        </div>
        
        <div class='footer'>
            <p>Spa Sentirse Bien - Tu bienestar es nuestra prioridad</p>
            <p>📍 Dirección del Spa | 📞 (011) 1234-5678</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateQRCodeTemplate(User user, string qrCodeBase64, string action, Dictionary<string, object>? data)
        {
            var actionDescription = action switch
            {
                "check_in" => "Check-in para tu turno",
                "payment_confirmation" => "Confirmación de pago",
                "service_access" => "Acceso a servicio exclusivo",
                "special_offer" => "Oferta especial",
                _ => "Código QR especial"
            };

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; margin: 0; padding: 20px; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; margin: -20px -20px 20px -20px; }}
        .content {{ padding: 20px 0; text-align: center; }}
        .qr-container {{ background: white; padding: 20px; border-radius: 10px; margin: 20px 0; display: inline-block; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .footer {{ text-align: center; padding-top: 20px; border-top: 1px solid #eee; color: #666; }}
        .instructions {{ background: #e3f2fd; border-left: 4px solid #2196f3; padding: 15px; margin: 15px 0; text-align: left; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📱 Tu Código QR</h1>
            <p>{actionDescription}</p>
        </div>
        
        <div class='content'>
            <p>Hola <strong>{user.FirstName} {user.LastName}</strong>,</p>
            
            <p>Tu código QR ha sido generado exitosamente:</p>
            
            <div class='qr-container'>
                <img src='data:image/png;base64,{qrCodeBase64}' alt='Código QR' style='max-width: 200px; height: auto;'>
            </div>
            
            <div class='instructions'>
                <h3>📋 Instrucciones de uso:</h3>
                <ol>
                    <li>Guarda este email o toma una captura del QR</li>
                    <li>Presenta el código al personal del spa</li>
                    <li>El código expira automáticamente después de su uso</li>
                    <li>No compartas este código con otras personas</li>
                </ol>
            </div>
        </div>
        
        <div class='footer'>
            <p>Spa Sentirse Bien - Tu bienestar es nuestra prioridad</p>
            <p>Este email fue enviado automáticamente, por favor no responder.</p>
        </div>
    </div>
</body>
</html>";
        }

        #endregion
    }
} 