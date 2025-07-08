namespace SentirseWellApi.Models
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public bool UseOAuth2 { get; set; }
        public string GoogleClientId { get; set; } = string.Empty;
        public string GoogleClientSecret { get; set; } = string.Empty;
        public string GoogleRefreshToken { get; set; } = string.Empty;
    }

    public class ResendEmailSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
    }

    public class QRCodeSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int DefaultExpirationMinutes { get; set; } = 60;
    }

    public class CorsSettings
    {
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }
} 