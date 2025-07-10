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
    public class TurnosController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<TurnosController> _logger;

        public TurnosController(MongoDbContext context, ILogger<TurnosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener turnos con filtros y paginación
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<TurnoDto>>> GetTurnos([FromQuery] TurnoFilterDto filters)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Construir filtros
                var filterBuilder = Builders<Turno>.Filter;
                var filter = filterBuilder.Empty;

                // Si no es admin, solo puede ver sus propios turnos como cliente
                if (!isAdmin && userRole != "profesional")
                {
                    filter = filterBuilder.Eq(t => t.ClienteId, userId);
                }
                // Si es profesional, puede ver turnos asignados a él
                else if (userRole == "profesional" && !isAdmin)
                {
                    filter = filterBuilder.Eq(t => t.ProfesionalId, userId);
                }
                // Si se especifica cliente en filtros y el usuario no es admin
                else if (!string.IsNullOrEmpty(filters.ClienteId) && !isAdmin)
                {
                    if (filters.ClienteId != userId)
                    {
                        return Forbid("No tienes permisos para ver turnos de otros usuarios");
                    }
                    filter = filterBuilder.And(filter, filterBuilder.Eq(t => t.ClienteId, filters.ClienteId));
                }

                // Aplicar filtros adicionales
                if (!string.IsNullOrEmpty(filters.ClienteId) && isAdmin)
                    filter = filterBuilder.And(filter, filterBuilder.Eq(t => t.ClienteId, filters.ClienteId));

                if (!string.IsNullOrEmpty(filters.ProfesionalId))
                    filter = filterBuilder.And(filter, filterBuilder.Eq(t => t.ProfesionalId, filters.ProfesionalId));

                if (!string.IsNullOrEmpty(filters.ServicioId))
                    filter = filterBuilder.And(filter, filterBuilder.Eq(t => t.ServicioId, filters.ServicioId));

                if (!string.IsNullOrEmpty(filters.Estado))
                    filter = filterBuilder.And(filter, filterBuilder.Eq(t => t.Estado, filters.Estado));

                if (filters.FechaDesde.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Gte(t => t.Fecha, filters.FechaDesde.Value));

                if (filters.FechaHasta.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Lte(t => t.Fecha, filters.FechaHasta.Value));

                // Contar total
                var totalCount = await _context.Turnos.CountDocumentsAsync(filter);

                // Obtener turnos paginados
                var turnos = await _context.Turnos
                    .Find(filter)
                    .Sort(Builders<Turno>.Sort.Descending(t => t.Fecha))
                    .Skip((filters.Page - 1) * filters.PageSize)
                    .Limit(filters.PageSize)
                    .ToListAsync();

                // Mapear a DTOs con información expandida
                var turnosDto = new List<TurnoDto>();
                foreach (var turno in turnos)
                {
                    var turnoDto = await MapToTurnoDtoAsync(turno);
                    turnosDto.Add(turnoDto);
                }

                var response = PaginatedResponse<TurnoDto>.SuccessResponse(
                    turnosDto, (int)totalCount, filters.Page, filters.PageSize, "Turnos obtenidos exitosamente");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener turnos");
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener turno por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<TurnoDto>>> GetTurno(string id)
        {
            try
            {
                var turno = await _context.Turnos.Find(t => t.Id == id).FirstOrDefaultAsync();
                
                if (turno == null)
                {
                    return NotFound(ApiResponse<TurnoDto>.ErrorResponse("Turno no encontrado"));
                }

                // Verificar permisos
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                if (!isAdmin && turno.ClienteId != userId && turno.ProfesionalId != userId)
                {
                    return Forbid("No tienes permisos para ver este turno");
                }

                var turnoDto = await MapToTurnoDtoAsync(turno);
                return Ok(ApiResponse<TurnoDto>.SuccessResponse(turnoDto, "Turno obtenido exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener turno: {Id}", id);
                return StatusCode(500, ApiResponse<TurnoDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Crear nuevo turno
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TurnoDto>>> CreateTurno([FromBody] CreateTurnoDto createTurnoDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Solo admins pueden crear turnos para otros usuarios
                if (!isAdmin && createTurnoDto.ClienteId != userId)
                {
                    return Forbid("No puedes crear turnos para otros usuarios");
                }

                // Verificar que el cliente existe
                var cliente = await _context.Users.Find(u => u.Id == createTurnoDto.ClienteId).FirstOrDefaultAsync();
                if (cliente == null)
                {
                    return BadRequest(ApiResponse<TurnoDto>.ErrorResponse("Cliente no encontrado"));
                }

                // Verificar que el servicio existe
                var servicio = await _context.Services.Find(s => s.Id == createTurnoDto.ServicioId).FirstOrDefaultAsync();
                if (servicio == null)
                {
                    return BadRequest(ApiResponse<TurnoDto>.ErrorResponse("Servicio no encontrado"));
                }

                // Verificar que el profesional existe
                var profesional = await _context.Users.Find(u => u.Id == createTurnoDto.ProfesionalId).FirstOrDefaultAsync();
                if (profesional == null)
                {
                    return BadRequest(ApiResponse<TurnoDto>.ErrorResponse("Profesional no encontrado"));
                }

                // Verificar disponibilidad (no hay otro turno en esa fecha/hora para el profesional)
                var existingTurno = await _context.Turnos
                    .Find(t => t.ProfesionalId == createTurnoDto.ProfesionalId && 
                              t.Fecha.Date == createTurnoDto.Fecha.Date && 
                              t.Hora == createTurnoDto.Hora &&
                              t.Estado != "cancelado")
                    .FirstOrDefaultAsync();

                if (existingTurno != null)
                {
                    return BadRequest(ApiResponse<TurnoDto>.ErrorResponse("El profesional ya tiene un turno en esa fecha y hora"));
                }

                // Crear turno
                var turno = new Turno
                {
                    ClienteId = createTurnoDto.ClienteId,
                    ServicioId = createTurnoDto.ServicioId,
                    ProfesionalId = createTurnoDto.ProfesionalId,
                    Fecha = createTurnoDto.Fecha,
                    Hora = createTurnoDto.Hora,
                    Estado = "pendiente",
                    Notas = createTurnoDto.Notas,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.Turnos.InsertOneAsync(turno);

                var turnoDto = await MapToTurnoDtoAsync(turno);
                _logger.LogInformation("Turno creado exitosamente: {Id}", turno.Id);

                return CreatedAtAction(nameof(GetTurno), new { id = turno.Id }, 
                    ApiResponse<TurnoDto>.SuccessResponse(turnoDto, "Turno creado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear turno");
                return StatusCode(500, ApiResponse<TurnoDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Actualizar turno existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<TurnoDto>>> UpdateTurno(string id, [FromBody] UpdateTurnoDto updateTurnoDto)
        {
            try
            {
                var turno = await _context.Turnos.Find(t => t.Id == id).FirstOrDefaultAsync();
                
                if (turno == null)
                {
                    return NotFound(ApiResponse<TurnoDto>.ErrorResponse("Turno no encontrado"));
                }

                // Verificar permisos
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

                _logger.LogInformation("UpdateTurno - UserId: {UserId}, IsAdmin: {IsAdmin}, Role: {Role}, Email: {Email}", 
                    userId, isAdmin, userRole, userEmail);

                if (!isAdmin && turno.ClienteId != userId && turno.ProfesionalId != userId)
                {
                    _logger.LogWarning("Usuario {UserId} no tiene permisos para modificar turno {TurnoId}", userId, id);
                    return Forbid("No tienes permisos para modificar este turno");
                }

                // No permitir cambios si el turno ya fue realizado (excepto para admins)
                if (turno.Estado == "realizado" && !isAdmin)
                {
                    return BadRequest(ApiResponse<TurnoDto>.ErrorResponse("No se puede modificar un turno ya realizado"));
                }

                // Construir actualización
                var updateBuilder = Builders<Turno>.Update.Set(t => t.UpdatedAt, DateTime.UtcNow);

                if (!string.IsNullOrEmpty(updateTurnoDto.ProfesionalId))
                {
                    // Verificar que el profesional existe
                    var profesional = await _context.Users.Find(u => u.Id == updateTurnoDto.ProfesionalId).FirstOrDefaultAsync();
                    if (profesional == null)
                    {
                        return BadRequest(ApiResponse<TurnoDto>.ErrorResponse("Profesional no encontrado"));
                    }
                    updateBuilder = updateBuilder.Set(t => t.ProfesionalId, updateTurnoDto.ProfesionalId);
                }

                if (updateTurnoDto.Fecha.HasValue)
                    updateBuilder = updateBuilder.Set(t => t.Fecha, updateTurnoDto.Fecha.Value);

                if (!string.IsNullOrEmpty(updateTurnoDto.Hora))
                    updateBuilder = updateBuilder.Set(t => t.Hora, updateTurnoDto.Hora);

                if (!string.IsNullOrEmpty(updateTurnoDto.Estado))
                    updateBuilder = updateBuilder.Set(t => t.Estado, updateTurnoDto.Estado);

                if (!string.IsNullOrWhiteSpace(updateTurnoDto.Notas))
                    updateBuilder = updateBuilder.Set(t => t.Notas, updateTurnoDto.Notas);

                if (updateTurnoDto.PrecioPagado.HasValue)
                    updateBuilder = updateBuilder.Set(t => t.PrecioPagado, updateTurnoDto.PrecioPagado.Value);

                await _context.Turnos.UpdateOneAsync(t => t.Id == id, updateBuilder);

                // Obtener turno actualizado
                var updatedTurno = await _context.Turnos.Find(t => t.Id == id).FirstOrDefaultAsync();
                var turnoDto = await MapToTurnoDtoAsync(updatedTurno!);

                _logger.LogInformation("Turno actualizado exitosamente: {Id}", id);
                return Ok(ApiResponse<TurnoDto>.SuccessResponse(turnoDto, "Turno actualizado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar turno: {Id}", id);
                return StatusCode(500, ApiResponse<TurnoDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Cancelar turno
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> CancelTurno(string id)
        {
            try
            {
                var turno = await _context.Turnos.Find(t => t.Id == id).FirstOrDefaultAsync();
                
                if (turno == null)
                {
                    return NotFound(ApiResponse<string>.ErrorResponse("Turno no encontrado"));
                }

                // Verificar permisos
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                if (!isAdmin && turno.ClienteId != userId)
                {
                    return Forbid("No tienes permisos para cancelar este turno");
                }

                // No permitir cancelar turnos ya realizados
                if (turno.Estado == "realizado")
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse("No se puede cancelar un turno ya realizado"));
                }

                // Actualizar estado a cancelado
                var update = Builders<Turno>.Update
                    .Set(t => t.Estado, "cancelado")
                    .Set(t => t.UpdatedAt, DateTime.UtcNow);

                await _context.Turnos.UpdateOneAsync(t => t.Id == id, update);

                _logger.LogInformation("Turno cancelado exitosamente: {Id}", id);
                return Ok(ApiResponse<string>.SuccessResponse("success", "Turno cancelado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar turno: {Id}", id);
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener horarios disponibles para un profesional en una fecha
        /// </summary>
        [HttpGet("disponibilidad")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetDisponibilidad(
            [FromQuery] string profesionalId, 
            [FromQuery] DateTime fecha)
        {
            try
            {
                // Horarios disponibles (esto podría venir de configuración)
                var horariosBase = new List<string>
                {
                    "09:00", "09:30", "10:00", "10:30", "11:00", "11:30",
                    "14:00", "14:30", "15:00", "15:30", "16:00", "16:30", "17:00", "17:30"
                };

                // Obtener turnos ocupados para esa fecha y profesional
                var turnosOcupados = await _context.Turnos
                    .Find(t => t.ProfesionalId == profesionalId && 
                              t.Fecha.Date == fecha.Date && 
                              t.Estado != "cancelado")
                    .ToListAsync();

                var horariosOcupados = turnosOcupados.Select(t => t.Hora).ToList();
                var horariosDisponibles = horariosBase.Except(horariosOcupados).ToList();

                return Ok(ApiResponse<List<string>>.SuccessResponse(
                    horariosDisponibles, "Horarios disponibles obtenidos exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener disponibilidad");
                return StatusCode(500, ApiResponse<List<string>>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Mapear Turno a TurnoDto con información expandida
        /// </summary>
        private async Task<TurnoDto> MapToTurnoDtoAsync(Turno turno)
        {
            var turnoDto = new TurnoDto
            {
                Id = turno.Id,
                ClienteId = turno.ClienteId,
                ServicioId = turno.ServicioId,
                ProfesionalId = turno.ProfesionalId,
                Fecha = turno.Fecha,
                Hora = turno.Hora,
                Estado = turno.Estado,
                CreatedAt = turno.CreatedAt,
                Notas = turno.Notas,
                PrecioPagado = turno.PrecioPagado
            };

            // Obtener información del cliente
            var cliente = await _context.Users.Find(u => u.Id == turno.ClienteId).FirstOrDefaultAsync();
            if (cliente != null)
            {
                turnoDto.Cliente = new UserDto
                {
                    Id = cliente.Id,
                    FirstName = cliente.FirstName,
                    LastName = cliente.LastName,
                    Email = cliente.Email,
                    Role = cliente.Role,
                    IsAdmin = cliente.IsAdmin,
                    CreatedAt = cliente.CreatedAt
                };
            }

            // Obtener información del servicio
            var servicio = await _context.Services.Find(s => s.Id == turno.ServicioId).FirstOrDefaultAsync();
            if (servicio != null)
            {
                turnoDto.Servicio = new ServiceDto
                {
                    Id = servicio.Id,
                    Nombre = servicio.Nombre,
                    Image = servicio.Image,
                    Tipo = servicio.Tipo,
                    Precio = servicio.Precio,
                    Descripcion = servicio.Descripcion,
                    CreatedAt = servicio.CreatedAt,
                    IsActive = servicio.IsActive
                };
            }

            // Obtener información del profesional
            var profesional = await _context.Users.Find(u => u.Id == turno.ProfesionalId).FirstOrDefaultAsync();
            if (profesional != null)
            {
                turnoDto.Profesional = new UserDto
                {
                    Id = profesional.Id,
                    FirstName = profesional.FirstName,
                    LastName = profesional.LastName,
                    Email = profesional.Email,
                    Role = profesional.Role,
                    IsAdmin = profesional.IsAdmin,
                    CreatedAt = profesional.CreatedAt
                };
            }

            return turnoDto;
        }
    }
} 