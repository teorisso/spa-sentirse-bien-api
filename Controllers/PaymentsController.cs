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
        /// Crear nuevo pago para un turno
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> CreatePayment([FromBody] CreatePaymentDto createPaymentDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Verificar que el turno existe
                var turno = await _context.Turnos.Find(t => t.Id == createPaymentDto.TurnoId).FirstOrDefaultAsync();
                if (turno == null)
                {
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse("Turno no encontrado"));
                }

                // Verificar permisos: solo el cliente del turno o admin puede crear pagos para ese turno
                if (!isAdmin && turno.ClienteId != userId)
                {
                    return Forbid("No puedes crear pagos para turnos de otros usuarios");
                }

                // Verificar que el turno no esté cancelado
                if (turno.Estado == "cancelado")
                {
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse("No se puede pagar un turno cancelado"));
                }

                // Verificar que no exista ya un pago completado para este turno
                var existingPayment = await _context.Payments
                    .Find(p => p.TurnoId == createPaymentDto.TurnoId && p.Estado == "completado")
                    .FirstOrDefaultAsync();

                if (existingPayment != null)
                {
                    return BadRequest(ApiResponse<PaymentDto>.ErrorResponse("Este turno ya tiene un pago completado"));
                }

                // Crear pago
                var payment = new Payment
                {
                    TurnoId = createPaymentDto.TurnoId,
                    ClienteId = turno.ClienteId,
                    Monto = createPaymentDto.Monto,
                    MetodoPago = createPaymentDto.MetodoPago,
                    Estado = "pendiente",
                    PaymentDetails = createPaymentDto.PaymentDetails,
                    Notas = createPaymentDto.Notas,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Payments.InsertOneAsync(payment);

                var paymentDto = await MapToPaymentDtoAsync(payment);
                _logger.LogInformation("Pago creado exitosamente: {Id}", payment.Id);

                return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, 
                    ApiResponse<PaymentDto>.SuccessResponse(paymentDto, "Pago creado exitosamente"));
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
                    var turnoUpdate = Builders<Turno>.Update
                        .Set(t => t.Estado, "confirmado")
                        .Set(t => t.PrecioPagado, payment.Monto)
                        .Set(t => t.UpdatedAt, DateTime.UtcNow);

                    await _context.Turnos.UpdateOneAsync(t => t.Id == payment.TurnoId, turnoUpdate);
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

                await _context.Turnos.UpdateOneAsync(t => t.Id == payment.TurnoId, turnoUpdate);

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
                TurnoId = payment.TurnoId,
                ClienteId = payment.ClienteId,
                Monto = payment.Monto,
                MetodoPago = payment.MetodoPago,
                Estado = payment.Estado,
                TransactionId = payment.TransactionId,
                CreatedAt = payment.CreatedAt,
                ProcessedAt = payment.ProcessedAt,
                Notas = payment.Notas
            };

            // Obtener información del turno
            var turno = await _context.Turnos.Find(t => t.Id == payment.TurnoId).FirstOrDefaultAsync();
            if (turno != null)
            {
                // Para el DTO del turno, obtener información básica sin crear dependencia circular
                paymentDto.Turno = new TurnoDto
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