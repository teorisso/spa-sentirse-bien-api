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
    }
} 