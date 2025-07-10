using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SentirseWellApi.Models
{
    [BsonIgnoreExtraElements] // Ignorar campos adicionales como __v de Mongoose
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("first_name")]
        [Required(ErrorMessage = "El nombre es requerido")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("last_name")]
        [Required(ErrorMessage = "El apellido es requerido")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("email")]
        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("password")]
        [Required(ErrorMessage = "La contraseña es requerida")]
        public string Password { get; set; } = string.Empty;

        [BsonElement("role")]
        public string Role { get; set; } = "cliente";

        [BsonElement("is_admin")]
        public bool IsAdmin { get; set; } = false;

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("password_reset_token")]
        public string? PasswordResetToken { get; set; }

        [BsonElement("password_reset_expires")]
        public DateTime? PasswordResetExpires { get; set; }

        // Campo de versión de Mongoose (MongoDB)
        [BsonElement("__v")]
        public int? Version { get; set; }

        // Método para verificar si es admin
        public bool IsAdministrator => IsAdmin || Role.ToLower() == "admin";

        // Método para verificar si es profesional
        public bool IsProfessional => Role.ToLower() == "profesional";
    }

    public class UserDto
    {
        public string? Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RegisterDto
    {
        [Required(ErrorMessage = "El nombre es requerido")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es requerido")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "cliente";
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida")]
        public string Password { get; set; } = string.Empty;
    }

    public class ForgotPasswordDto
    {
        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "El token es requerido")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es requerida")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UpdateUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        public string? Email { get; set; }
        
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
        public string? Password { get; set; }
        
        public string? Role { get; set; }
    }

    public class TestEmailDto
    {
        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        public string Email { get; set; } = string.Empty;
    }
} 