using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SentirseWellApi.Data;
using SentirseWellApi.Models;
using System.Security.Claims;

namespace SentirseWellApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(MongoDbContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener usuarios con filtros
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetUsers([FromQuery] string? role = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Solo admins pueden obtener listas de usuarios
                if (!isAdmin)
                {
                    return Forbid("No tienes permisos para acceder a esta información");
                }

                // Construir filtro
                var filterBuilder = Builders<User>.Filter;
                var filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(role))
                {
                    filter = filterBuilder.Eq(u => u.Role, role.ToLower());
                }

                // Obtener usuarios
                var users = await _context.Users
                    .Find(filter)
                    .Sort(Builders<User>.Sort.Ascending(u => u.FirstName))
                    .ToListAsync();

                // Mapear a DTOs (sin incluir información sensible)
                var usersDto = users.Select(u => new UserDto
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role,
                    IsAdmin = u.IsAdmin,
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(ApiResponse<List<UserDto>>.SuccessResponse(usersDto, "Usuarios obtenidos exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios");
                return StatusCode(500, ApiResponse<List<UserDto>>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener profesionales específicamente
        /// </summary>
        [HttpGet("profesionales")]
        [AllowAnonymous] // Permitir acceso sin autenticación para seleccionar profesionales
        public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetProfesionales()
        {
            try
            {
                // Obtener solo usuarios con rol de profesional
                var profesionales = await _context.Users
                    .Find(u => u.Role == "profesional")
                    .Sort(Builders<User>.Sort.Ascending(u => u.FirstName))
                    .ToListAsync();

                // Mapear a DTOs (sin información sensible)
                var profesionalesDto = profesionales.Select(u => new UserDto
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role,
                    IsAdmin = u.IsAdmin,
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(ApiResponse<List<UserDto>>.SuccessResponse(profesionalesDto, "Profesionales obtenidos exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener profesionales");
                return StatusCode(500, ApiResponse<List<UserDto>>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener clientes específicamente (solo admins)
        /// </summary>
        [HttpGet("clientes")]
        public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetClientes()
        {
            try
            {
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Solo admins pueden obtener lista de clientes
                if (!isAdmin)
                {
                    return Forbid("No tienes permisos para acceder a esta información");
                }

                // Obtener solo usuarios con rol de cliente
                var clientes = await _context.Users
                    .Find(u => u.Role == "cliente")
                    .Sort(Builders<User>.Sort.Ascending(u => u.FirstName))
                    .ToListAsync();

                // Mapear a DTOs (sin información sensible)
                var clientesDto = clientes.Select(u => new UserDto
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role,
                    IsAdmin = u.IsAdmin,
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(ApiResponse<List<UserDto>>.SuccessResponse(clientesDto, "Clientes obtenidos exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clientes");
                return StatusCode(500, ApiResponse<List<UserDto>>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener usuario por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(string id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Solo admins pueden ver otros usuarios, o el usuario puede ver su propia información
                if (!isAdmin && userId != id)
                {
                    return Forbid("No tienes permisos para acceder a esta información");
                }

                var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
                
                if (user == null)
                {
                    return NotFound(ApiResponse<UserDto>.ErrorResponse("Usuario no encontrado"));
                }

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

                return Ok(ApiResponse<UserDto>.SuccessResponse(userDto, "Usuario obtenido exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario: {Id}", id);
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener perfil del usuario actual
        /// </summary>
        [HttpGet("profile")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<UserDto>.ErrorResponse("Usuario no autenticado"));
                }

                var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
                
                if (user == null)
                {
                    return NotFound(ApiResponse<UserDto>.ErrorResponse("Usuario no encontrado"));
                }

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

                return Ok(ApiResponse<UserDto>.SuccessResponse(userDto, "Perfil obtenido exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener perfil del usuario");
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Actualizar usuario por ID (solo admins o el mismo usuario)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(string id, [FromBody] UpdateUserDto updateDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Solo admins pueden editar otros usuarios, o el usuario puede editar su propia información
                if (!isAdmin && userId != id)
                {
                    return Forbid("No tienes permisos para editar este usuario");
                }

                // Validar el modelo
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<UserDto>.ErrorResponse("Datos de entrada inválidos"));
                }

                // Buscar el usuario
                var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
                
                if (user == null)
                {
                    return NotFound(ApiResponse<UserDto>.ErrorResponse("Usuario no encontrado"));
                }

                // Verificar si el email ya existe (si se está cambiando)
                if (!string.IsNullOrEmpty(updateDto.Email) && updateDto.Email != user.Email)
                {
                    var existingUser = await _context.Users.Find(u => u.Email == updateDto.Email).FirstOrDefaultAsync();
                    if (existingUser != null)
                    {
                        return BadRequest(ApiResponse<UserDto>.ErrorResponse("El email ya está en uso"));
                    }
                }

                // Actualizar campos
                if (!string.IsNullOrEmpty(updateDto.FirstName))
                    user.FirstName = updateDto.FirstName;
                
                if (!string.IsNullOrEmpty(updateDto.LastName))
                    user.LastName = updateDto.LastName;
                
                if (!string.IsNullOrEmpty(updateDto.Email))
                    user.Email = updateDto.Email;

                // Solo admins pueden cambiar roles
                if (isAdmin && !string.IsNullOrEmpty(updateDto.Role))
                    user.Role = updateDto.Role;

                user.UpdatedAt = DateTime.UtcNow;

                // Guardar cambios
                await _context.Users.ReplaceOneAsync(u => u.Id == id, user);

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

                return Ok(ApiResponse<UserDto>.SuccessResponse(userDto, "Usuario actualizado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario: {Id}", id);
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Eliminar usuario por ID (solo admins)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> DeleteUser(string id)
        {
            try
            {
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Solo admins pueden eliminar usuarios
                if (!isAdmin)
                {
                    return Forbid("No tienes permisos para eliminar usuarios");
                }

                // Buscar el usuario
                var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
                
                if (user == null)
                {
                    return NotFound(ApiResponse<string>.ErrorResponse("Usuario no encontrado"));
                }

                // No permitir eliminar admins por seguridad
                if (user.IsAdmin)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse("No se puede eliminar un administrador"));
                }

                // Verificar si el usuario tiene turnos activos
                var turnosActivos = await _context.Turnos
                    .Find(t => (t.ClienteId == id || t.ProfesionalId == id) && t.Estado != "cancelado")
                    .CountDocumentsAsync();

                if (turnosActivos > 0)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse("No se puede eliminar el usuario porque tiene turnos activos"));
                }

                // Eliminar el usuario
                await _context.Users.DeleteOneAsync(u => u.Id == id);

                return Ok(ApiResponse<string>.SuccessResponse("Usuario eliminado exitosamente", "Usuario eliminado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar usuario: {Id}", id);
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Error interno del servidor"));
            }
        }
    }
} 