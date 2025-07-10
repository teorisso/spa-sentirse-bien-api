using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SentirseWellApi.Models
{
    [BsonIgnoreExtraElements] // Ignorar campos adicionales como __v de Mongoose
    public class Service
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("nombre")]
        [Required(ErrorMessage = "El nombre del servicio es requerido")]
        public string Nombre { get; set; } = string.Empty;

        [BsonElement("Image")]
        [Required(ErrorMessage = "La imagen del servicio es requerida")]
        public string Image { get; set; } = string.Empty;

        [BsonElement("tipo")]
        public string? Tipo { get; set; }

        [BsonElement("precio")]
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a 0")]
        public decimal? Precio { get; set; }

        [BsonElement("descripcion")]
        public string? Descripcion { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        // Campo de versión removido ya que [BsonIgnoreExtraElements] maneja esto automáticamente
        // [BsonElement("__v")]
        // public int? Version { get; set; }
    }

    public class ServiceDto
    {
        public string? Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string? Tipo { get; set; }
        public decimal? Precio { get; set; }
        public string? Descripcion { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateServiceDto
    {
        [Required(ErrorMessage = "El nombre del servicio es requerido")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La imagen del servicio es requerida")]
        public string Image { get; set; } = string.Empty;

        public string? Tipo { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a 0")]
        public decimal? Precio { get; set; }

        public string? Descripcion { get; set; }
    }

    public class UpdateServiceDto
    {
        public string? Nombre { get; set; }
        public string? Image { get; set; }
        public string? Tipo { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a 0")]
        public decimal? Precio { get; set; }
        
        public string? Descripcion { get; set; }
        public bool? IsActive { get; set; }
    }
} 