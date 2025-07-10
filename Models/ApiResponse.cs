using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SentirseWellApi.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponse<T> SuccessResponse(T data, string message = "Operación exitosa")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResponse(string message, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }

    public class PaginatedResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<T> Data { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNext => Page < TotalPages;
        public bool HasPrevious => Page > 1;

        public static PaginatedResponse<T> SuccessResponse(List<T> data, int totalCount, int page, int pageSize, string message = "Operación exitosa")
        {
            return new PaginatedResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
    }

    [BsonIgnoreExtraElements] // Ignorar campos adicionales como __v de Mongoose
    public class QRCode
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("token")]
        [Required]
        public string Token { get; set; } = string.Empty;

        [BsonElement("user_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? UserId { get; set; }

        [BsonElement("turno_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? TurnoId { get; set; }

        [BsonElement("action")]
        [Required]
        public string Action { get; set; } = string.Empty; // "payment", "confirmation", "check_in", etc.

        [BsonElement("data")]
        public Dictionary<string, object>? Data { get; set; }

        [BsonElement("expires_at")]
        [Required]
        public DateTime ExpiresAt { get; set; }

        [BsonElement("used_at")]
        public DateTime? UsedAt { get; set; }

        [BsonElement("is_used")]
        public bool IsUsed { get; set; } = false;

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("created_by")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? CreatedBy { get; set; }

        public bool IsValid => !IsUsed && DateTime.UtcNow < ExpiresAt;
    }

    public class QRCodeDto
    {
        public string? Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? TurnoId { get; set; }
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, object>? Data { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsValid { get; set; }
    }

    public class CreateQRCodeDto
    {
        [Required(ErrorMessage = "La acción es requerida")]
        public string Action { get; set; } = string.Empty;

        public string? UserId { get; set; }
        public string? TurnoId { get; set; }
        public Dictionary<string, object>? Data { get; set; }

        [Range(1, 1440, ErrorMessage = "Los minutos de expiración deben estar entre 1 y 1440 (24 horas)")]
        public int ExpirationMinutes { get; set; } = 60; // Por defecto 1 hora
    }

    public class QRCodeResponse
    {
        public string QRCodeId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string QRCodeImageBase64 { get; set; } = string.Empty;
        public string QRCodeUrl { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = new();
    }

    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int DurationInMinutes { get; set; } = 60;
    }
} 