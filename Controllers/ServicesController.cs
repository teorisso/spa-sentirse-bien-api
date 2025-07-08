using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SentirseWellApi.Data;
using SentirseWellApi.Models;

namespace SentirseWellApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<ServicesController> _logger;

        public ServicesController(MongoDbContext context, ILogger<ServicesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener servicios con paginación y filtros
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<ServiceDto>>> GetServices(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? tipo = null,
            [FromQuery] string? search = null,
            [FromQuery] bool includeInactive = false)
        {
            try
            {
                // Validar parámetros de paginación
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                // Construir filtros
                var filterBuilder = Builders<Service>.Filter;
                var filter = filterBuilder.Empty;

                // Filtro por estado activo (incluir servicios sin el campo is_active)
                if (!includeInactive)
                {
                    var activeFilter = filterBuilder.Or(
                        filterBuilder.Eq(s => s.IsActive, true),
                        filterBuilder.Exists(s => s.IsActive, false) // Incluir documentos sin el campo is_active
                    );
                    filter = filterBuilder.And(filter, activeFilter);
                }

                // Filtro por tipo
                if (!string.IsNullOrEmpty(tipo))
                {
                    filter = filterBuilder.And(filter, filterBuilder.Eq(s => s.Tipo, tipo));
                }

                // Filtro por búsqueda en nombre o descripción
                if (!string.IsNullOrEmpty(search))
                {
                    var searchFilter = filterBuilder.Or(
                        filterBuilder.Regex(s => s.Nombre, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                        filterBuilder.Regex(s => s.Descripcion, new MongoDB.Bson.BsonRegularExpression(search, "i"))
                    );
                    filter = filterBuilder.And(filter, searchFilter);
                }

                // Contar total de elementos
                var totalCount = await _context.Services.CountDocumentsAsync(filter);

                // Obtener servicios paginados
                var services = await _context.Services
                    .Find(filter)
                    .Sort(Builders<Service>.Sort.Descending(s => s.CreatedAt))
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();

                // Mapear a DTOs
                var serviceDtos = services.Select(MapToServiceDto).ToList();

                var response = PaginatedResponse<ServiceDto>.SuccessResponse(
                    serviceDtos, (int)totalCount, page, pageSize, "Servicios obtenidos exitosamente");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener servicios");
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener servicio por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ServiceDto>>> GetServiceById(string id)
        {
            try
            {
                var service = await _context.Services
                    .Find(s => s.Id == id)
                    .FirstOrDefaultAsync();

                if (service == null)
                {
                    return NotFound(ApiResponse<ServiceDto>.ErrorResponse("Servicio no encontrado"));
                }

                var serviceDto = MapToServiceDto(service);
                return Ok(ApiResponse<ServiceDto>.SuccessResponse(serviceDto, "Servicio obtenido exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener servicio por ID: {Id}", id);
                return StatusCode(500, ApiResponse<ServiceDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener servicio por nombre
        /// </summary>
        [HttpGet("name/{nombre}")]
        public async Task<ActionResult<ApiResponse<ServiceDto>>> GetServiceByName(string nombre)
        {
            try
            {
                var service = await _context.Services
                    .Find(s => s.Nombre.ToLower() == nombre.ToLower())
                    .FirstOrDefaultAsync();

                if (service == null)
                {
                    return NotFound(ApiResponse<ServiceDto>.ErrorResponse("Servicio no encontrado"));
                }

                var serviceDto = MapToServiceDto(service);
                return Ok(ApiResponse<ServiceDto>.SuccessResponse(serviceDto, "Servicio obtenido exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener servicio por nombre: {Nombre}", nombre);
                return StatusCode(500, ApiResponse<ServiceDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Crear nuevo servicio
        /// </summary>
        [HttpPost]
        [Authorize] // Requiere autenticación
        public async Task<ActionResult<ApiResponse<ServiceDto>>> CreateService([FromBody] CreateServiceDto createServiceDto)
        {
            try
            {
                // Verificar si ya existe un servicio con el mismo nombre
                var existingService = await _context.Services
                    .Find(s => s.Nombre.ToLower() == createServiceDto.Nombre.ToLower())
                    .FirstOrDefaultAsync();

                if (existingService != null)
                {
                    return BadRequest(ApiResponse<ServiceDto>.ErrorResponse("Ya existe un servicio con ese nombre"));
                }

                // Crear nuevo servicio
                var service = new Service
                {
                    Nombre = createServiceDto.Nombre,
                    Image = createServiceDto.Image,
                    Tipo = createServiceDto.Tipo,
                    Precio = createServiceDto.Precio,
                    Descripcion = createServiceDto.Descripcion,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.Services.InsertOneAsync(service);

                var serviceDto = MapToServiceDto(service);
                _logger.LogInformation("Servicio creado exitosamente: {Nombre}", service.Nombre);

                return CreatedAtAction(nameof(GetServiceById), new { id = service.Id }, 
                    ApiResponse<ServiceDto>.SuccessResponse(serviceDto, "Servicio creado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear servicio");
                return StatusCode(500, ApiResponse<ServiceDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Actualizar servicio existente
        /// </summary>
        [HttpPut("{id}")]
        [Authorize] // Requiere autenticación
        public async Task<ActionResult<ApiResponse<ServiceDto>>> UpdateService(string id, [FromBody] UpdateServiceDto updateServiceDto)
        {
            try
            {
                var service = await _context.Services
                    .Find(s => s.Id == id)
                    .FirstOrDefaultAsync();

                if (service == null)
                {
                    return NotFound(ApiResponse<ServiceDto>.ErrorResponse("Servicio no encontrado"));
                }

                // Verificar si el nuevo nombre ya existe (si se está cambiando)
                if (!string.IsNullOrEmpty(updateServiceDto.Nombre) && 
                    updateServiceDto.Nombre.ToLower() != service.Nombre.ToLower())
                {
                    var existingService = await _context.Services
                        .Find(s => s.Nombre.ToLower() == updateServiceDto.Nombre.ToLower() && s.Id != id)
                        .FirstOrDefaultAsync();

                    if (existingService != null)
                    {
                        return BadRequest(ApiResponse<ServiceDto>.ErrorResponse("Ya existe un servicio con ese nombre"));
                    }
                }

                // Actualizar campos
                var updateBuilder = Builders<Service>.Update
                    .Set(s => s.UpdatedAt, DateTime.UtcNow);

                if (!string.IsNullOrEmpty(updateServiceDto.Nombre))
                    updateBuilder = updateBuilder.Set(s => s.Nombre, updateServiceDto.Nombre);

                if (!string.IsNullOrEmpty(updateServiceDto.Image))
                    updateBuilder = updateBuilder.Set(s => s.Image, updateServiceDto.Image);

                if (updateServiceDto.Tipo != null)
                    updateBuilder = updateBuilder.Set(s => s.Tipo, updateServiceDto.Tipo);

                if (updateServiceDto.Precio.HasValue)
                    updateBuilder = updateBuilder.Set(s => s.Precio, updateServiceDto.Precio);

                if (updateServiceDto.Descripcion != null)
                    updateBuilder = updateBuilder.Set(s => s.Descripcion, updateServiceDto.Descripcion);

                if (updateServiceDto.IsActive.HasValue)
                    updateBuilder = updateBuilder.Set(s => s.IsActive, updateServiceDto.IsActive.Value);

                await _context.Services.UpdateOneAsync(s => s.Id == id, updateBuilder);

                // Obtener servicio actualizado
                var updatedService = await _context.Services
                    .Find(s => s.Id == id)
                    .FirstOrDefaultAsync();

                var serviceDto = MapToServiceDto(updatedService!);
                _logger.LogInformation("Servicio actualizado exitosamente: {Id}", id);

                return Ok(ApiResponse<ServiceDto>.SuccessResponse(serviceDto, "Servicio actualizado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar servicio: {Id}", id);
                return StatusCode(500, ApiResponse<ServiceDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Eliminar servicio (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize] // Requiere autenticación
        public async Task<ActionResult<ApiResponse<string>>> DeleteService(string id)
        {
            try
            {
                var service = await _context.Services
                    .Find(s => s.Id == id)
                    .FirstOrDefaultAsync();

                if (service == null)
                {
                    return NotFound(ApiResponse<string>.ErrorResponse("Servicio no encontrado"));
                }

                // Soft delete - marcar como inactivo
                var update = Builders<Service>.Update
                    .Set(s => s.IsActive, false)
                    .Set(s => s.UpdatedAt, DateTime.UtcNow);

                await _context.Services.UpdateOneAsync(s => s.Id == id, update);

                _logger.LogInformation("Servicio eliminado exitosamente: {Id}", id);
                return Ok(ApiResponse<string>.SuccessResponse("success", "Servicio eliminado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar servicio: {Id}", id);
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener tipos de servicios únicos
        /// </summary>
        [HttpGet("tipos")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetServiceTypes()
        {
            try
            {
                var tipos = await _context.Services
                    .Find(s => !string.IsNullOrEmpty(s.Tipo))
                    .Project(s => s.Tipo)
                    .ToListAsync();

                var tiposUnicos = tipos.Where(t => !string.IsNullOrEmpty(t)).Cast<string>().Distinct().ToList();
                
                return Ok(ApiResponse<List<string>>.SuccessResponse(tiposUnicos, "Tipos de servicios obtenidos exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tipos de servicios");
                return StatusCode(500, ApiResponse<List<string>>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Mapear Service a ServiceDto
        /// </summary>
        private static ServiceDto MapToServiceDto(Service service)
        {
            return new ServiceDto
            {
                Id = service.Id,
                Nombre = service.Nombre,
                Image = service.Image,
                Tipo = service.Tipo,
                Precio = service.Precio,
                Descripcion = service.Descripcion,
                CreatedAt = service.CreatedAt,
                IsActive = service.IsActive
            };
        }
    }
} 