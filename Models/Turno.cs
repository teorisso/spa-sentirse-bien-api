using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SentirseWellApi.Models
{
    [BsonIgnoreExtraElements] // Ignorar campos adicionales como __v de Mongoose
    public class Turno
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("cliente")]
        [BsonRepresentation(BsonType.ObjectId)]
        [Required(ErrorMessage = "El cliente es requerido")]
        public string ClienteId { get; set; } = string.Empty;

        [BsonElement("servicio")]
        [BsonRepresentation(BsonType.ObjectId)]
        [Required(ErrorMessage = "El servicio es requerido")]
        public string ServicioId { get; set; } = string.Empty;

        [BsonElement("profesional")]
        [BsonRepresentation(BsonType.ObjectId)]
        [Required(ErrorMessage = "El profesional es requerido")]
        public string ProfesionalId { get; set; } = string.Empty;

        [BsonElement("fecha")]
        [Required(ErrorMessage = "La fecha es requerida")]
        public DateTime Fecha { get; set; }

        [BsonElement("hora")]
        [Required(ErrorMessage = "La hora es requerida")]
        public string Hora { get; set; } = string.Empty;

        [BsonElement("estado")]
        public string Estado { get; set; } = "pendiente";

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("notas")]
        public string? Notas { get; set; }

        [BsonElement("precio_pagado")]
        public decimal? PrecioPagado { get; set; }

        // Propiedades de navegación (no se almacenan en BD)
        [BsonIgnore]
        public User? Cliente { get; set; }

        [BsonIgnore]
        public Service? Servicio { get; set; }

        [BsonIgnore]
        public User? Profesional { get; set; }
    }

    public enum EstadoTurno
    {
        Pendiente,
        Confirmado,
        Cancelado,
        Realizado
    }

    public class TurnoDto
    {
        public string? Id { get; set; }
        public string ClienteId { get; set; } = string.Empty;
        public string ServicioId { get; set; } = string.Empty;
        public string ProfesionalId { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Hora { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Notas { get; set; }
        public decimal? PrecioPagado { get; set; }

        // Información expandida
        public UserDto? Cliente { get; set; }
        public ServiceDto? Servicio { get; set; }
        public UserDto? Profesional { get; set; }
    }

    public class CreateTurnoDto
    {
        [Required(ErrorMessage = "El cliente es requerido")]
        public string ClienteId { get; set; } = string.Empty;

        [Required(ErrorMessage = "El servicio es requerido")]
        public string ServicioId { get; set; } = string.Empty;

        [Required(ErrorMessage = "El profesional es requerido")]
        public string ProfesionalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha es requerida")]
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "La hora es requerida")]
        public string Hora { get; set; } = string.Empty;

        public string? Notas { get; set; }
    }

    public class UpdateTurnoDto
    {
        public string? ProfesionalId { get; set; }
        public DateTime? Fecha { get; set; }
        public string? Hora { get; set; }
        public string? Estado { get; set; }
        public string? Notas { get; set; }
        public decimal? PrecioPagado { get; set; }
    }

    public class TurnoFilterDto
    {
        public string? ClienteId { get; set; }
        public string? ProfesionalId { get; set; }
        public string? ServicioId { get; set; }
        public string? Estado { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
} 