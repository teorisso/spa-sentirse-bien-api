using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SentirseWellApi.Data;
using SentirseWellApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.Extensions.Options;

namespace SentirseWellApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            MongoDbContext context, 
            IOptions<JwtSettings> jwtSettings,
            ILogger<AuthController> logger)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                // Verificar si el usuario ya existe
                var existingUser = await _context.Users
                    .Find(u => u.Email == registerDto.Email.ToLower())
                    .FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    return BadRequest(ApiResponse<AuthResponse>.ErrorResponse(
                        "El usuario ya existe con este email"));
                }

                // Crear nuevo usuario
                var user = new User
                {
                    FirstName = registerDto.FirstName,
                    LastName = registerDto.LastName,
                    Email = registerDto.Email.ToLower(),
                    Password = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                    Role = registerDto.Role.ToLower(),
                    IsAdmin = registerDto.Role.ToLower() == "admin",
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Users.InsertOneAsync(user);

                // Generar token JWT
                var token = GenerateJwtToken(user);
                var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes);

                var userDto = new UserDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = user.Role,
                    IsAdmin = user.IsAdmin,
                    CreatedAt = user.CreatedAt
                };

                var authResponse = new AuthResponse
                {
                    Token = token,
                    ExpiresAt = expiresAt,
                    User = userDto
                };

                _logger.LogInformation("Usuario registrado exitosamente: {Email}", user.Email);

                return Ok(ApiResponse<AuthResponse>.SuccessResponse(
                    authResponse, "Usuario registrado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar usuario: {Email}", registerDto.Email);
                return StatusCode(500, ApiResponse<AuthResponse>.ErrorResponse(
                    "Error interno del servidor"));
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                _logger.LogInformation("Intento de login para email: {Email}", loginDto.Email);

                // Buscar usuario por email
                var user = await _context.Users
                    .Find(u => u.Email == loginDto.Email.ToLower())
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("Usuario no encontrado: {Email}", loginDto.Email);
                    return BadRequest(ApiResponse<AuthResponse>.ErrorResponse(
                        "Credenciales inválidas"));
                }

                bool passwordValid = false;

                // COMPATIBILIDAD: Verificar diferentes tipos de hash de contraseña
                if (user.Password.StartsWith("$2a$") || user.Password.StartsWith("$2b$") || user.Password.StartsWith("$2y$"))
                {
                    // Hash de bcrypt (incluyendo los del backend Node.js anterior)
                    try
                    {
                        passwordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password);
                        _logger.LogInformation("Verificación bcrypt para {Email}: {Valid}", loginDto.Email, passwordValid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error verificando hash bcrypt para {Email}", loginDto.Email);
                        
                        // FALLBACK: Si BCrypt.Net falla, intentar verificación manual
                        // Esto puede pasar con hashes de versiones diferentes de bcrypt
                        passwordValid = false;
                    }
                }
                else if (user.Password.Length < 50 && !user.Password.Contains("$"))
                {
                    // TEMPORAL: Password en texto plano (migración de datos legacy)
                    passwordValid = user.Password == loginDto.Password;
                    _logger.LogWarning("Usuario {Email} tiene password en texto plano - requiere migración", loginDto.Email);
                    
                    // Auto-migrar a hash bcrypt
                    if (passwordValid)
                    {
                        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(loginDto.Password);
                        var update = Builders<User>.Update.Set(u => u.Password, hashedPassword);
                        await _context.Users.UpdateOneAsync(u => u.Id == user.Id, update);
                        _logger.LogInformation("Password migrado a hash bcrypt para {Email}", loginDto.Email);
                    }
                }
                else
                {
                    _logger.LogWarning("Formato de password no reconocido para {Email}", loginDto.Email);
                    passwordValid = false;
                }

                if (!passwordValid)
                {
                    _logger.LogWarning("Credenciales inválidas para {Email}", loginDto.Email);
                    return BadRequest(ApiResponse<AuthResponse>.ErrorResponse(
                        "Credenciales inválidas"));
                }

                // Generar token JWT
                var token = GenerateJwtToken(user);
                var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes);

                var userDto = new UserDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = user.Role,
                    IsAdmin = user.IsAdmin,
                    CreatedAt = user.CreatedAt
                };

                var authResponse = new AuthResponse
                {
                    Token = token,
                    ExpiresAt = expiresAt,
                    User = userDto
                };

                _logger.LogInformation("Usuario logueado exitosamente: {Email}", user.Email);

                return Ok(ApiResponse<AuthResponse>.SuccessResponse(
                    authResponse, "Login exitoso"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al hacer login: {Email}", loginDto.Email);
                return StatusCode(500, ApiResponse<AuthResponse>.ErrorResponse(
                    "Error interno del servidor"));
            }
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult<ApiResponse<string>>> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            try
            {
                // Buscar usuario por email
                var user = await _context.Users
                    .Find(u => u.Email == forgotPasswordDto.Email.ToLower())
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    // Por seguridad, no revelar si el email existe o no
                    return Ok(ApiResponse<string>.SuccessResponse(
                        "success", "Si el email existe, se ha enviado un enlace de recuperación"));
                }

                // Generar token de recuperación (válido por 1 hora)
                var resetToken = Guid.NewGuid().ToString();
                var resetTokenExpires = DateTime.UtcNow.AddHours(1);

                // Actualizar usuario con token de recuperación
                var update = Builders<User>.Update
                    .Set(u => u.PasswordResetToken, resetToken)
                    .Set(u => u.PasswordResetExpires, resetTokenExpires);

                await _context.Users.UpdateOneAsync(
                    u => u.Id == user.Id, update);

                // TODO: Enviar email con el token de recuperación
                // El enlace sería algo como: https://frontend.com/reset-password?token={resetToken}

                _logger.LogInformation("Token de recuperación generado para: {Email}", user.Email);

                return Ok(ApiResponse<string>.SuccessResponse(
                    "success", "Si el email existe, se ha enviado un enlace de recuperación"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar token de recuperación: {Email}", forgotPasswordDto.Email);
                return StatusCode(500, ApiResponse<string>.ErrorResponse(
                    "Error interno del servidor"));
            }
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult<ApiResponse<string>>> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            try
            {
                // Buscar usuario por token de recuperación
                var user = await _context.Users
                    .Find(u => u.PasswordResetToken == resetPasswordDto.Token && 
                              u.PasswordResetExpires > DateTime.UtcNow)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse(
                        "Token de recuperación inválido o expirado"));
                }

                // Actualizar contraseña y limpiar token de recuperación
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
                var update = Builders<User>.Update
                    .Set(u => u.Password, hashedPassword)
                    .Unset(u => u.PasswordResetToken)
                    .Unset(u => u.PasswordResetExpires);

                await _context.Users.UpdateOneAsync(
                    u => u.Id == user.Id, update);

                _logger.LogInformation("Contraseña restablecida exitosamente para: {Email}", user.Email);

                return Ok(ApiResponse<string>.SuccessResponse(
                    "success", "Contraseña restablecida exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer contraseña");
                return StatusCode(500, ApiResponse<string>.ErrorResponse(
                    "Error interno del servidor"));
            }
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id!),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new(ClaimTypes.Role, user.Role),
                new("isAdmin", user.IsAdmin.ToString().ToLower())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
} 