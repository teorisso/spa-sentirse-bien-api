using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SentirseWellApi.Models
{
    public class Payment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("turno_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        [Required(ErrorMessage = "El turno es requerido")]
        public string TurnoId { get; set; } = string.Empty;

        [BsonElement("cliente_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        [Required(ErrorMessage = "El cliente es requerido")]
        public string ClienteId { get; set; } = string.Empty;

        [BsonElement("monto")]
        [Required(ErrorMessage = "El monto es requerido")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Monto { get; set; }

        [BsonElement("metodo_pago")]
        [Required(ErrorMessage = "El método de pago es requerido")]
        public string MetodoPago { get; set; } = string.Empty;

        [BsonElement("estado")]
        public string Estado { get; set; } = "pendiente";

        [BsonElement("transaction_id")]
        public string? TransactionId { get; set; }

        [BsonElement("payment_details")]
        public PaymentDetails? PaymentDetails { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("processed_at")]
        public DateTime? ProcessedAt { get; set; }

        [BsonElement("notas")]
        public string? Notas { get; set; }

        // Propiedades de navegación
        [BsonIgnore]
        public Turno? Turno { get; set; }

        [BsonIgnore]
        public User? Cliente { get; set; }
    }

    public class PaymentDetails
    {
        [BsonElement("card_number")]
        public string? CardNumber { get; set; }

        [BsonElement("card_holder")]
        public string? CardHolder { get; set; }

        [BsonElement("expiry_month")]
        public int? ExpiryMonth { get; set; }

        [BsonElement("expiry_year")]
        public int? ExpiryYear { get; set; }

        [BsonElement("bank_name")]
        public string? BankName { get; set; }

        [BsonElement("authorization_code")]
        public string? AuthorizationCode { get; set; }
    }

    public enum PaymentStatus
    {
        Pendiente,
        Procesando,
        Completado,
        Fallido,
        Reembolsado,
        Cancelado
    }

    public enum PaymentMethod
    {
        Efectivo,
        TarjetaCredito,
        TarjetaDebito,
        Transferencia,
        MercadoPago,
        Otro
    }

    public class PaymentDto
    {
        public string? Id { get; set; }
        public string TurnoId { get; set; } = string.Empty;
        public string ClienteId { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string MetodoPago { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? Notas { get; set; }

        // Información expandida
        public TurnoDto? Turno { get; set; }
        public UserDto? Cliente { get; set; }
    }

    public class CreatePaymentDto
    {
        [Required(ErrorMessage = "El turno es requerido")]
        public string TurnoId { get; set; } = string.Empty;

        [Required(ErrorMessage = "El monto es requerido")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "El método de pago es requerido")]
        public string MetodoPago { get; set; } = string.Empty;

        public PaymentDetails? PaymentDetails { get; set; }
        public string? Notas { get; set; }
    }

    public class ProcessPaymentDto
    {
        [Required(ErrorMessage = "El ID del pago es requerido")]
        public string PaymentId { get; set; } = string.Empty;

        public string? TransactionId { get; set; }
        public string? AuthorizationCode { get; set; }
        public string? Notas { get; set; }
    }

    public class PaymentFilterDto
    {
        public string? ClienteId { get; set; }
        public string? Estado { get; set; }
        public string? MetodoPago { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public decimal? MontoMinimo { get; set; }
        public decimal? MontoMaximo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
} 