using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SentirseWellApi.Models
{
    [BsonIgnoreExtraElements] // Ignorar campos adicionales como __v de Mongoose
    public class Payment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("turnos_ids")]
        [Required(ErrorMessage = "Los turnos son requeridos")]
        public List<string> TurnosIds { get; set; } = new List<string>();

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
        [BsonIgnoreIfNull] // No serializar si es null
        public string? TransactionId { get; set; }

        [BsonElement("payment_details")]
        [BsonIgnoreIfNull] // No serializar si es null
        public PaymentDetails? PaymentDetails { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("processed_at")]
        [BsonIgnoreIfNull] // No serializar si es null
        public DateTime? ProcessedAt { get; set; }

        [BsonElement("notas")]
        [BsonIgnoreIfNull] // No serializar si es null o vacío
        public string? Notas { get; set; }

        // Propiedades de navegación
        [BsonIgnore]
        public List<Turno>? Turnos { get; set; }

        [BsonIgnore]
        public User? Cliente { get; set; }

        // Compatibilidad con estructura antigua (single turno) - SOLO para lectura, no persistir
        [BsonIgnore] // ⚠️ CRÍTICO: No persistir este campo en MongoDB
        public string? TurnoId 
        { 
            get => TurnosIds.FirstOrDefault();
            set 
            { 
                if (!string.IsNullOrEmpty(value) && !TurnosIds.Contains(value))
                {
                    TurnosIds.Add(value);
                }
            }
        }
    }

    public class PaymentDetails
    {
        [BsonElement("card_number")]
        [BsonIgnoreIfNull]
        public string? CardNumber { get; set; }

        [BsonElement("card_holder")]
        [BsonIgnoreIfNull]
        public string? CardHolder { get; set; }

        [BsonElement("expiry_month")]
        [BsonIgnoreIfNull]
        public int? ExpiryMonth { get; set; }

        [BsonElement("expiry_year")]
        [BsonIgnoreIfNull]
        public int? ExpiryYear { get; set; }

        [BsonElement("bank_name")]
        [BsonIgnoreIfNull]
        public string? BankName { get; set; }

        [BsonElement("authorization_code")]
        [BsonIgnoreIfNull]
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
        public List<string> TurnosIds { get; set; } = new List<string>();
        public string ClienteId { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string MetodoPago { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? Notas { get; set; }

        // Información expandida
        public List<TurnoDto>? Turnos { get; set; }
        public UserDto? Cliente { get; set; }

        // Compatibilidad con estructura antigua
        public string? TurnoId => TurnosIds.FirstOrDefault();
    }

    public class CreatePaymentDto
    {
        // Soporte para múltiples turnos (nuevo)
        public List<string>? TurnosIds { get; set; }
        
        // Soporte para un solo turno (compatibilidad)
        public string? TurnoId { get; set; }

        [Required(ErrorMessage = "El monto es requerido")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "El método de pago es requerido")]
        public string MetodoPago { get; set; } = string.Empty;

        public PaymentDetails? PaymentDetails { get; set; }
        public string? Notas { get; set; }

        // Método para obtener la lista final de turnos
        public List<string> GetTurnosIds()
        {
            var turnos = new List<string>();
            
            if (TurnosIds != null && TurnosIds.Any())
            {
                turnos.AddRange(TurnosIds);
            }
            else if (!string.IsNullOrEmpty(TurnoId))
            {
                turnos.Add(TurnoId);
            }
            
            return turnos.Distinct().ToList();
        }
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

    // Extensiones para validación de PaymentDetails
    public static class PaymentDetailsExtensions
    {
        public static bool HasValue(this PaymentDetails? details)
        {
            return details != null && (
                !string.IsNullOrWhiteSpace(details.CardNumber) ||
                !string.IsNullOrWhiteSpace(details.CardHolder) ||
                details.ExpiryMonth.HasValue ||
                details.ExpiryYear.HasValue ||
                !string.IsNullOrWhiteSpace(details.BankName) ||
                !string.IsNullOrWhiteSpace(details.AuthorizationCode)
            );
        }
    }
} 