using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SentirseWellApi.Data;
using SentirseWellApi.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace SentirseWellApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(MongoDbContext context, ILogger<PaymentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener pagos con filtros y paginación
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<PaymentDto>>> GetPayments([FromQuery] PaymentFilterDto filters)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Construir filtros
                var filterBuilder = Builders<Payment>.Filter;
                var filter = filterBuilder.Empty;

                // Si no es admin, solo puede ver sus propios pagos
                if (!isAdmin)
                {
                    filter = filterBuilder.Eq(p => p.ClienteId, userId);
                }
                else if (!string.IsNullOrEmpty(filters.ClienteId))
                {
                    filter = filterBuilder.And(filter, filterBuilder.Eq(p => p.ClienteId, filters.ClienteId));
                }

                // Aplicar filtros adicionales
                if (!string.IsNullOrEmpty(filters.Estado))
                    filter = filterBuilder.And(filter, filterBuilder.Eq(p => p.Estado, filters.Estado));

                if (!string.IsNullOrEmpty(filters.MetodoPago))
                    filter = filterBuilder.And(filter, filterBuilder.Eq(p => p.MetodoPago, filters.MetodoPago));

                if (filters.FechaDesde.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Gte(p => p.CreatedAt, filters.FechaDesde.Value));

                if (filters.FechaHasta.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Lte(p => p.CreatedAt, filters.FechaHasta.Value));

                if (filters.MontoMinimo.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Gte(p => p.Monto, filters.MontoMinimo.Value));

                if (filters.MontoMaximo.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Lte(p => p.Monto, filters.MontoMaximo.Value));

                // Contar total
                var totalCount = await _context.Payments.CountDocumentsAsync(filter);

                // Obtener pagos paginados
                var payments = await _context.Payments
                    .Find(filter)
                    .Sort(Builders<Payment>.Sort.Descending(p => p.CreatedAt))
                    .Skip((filters.Page - 1) * filters.PageSize)
                    .Limit(filters.PageSize)
                    .ToListAsync();

                // Mapear a DTOs con información expandida
                var paymentsDto = new List<PaymentDto>();
                foreach (var payment in payments)
                {
                    var paymentDto = await MapToPaymentDtoAsync(payment);
                    paymentsDto.Add(paymentDto);
                }

                var response = PaginatedResponse<PaymentDto>.SuccessResponse(
                    paymentsDto, (int)totalCount, filters.Page, filters.PageSize, "Pagos obtenidos exitosamente");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pagos");
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener pago por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPayment(string id)
        {
            try
            {
                var payment = await _context.Payments.Find(p => p.Id == id).FirstOrDefaultAsync();
                
                if (payment == null)
                {
                    return NotFound(ApiResponse<PaymentDto>.ErrorResponse("Pago no encontrado"));
                }

                // Verificar permisos
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                if (!isAdmin && payment.ClienteId != userId)
                {
                    return Forbid("No tienes permisos para ver este pago");
                }

                var paymentDto = await MapToPaymentDtoAsync(payment);
                return Ok(ApiResponse<PaymentDto>.SuccessResponse(paymentDto, "Pago obtenido exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pago: {Id}", id);
                return StatusCode(500, ApiResponse<PaymentDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Crear nuevo pago para uno o múltiples turnos
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> CreatePayment([FromBody] CreatePaymentDto createPaymentDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Obtener la lista de turnos IDs (compatible con ambas estructuras)
                var turnosIds = createPaymentDto.GetTurnosIds();
                
                if (!turnosIds.Any())
                {
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse("Debe especificar al menos un turno"));
                }

                // Verificar que todos los turnos existan
                var turnos = await _context.Turnos
                    .Find(t => turnosIds.Contains(t.Id))
                    .ToListAsync();

                if (turnos.Count != turnosIds.Count)
                {
                    var turnosNoEncontrados = turnosIds.Except(turnos.Select(t => t.Id)).ToList();
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse(
                        $"Turnos no encontrados: {string.Join(", ", turnosNoEncontrados)}"));
                }

                // Verificar permisos: todos los turnos deben pertenecer al mismo cliente
                var clienteIds = turnos.Select(t => t.ClienteId).Distinct().ToList();
                if (clienteIds.Count > 1)
                {
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse(
                        "Todos los turnos deben pertenecer al mismo cliente"));
                }

                var clienteId = clienteIds.First();
                if (!isAdmin && clienteId != userId)
                {
                    return Forbid("No puedes crear pagos para turnos de otros usuarios");
                }

                // Verificar que ningún turno esté cancelado
                var turnosCancelados = turnos.Where(t => t.Estado == "cancelado").ToList();
                if (turnosCancelados.Any())
                {
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse(
                        $"No se pueden pagar turnos cancelados: {string.Join(", ", turnosCancelados.Select(t => t.Id))}"));
                }

                // Verificar que no existan pagos completados para estos turnos
                var existingPayments = await _context.Payments
                    .Find(p => p.TurnosIds.Any(id => turnosIds.Contains(id)) && p.Estado == "completado")
                    .ToListAsync();

                if (existingPayments.Any())
                {
                    var turnosPagados = existingPayments.SelectMany(p => p.TurnosIds).Distinct().ToList();
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse(
                        $"Algunos turnos ya tienen pagos completados: {string.Join(", ", turnosPagados)}"));
                }

                // Crear pago
                var payment = new Payment
                {
                    TurnosIds = turnosIds,
                    ClienteId = clienteId,
                    Monto = createPaymentDto.Monto,
                    MetodoPago = createPaymentDto.MetodoPago,
                    Estado = "completado", // ✅ Directamente completado para pagos con débito
                    PaymentDetails = createPaymentDto.PaymentDetails,
                    Notas = createPaymentDto.Notas,
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow // ✅ Procesado inmediatamente
                };

                await _context.Payments.InsertOneAsync(payment);

                // ✅ AUTO-PROCESAR: Marcar todos los turnos como confirmado inmediatamente
                // (Similar al comportamiento del backend Node.js)
                
                // Obtener los servicios para calcular precios individuales con descuento
                var serviciosIds = turnos.Select(t => t.ServicioId).ToList();
                var servicios = await _context.Services
                    .Find(s => serviciosIds.Contains(s.Id))
                    .ToListAsync();

                // Determinar si aplicar descuento (15% para débito)
                decimal descuentoPorcentaje = createPaymentDto.MetodoPago?.ToLower() == "débito" ? 0.15m : 0;

                // Actualizar cada turno individualmente con su precio correcto
                foreach (var turno in turnos)
                {
                    var servicio = servicios.FirstOrDefault(s => s.Id == turno.ServicioId);
                    if (servicio?.Precio.HasValue == true)
                    {
                        // Calcular precio con descuento individual
                        decimal precioConDescuento = servicio.Precio.Value * (1 - descuentoPorcentaje);
                        
                        var turnoUpdate = Builders<Turno>.Update
                            .Set(t => t.Estado, "confirmado")
                            .Set(t => t.PrecioPagado, precioConDescuento)
                            .Set(t => t.UpdatedAt, DateTime.UtcNow);

                        await _context.Turnos.UpdateOneAsync(t => t.Id == turno.Id, turnoUpdate);
                    }
                }

                _logger.LogInformation("✅ Pago auto-procesado exitosamente: {Id} para {TurnosCount} turnos. Turnos marcados como confirmado: {TurnosIds}", 
                    payment.Id, turnosIds.Count, string.Join(", ", turnosIds));

                var paymentDto = await MapToPaymentDtoAsync(payment);

                return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, 
                    ApiResponse<PaymentDto>.SuccessResponse(paymentDto, "Pago creado y procesado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear pago");
                return StatusCode(500, ApiResponse<PaymentDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Procesar pago (simular transacción)
        /// </summary>
        [HttpPost("process")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> ProcessPayment([FromBody] ProcessPaymentDto processPaymentDto)
        {
            try
            {
                var payment = await _context.Payments.Find(p => p.Id == processPaymentDto.PaymentId).FirstOrDefaultAsync();
                
                if (payment == null)
                {
                    return NotFound(ApiResponse<PaymentDto>.ErrorResponse("Pago no encontrado"));
                }

                // Verificar permisos
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                if (!isAdmin && payment.ClienteId != userId)
                {
                    return Forbid("No tienes permisos para procesar este pago");
                }

                // Verificar que el pago esté pendiente
                if (payment.Estado != "pendiente")
                {
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse($"No se puede procesar un pago en estado: {payment.Estado}"));
                }

                // Simular procesamiento de pago
                var isSuccessful = await SimulatePaymentProcessing(payment);

                // Actualizar estado del pago
                var updateBuilder = Builders<Payment>.Update
                    .Set(p => p.Estado, isSuccessful ? "completado" : "fallido")
                    .Set(p => p.ProcessedAt, DateTime.UtcNow);

                if (!string.IsNullOrEmpty(processPaymentDto.TransactionId))
                    updateBuilder = updateBuilder.Set(p => p.TransactionId, processPaymentDto.TransactionId);

                if (!string.IsNullOrEmpty(processPaymentDto.AuthorizationCode) && payment.PaymentDetails != null)
                {
                    updateBuilder = updateBuilder.Set("payment_details.authorization_code", processPaymentDto.AuthorizationCode);
                }

                if (!string.IsNullOrEmpty(processPaymentDto.Notas))
                    updateBuilder = updateBuilder.Set(p => p.Notas, processPaymentDto.Notas);

                await _context.Payments.UpdateOneAsync(p => p.Id == processPaymentDto.PaymentId, updateBuilder);

                // Si el pago fue exitoso, actualizar el turno
                if (isSuccessful)
                {
                    // Obtener turnos y servicios para calcular precios individuales
                    var turnosIds = payment.TurnosIds.Any() ? payment.TurnosIds : 
                                   (!string.IsNullOrEmpty(payment.TurnoId) ? new List<string> { payment.TurnoId } : new List<string>());
                    
                    var turnos = await _context.Turnos
                        .Find(t => turnosIds.Contains(t.Id))
                        .ToListAsync();

                    var serviciosIds = turnos.Select(t => t.ServicioId).ToList();
                    var servicios = await _context.Services
                        .Find(s => serviciosIds.Contains(s.Id))
                        .ToListAsync();

                    // Determinar si aplicar descuento (15% para débito)
                    decimal descuentoPorcentaje = payment.MetodoPago?.ToLower() == "débito" ? 0.15m : 0;

                    // Actualizar cada turno individualmente con su precio correcto
                    foreach (var turno in turnos)
                    {
                        var servicio = servicios.FirstOrDefault(s => s.Id == turno.ServicioId);
                        if (servicio?.Precio.HasValue == true)
                        {
                            // Calcular precio con descuento individual
                            decimal precioConDescuento = servicio.Precio.Value * (1 - descuentoPorcentaje);
                            
                            var turnoUpdate = Builders<Turno>.Update
                                .Set(t => t.Estado, "confirmado")
                                .Set(t => t.PrecioPagado, precioConDescuento)
                                .Set(t => t.UpdatedAt, DateTime.UtcNow);

                            await _context.Turnos.UpdateOneAsync(t => t.Id == turno.Id, turnoUpdate);
                        }
                    }

                    _logger.LogInformation("Turnos actualizados a confirmado: {TurnosIds}", 
                        string.Join(", ", turnosIds));
                }

                // Obtener pago actualizado
                var updatedPayment = await _context.Payments.Find(p => p.Id == processPaymentDto.PaymentId).FirstOrDefaultAsync();
                var paymentDto = await MapToPaymentDtoAsync(updatedPayment!);

                var message = isSuccessful ? "Pago procesado exitosamente" : "El pago falló";
                _logger.LogInformation("Pago procesado: {Id}, Resultado: {Resultado}", processPaymentDto.PaymentId, isSuccessful ? "Exitoso" : "Fallido");

                return Ok(ApiResponse<PaymentDto>.SuccessResponse(paymentDto, message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar pago: {Id}", processPaymentDto.PaymentId);
                return StatusCode(500, ApiResponse<PaymentDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Reembolsar pago
        /// </summary>
        [HttpPost("{id}/refund")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> RefundPayment(string id, [FromBody] RefundPaymentDto refundDto)
        {
            try
            {
                var payment = await _context.Payments.Find(p => p.Id == id).FirstOrDefaultAsync();
                
                if (payment == null)
                {
                    return NotFound(ApiResponse<PaymentDto>.ErrorResponse("Pago no encontrado"));
                }

                // Verificar permisos (solo admins pueden hacer reembolsos)
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";
                if (!isAdmin)
                {
                    return Forbid("Solo los administradores pueden procesar reembolsos");
                }

                // Verificar que el pago esté completado
                if (payment.Estado != "completado")
                {
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse("Solo se pueden reembolsar pagos completados"));
                }

                // Actualizar estado del pago
                var update = Builders<Payment>.Update
                    .Set(p => p.Estado, "reembolsado")
                    .Set(p => p.Notas, refundDto.Motivo);

                await _context.Payments.UpdateOneAsync(p => p.Id == id, update);

                // Actualizar estado del turno
                var turnoUpdate = Builders<Turno>.Update
                    .Set(t => t.Estado, "cancelado")
                    .Set(t => t.UpdatedAt, DateTime.UtcNow);

                // Actualizar todos los turnos del pago (compatible con estructura antigua y nueva)
                var turnosToUpdate = payment.TurnosIds.Any() 
                    ? Builders<Turno>.Filter.In(t => t.Id, payment.TurnosIds)
                    : Builders<Turno>.Filter.Eq(t => t.Id, payment.TurnoId);

                await _context.Turnos.UpdateManyAsync(turnosToUpdate, turnoUpdate);

                var updatedPayment = await _context.Payments.Find(p => p.Id == id).FirstOrDefaultAsync();
                var paymentDto = await MapToPaymentDtoAsync(updatedPayment!);

                _logger.LogInformation("Pago reembolsado: {Id}", id);
                return Ok(ApiResponse<PaymentDto>.SuccessResponse(paymentDto, "Pago reembolsado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reembolsar pago: {Id}", id);
                return StatusCode(500, ApiResponse<PaymentDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener estadísticas de pagos (solo admins)
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<ApiResponse<PaymentStatsDto>>> GetPaymentStats([FromQuery] DateTime? fechaDesde, [FromQuery] DateTime? fechaHasta)
        {
            try
            {
                var filterBuilder = Builders<Payment>.Filter;
                var filter = filterBuilder.Empty;

                if (fechaDesde.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Gte(p => p.CreatedAt, fechaDesde.Value));

                if (fechaHasta.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Lte(p => p.CreatedAt, fechaHasta.Value));

                var payments = await _context.Payments.Find(filter).ToListAsync();

                var stats = new PaymentStatsDto
                {
                    TotalPagos = payments.Count,
                    PagosCompletados = payments.Count(p => p.Estado == "completado"),
                    PagosPendientes = payments.Count(p => p.Estado == "pendiente"),
                    PagosFallidos = payments.Count(p => p.Estado == "fallido"),
                    MontoTotal = payments.Where(p => p.Estado == "completado").Sum(p => p.Monto),
                    MontoPromedio = payments.Where(p => p.Estado == "completado").Any() 
                        ? payments.Where(p => p.Estado == "completado").Average(p => p.Monto) 
                        : 0,
                    FechaDesde = fechaDesde,
                    FechaHasta = fechaHasta
                };

                return Ok(ApiResponse<PaymentStatsDto>.SuccessResponse(stats, "Estadísticas obtenidas exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de pagos");
                return StatusCode(500, ApiResponse<PaymentStatsDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Simular procesamiento de pago
        /// </summary>
        private async Task<bool> SimulatePaymentProcessing(Payment payment)
        {
            // Simular delay de procesamiento
            await Task.Delay(1000);

            // Simular una tasa de éxito del 95%
            var random = new Random();
            return random.NextDouble() > 0.05;
        }

        /// <summary>
        /// Mapear Payment a PaymentDto con información expandida
        /// </summary>
        private async Task<PaymentDto> MapToPaymentDtoAsync(Payment payment)
        {
            var paymentDto = new PaymentDto
            {
                Id = payment.Id,
                TurnosIds = payment.TurnosIds,
                ClienteId = payment.ClienteId,
                Monto = payment.Monto,
                MetodoPago = payment.MetodoPago,
                Estado = payment.Estado,
                TransactionId = payment.TransactionId,
                CreatedAt = payment.CreatedAt,
                ProcessedAt = payment.ProcessedAt,
                Notas = payment.Notas
            };

            // Obtener información de los turnos (compatible con estructura antigua y nueva)
            var turnosIds = payment.TurnosIds.Any() ? payment.TurnosIds : 
                           (!string.IsNullOrEmpty(payment.TurnoId) ? new List<string> { payment.TurnoId } : new List<string>());

            if (turnosIds.Any())
            {
                var turnos = await _context.Turnos
                    .Find(t => turnosIds.Contains(t.Id))
                    .ToListAsync();

                paymentDto.Turnos = turnos.Select(turno => new TurnoDto
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
                }).ToList();
            }

            // Obtener información del cliente
            var cliente = await _context.Users.Find(u => u.Id == payment.ClienteId).FirstOrDefaultAsync();
            if (cliente != null)
            {
                paymentDto.Cliente = new UserDto
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

            return paymentDto;
        }
    }

    // DTO adicional para reembolsos
    public class RefundPaymentDto
    {
        [Required(ErrorMessage = "El motivo del reembolso es requerido")]
        public string Motivo { get; set; } = string.Empty;
    }

    // DTO para estadísticas
    public class PaymentStatsDto
    {
        public int TotalPagos { get; set; }
        public int PagosCompletados { get; set; }
        public int PagosPendientes { get; set; }
        public int PagosFallidos { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal MontoPromedio { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }
} 