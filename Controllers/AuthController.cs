using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SentirseWellApi.Data;
using SentirseWellApi.Models;
using SentirseWellApi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;

namespace SentirseWellApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthController> _logger;
        private readonly IEmailService _emailService;

        public AuthController(
            MongoDbContext context, 
            IOptions<JwtSettings> jwtSettings,
            ILogger<AuthController> logger,
            IEmailService emailService)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
            _emailService = emailService;
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

                // Enviar email con el token de recuperación
                var emailSent = await _emailService.SendPasswordResetAsync(user, resetToken);
                
                if (emailSent)
                {
                    _logger.LogInformation("Email de recuperación enviado exitosamente para: {Email}", user.Email);
                    return Ok(ApiResponse<string>.SuccessResponse(
                        "success", "Se ha enviado un enlace de recuperación a tu email"));
                }
                else
                {
                    _logger.LogError("Error al enviar email de recuperación para: {Email}", user.Email);
                    return StatusCode(500, ApiResponse<string>.ErrorResponse(
                        "Error al enviar el email de recuperación. Por favor, inténtalo nuevamente."));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar token de recuperación: {Email}", forgotPasswordDto.Email);
                return StatusCode(500, ApiResponse<string>.ErrorResponse(
                    "Error interno del servidor"));
            }
        }

        [HttpPost("test-email")]
        public async Task<ActionResult<ApiResponse<string>>> TestEmail([FromBody] TestEmailDto testEmailDto)
        {
            try
            {
                _logger.LogInformation("Probando envío de email a: {Email}", testEmailDto.Email);
                
                var testUser = new User
                {
                    FirstName = "Test",
                    LastName = "User",
                    Email = testEmailDto.Email
                };

                var emailSent = await _emailService.SendPasswordResetAsync(testUser, "test-token-123");
                
                if (emailSent)
                {
                    _logger.LogInformation("Email de prueba enviado exitosamente a: {Email}", testEmailDto.Email);
                    return Ok(ApiResponse<string>.SuccessResponse(
                        "success", "Email de prueba enviado exitosamente"));
                }
                else
                {
                    _logger.LogError("Error al enviar email de prueba a: {Email}", testEmailDto.Email);
                    return StatusCode(500, ApiResponse<string>.ErrorResponse(
                        "Error al enviar email de prueba"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en prueba de email: {Email}", testEmailDto.Email);
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

        [HttpPost("google")]
        public async Task<ActionResult<ApiResponse<AuthResponse>>> GoogleAuth([FromBody] GoogleAuthDto googleAuthDto)
        {
            try
            {
                _logger.LogInformation("Iniciando autenticación con Google");

                var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
                
                if (string.IsNullOrEmpty(clientId))
                {
                    _logger.LogError("GOOGLE_CLIENT_ID no está configurado");
                    return StatusCode(500, ApiResponse<AuthResponse>.ErrorResponse(
                        "Error de configuración del servidor"));
                }

                _logger.LogInformation("Validando token de Google con ClientId: {ClientId}", clientId);

                GoogleJsonWebSignature.Payload payload;
                try
                {
                    payload = await GoogleJsonWebSignature.ValidateAsync(googleAuthDto.IdToken, new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { clientId }
                    });
                }
                catch (InvalidJwtException ex)
                {
                    _logger.LogError(ex, "Token de Google inválido");
                    return BadRequest(ApiResponse<AuthResponse>.ErrorResponse(
                        "Token de Google inválido o expirado"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validando token de Google");
                    return BadRequest(ApiResponse<AuthResponse>.ErrorResponse(
                        "Error validando credenciales de Google"));
                }

                _logger.LogInformation("Token de Google válido para email: {Email}", payload.Email);

                // Buscar usuario existente por email
                var existingUser = await _context.Users
                    .Find(u => u.Email == payload.Email.ToLower())
                    .FirstOrDefaultAsync();

                User user;

                if (existingUser != null)
                {
                    // Usuario existe, actualizar información si es necesario
                    user = existingUser;
                    _logger.LogInformation("Usuario existente encontrado: {Email}", user.Email);
                }
                else
                {
                    // Crear nuevo usuario
                    user = new User
                    {
                        FirstName = payload.GivenName ?? "Usuario",
                        LastName = payload.FamilyName ?? "Google",
                        Email = payload.Email.ToLower(),
                        Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // Password aleatorio
                        Role = "cliente", // Por defecto
                        IsAdmin = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Users.InsertOneAsync(user);
                    _logger.LogInformation("Nuevo usuario creado con Google: {Email}", user.Email);

                    // Enviar email de bienvenida (opcional, no bloquear si falla)
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(user);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "Error enviando email de bienvenida para {Email}", user.Email);
                    }
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

                _logger.LogInformation("Autenticación con Google exitosa: {Email}", user.Email);

                return Ok(ApiResponse<AuthResponse>.SuccessResponse(
                    authResponse, "Autenticación con Google exitosa"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en autenticación con Google");
                return StatusCode(500, ApiResponse<AuthResponse>.ErrorResponse(
                    "Error interno del servidor"));
            }
        }

        private async Task<GoogleUserInfo?> ValidateGoogleTokenAsync(string idToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                
                // Obtener las claves públicas de Google
                var keysResponse = await httpClient.GetStringAsync(
                    "https://www.googleapis.com/oauth2/v3/certs");
                
                var keys = JsonSerializer.Deserialize<GoogleKeysResponse>(keysResponse);
                
                if (keys?.Keys == null || !keys.Keys.Any())
                {
                    _logger.LogError("No se pudieron obtener las claves públicas de Google");
                    return null;
                }

                // Validar el token con las claves públicas
                var tokenHandler = new JwtSecurityTokenHandler();
                
                foreach (var key in keys.Keys)
                {
                    try
                    {
                        var validationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = "https://accounts.google.com",
                            ValidateAudience = true,
                            ValidAudience = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"),
                            ValidateLifetime = true,
                            IssuerSigningKey = new JsonWebKey(key.N),
                            ValidateIssuerSigningKey = true
                        };

                        var principal = tokenHandler.ValidateToken(idToken, validationParameters, out var validatedToken);
                        
                        if (validatedToken != null)
                        {
                            // Extraer información del usuario del token
                            var email = principal.FindFirst("email")?.Value;
                            var name = principal.FindFirst("name")?.Value;
                            var givenName = principal.FindFirst("given_name")?.Value;
                            var familyName = principal.FindFirst("family_name")?.Value;
                            var picture = principal.FindFirst("picture")?.Value;
                            var emailVerified = principal.FindFirst("email_verified")?.Value == "true";

                            if (!string.IsNullOrEmpty(email))
                            {
                                return new GoogleUserInfo
                                {
                                    Sub = principal.FindFirst("sub")?.Value ?? "",
                                    Name = name ?? "",
                                    GivenName = givenName ?? "",
                                    FamilyName = familyName ?? "",
                                    Email = email,
                                    Picture = picture ?? "",
                                    EmailVerified = emailVerified
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error validando token con clave específica");
                        continue;
                    }
                }

                _logger.LogWarning("No se pudo validar el token de Google con ninguna clave");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando token de Google");
                return null;
            }
        }
    }

    public class GoogleKeysResponse
    {
        public List<GoogleKey> Keys { get; set; } = new();
    }

    public class GoogleKey
    {
        public string Kid { get; set; } = string.Empty;
        public string N { get; set; } = string.Empty;
        public string E { get; set; } = string.Empty;
        public string Kty { get; set; } = string.Empty;
        public string Alg { get; set; } = string.Empty;
        public string Use { get; set; } = string.Empty;
    }
} 