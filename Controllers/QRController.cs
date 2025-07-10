using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using QRCoder;
using SentirseWellApi.Data;
using SentirseWellApi.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SentirseWellApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QRController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<QRController> _logger;
        private readonly IConfiguration _configuration;

        public QRController(MongoDbContext context, ILogger<QRController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Generar código QR para un turno (funcionalidad exclusiva)
        /// </summary>
        [HttpPost("generate")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<QRCodeResponse>>> GenerateQRCode([FromBody] CreateQRCodeDto createQRDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Validar permisos según el tipo de acción
                if (!await ValidateQRPermissions(createQRDto, userId, isAdmin))
                {
                    return Forbid("No tienes permisos para generar este tipo de QR");
                }

                // Generar token único y seguro
                var token = GenerateSecureToken();
                var expiresAt = DateTime.UtcNow.AddMinutes(createQRDto.ExpirationMinutes);

                // Crear registro de QR en la base de datos
                var qrCode = new QRCode
                {
                    Token = token,
                    UserId = createQRDto.UserId ?? userId,
                    TurnoId = createQRDto.TurnoId,
                    Action = createQRDto.Action,
                    Data = createQRDto.Data,
                    ExpiresAt = expiresAt,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsUsed = false
                };

                await _context.QRCodes.InsertOneAsync(qrCode);

                // Generar URL del QR
                var baseUrl = _configuration["QRCode:BaseUrl"] ?? "https://localhost:7000/api/qr";
                var qrUrl = $"{baseUrl}/validate/{token}";

                // Generar imagen del código QR
                var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
                var qrCodeBmp = new BitmapByteQRCode(qrCodeData);
                var qrCodeImageBytes = qrCodeBmp.GetGraphic(20);
                var qrCodeBase64 = Convert.ToBase64String(qrCodeImageBytes);

                var response = new QRCodeResponse
                {
                    QRCodeId = qrCode.Id!,
                    Token = token,
                    QRCodeImageBase64 = qrCodeBase64,
                    QRCodeUrl = qrUrl,
                    ExpiresAt = expiresAt
                };

                _logger.LogInformation("QR Code generado: {Id} para acción: {Action}", qrCode.Id, qrCode.Action);
                return Ok(ApiResponse<QRCodeResponse>.SuccessResponse(response, "Código QR generado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar código QR");
                return StatusCode(500, ApiResponse<QRCodeResponse>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Validar y procesar código QR escaneado (funcionalidad exclusiva)
        /// </summary>
        [HttpGet("validate/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult> ValidateQRCode(string token)
        {
            try
            {
                // Buscar el código QR por token
                var qrCode = await _context.QRCodes
                    .Find(qr => qr.Token == token)
                    .FirstOrDefaultAsync();

                if (qrCode == null)
                {
                    return NotFound("Código QR no válido");
                }

                // Verificar si el QR está expirado
                if (DateTime.UtcNow > qrCode.ExpiresAt)
                {
                    return BadRequest("Código QR expirado");
                }

                // Verificar si ya fue usado
                if (qrCode.IsUsed)
                {
                    return BadRequest("Código QR ya utilizado");
                }

                // Procesar según el tipo de acción
                var result = await ProcessQRAction(qrCode);

                if (result.Success)
                {
                    // Marcar como usado
                    var update = Builders<QRCode>.Update
                        .Set(qr => qr.IsUsed, true)
                        .Set(qr => qr.UsedAt, DateTime.UtcNow);

                    await _context.QRCodes.UpdateOneAsync(qr => qr.Id == qrCode.Id, update);

                    _logger.LogInformation("QR Code validado y procesado: {Id} - Acción: {Action}", qrCode.Id, qrCode.Action);
                    
                    // Redirigir a página de éxito con información
                    return Redirect($"/qr-success?action={qrCode.Action}&message={Uri.EscapeDataString(result.Message)}");
                }
                else
                {
                    _logger.LogWarning("Error al procesar QR Code: {Id} - Error: {Error}", qrCode.Id, result.Message);
                    return BadRequest(result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar código QR: {Token}", token);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtener información de un QR sin marcarlo como usado
        /// </summary>
        [HttpGet("info/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<QRCodeDto>>> GetQRInfo(string token)
        {
            try
            {
                var qrCode = await _context.QRCodes
                    .Find(qr => qr.Token == token)
                    .FirstOrDefaultAsync();

                if (qrCode == null)
                {
                    return NotFound(ApiResponse<QRCodeDto>.ErrorResponse("Código QR no válido"));
                }

                var qrDto = new QRCodeDto
                {
                    Id = qrCode.Id,
                    Token = qrCode.Token,
                    UserId = qrCode.UserId,
                    TurnoId = qrCode.TurnoId,
                    Action = qrCode.Action,
                    Data = qrCode.Data,
                    ExpiresAt = qrCode.ExpiresAt,
                    IsUsed = qrCode.IsUsed,
                    CreatedAt = qrCode.CreatedAt,
                    IsValid = qrCode.IsValid
                };

                return Ok(ApiResponse<QRCodeDto>.SuccessResponse(qrDto, "Información del QR obtenida"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información del QR: {Token}", token);
                return StatusCode(500, ApiResponse<QRCodeDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener QR de check-in para turno (automático o existente)
        /// </summary>
        [HttpPost("turno/{turnoId}/checkin")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<QRCodeResponse>>> GenerateCheckinQR(string turnoId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.FindFirst("isAdmin")?.Value == "true";

                // Verificar que el turno existe
                var turno = await _context.Turnos.Find(t => t.Id == turnoId).FirstOrDefaultAsync();
                if (turno == null)
                {
                    return NotFound(ApiResponse<QRCodeResponse>.ErrorResponse("Turno no encontrado"));
                }

                // Verificar permisos
                if (!isAdmin && turno.ClienteId != userId && turno.ProfesionalId != userId)
                {
                    return Forbid("No tienes permisos para ver el QR de este turno");
                }

                // Verificar que el turno esté confirmado
                if (turno.Estado != "confirmado")
                {
                    return BadRequest(ApiResponse<QRCodeResponse>.ErrorResponse("El turno debe estar confirmado para generar QR de check-in"));
                }

                // 🚀 NUEVA LÓGICA: Verificar si ya existe un QR válido para este turno
                var existingQR = await _context.QRCodes
                    .Find(qr => qr.TurnoId == turnoId && 
                               qr.Action == "check_in" && 
                               qr.IsUsed == false && 
                               qr.ExpiresAt > DateTime.UtcNow)
                    .FirstOrDefaultAsync();

                if (existingQR != null)
                {
                    // Retornar el QR existente válido
                    var existingResponse = await GenerateQRResponse(existingQR);
                    _logger.LogInformation("QR existente reutilizado para turno: {TurnoId}", turnoId);
                    return Ok(ApiResponse<QRCodeResponse>.SuccessResponse(existingResponse, "QR de check-in obtenido"));
                }

                // 🚀 VERIFICAR VENTANA DE TIEMPO PARA GENERACIÓN AUTOMÁTICA
                var turnoDateTime = DateTime.Parse($"{turno.Fecha:yyyy-MM-dd} {turno.Hora}");
                var now = DateTime.Now;
                var ventanaInicio = turnoDateTime.AddHours(-1); // 1 hora antes
                var ventanaFin = turnoDateTime.AddHours(1);     // 1 hora después

                if (now < ventanaInicio)
                {
                    return BadRequest(ApiResponse<QRCodeResponse>.ErrorResponse($"El QR estará disponible 1 hora antes del turno ({ventanaInicio:dd/MM/yyyy HH:mm})"));
                }

                if (now > ventanaFin)
                {
                    return BadRequest(ApiResponse<QRCodeResponse>.ErrorResponse("El turno ha finalizado, QR ya no disponible"));
                }

                // Generar nuevo QR automáticamente
                var createQRDto = new CreateQRCodeDto
                {
                    Action = "check_in",
                    TurnoId = turnoId,
                    UserId = turno.ClienteId,
                    ExpirationMinutes = 60, // QR válido por 60 minutos
                    Data = new Dictionary<string, object>
                    {
                        ["turno_id"] = turnoId,
                        ["cliente_id"] = turno.ClienteId,
                        ["fecha"] = turno.Fecha.ToString("yyyy-MM-dd"),
                        ["hora"] = turno.Hora,
                        ["generado_automaticamente"] = true,
                        ["ventana_checkin"] = $"{ventanaInicio:HH:mm} - {ventanaFin:HH:mm}"
                    }
                };

                var result = await GenerateQRCode(createQRDto);
                _logger.LogInformation("QR generado automáticamente para turno: {TurnoId}", turnoId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener/generar QR de check-in para turno: {TurnoId}", turnoId);
                return StatusCode(500, ApiResponse<QRCodeResponse>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener historial de QRs generados (solo admins)
        /// </summary>
        [HttpGet("history")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<PaginatedResponse<QRCodeDto>>> GetQRHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? action = null,
            [FromQuery] bool? isUsed = null)
        {
            try
            {
                var filterBuilder = Builders<QRCode>.Filter;
                var filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(action))
                    filter = filterBuilder.And(filter, filterBuilder.Eq(qr => qr.Action, action));

                if (isUsed.HasValue)
                    filter = filterBuilder.And(filter, filterBuilder.Eq(qr => qr.IsUsed, isUsed.Value));

                var totalCount = await _context.QRCodes.CountDocumentsAsync(filter);

                var qrCodes = await _context.QRCodes
                    .Find(filter)
                    .Sort(Builders<QRCode>.Sort.Descending(qr => qr.CreatedAt))
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();

                var qrDtos = qrCodes.Select(qr => new QRCodeDto
                {
                    Id = qr.Id,
                    Token = qr.Token,
                    UserId = qr.UserId,
                    TurnoId = qr.TurnoId,
                    Action = qr.Action,
                    Data = qr.Data,
                    ExpiresAt = qr.ExpiresAt,
                    IsUsed = qr.IsUsed,
                    CreatedAt = qr.CreatedAt,
                    IsValid = qr.IsValid
                }).ToList();

                var response = PaginatedResponse<QRCodeDto>.SuccessResponse(
                    qrDtos, (int)totalCount, page, pageSize, "Historial de QR obtenido");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de QR");
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Generar respuesta QR a partir de un QRCode existente
        /// </summary>
        private async Task<QRCodeResponse> GenerateQRResponse(QRCode existingQR)
        {
            // Regenerar la imagen QR para el token existente
            var baseUrl = _configuration["QRCode:BaseUrl"] ?? "https://localhost:7000/api/qr";
            var qrUrl = $"{baseUrl}/validate/{existingQR.Token}";

            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
            var qrCodeBmp = new BitmapByteQRCode(qrCodeData);
            var qrCodeImageBytes = qrCodeBmp.GetGraphic(20);
            var qrCodeBase64 = Convert.ToBase64String(qrCodeImageBytes);

            return new QRCodeResponse
            {
                QRCodeId = existingQR.Id!,
                Token = existingQR.Token,
                QRCodeImageBase64 = qrCodeBase64,
                QRCodeUrl = qrUrl,
                ExpiresAt = existingQR.ExpiresAt
            };
        }

        /// <summary>
        /// Generar token seguro para QR
        /// </summary>
        private string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var tokenBytes = new byte[32];
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        /// <summary>
        /// Validar permisos para generar QR
        /// </summary>
        private async Task<bool> ValidateQRPermissions(CreateQRCodeDto createQRDto, string? userId, bool isAdmin)
        {
            // Admins pueden generar cualquier QR
            if (isAdmin) return true;

            // Para turnos, verificar que el usuario sea el cliente o profesional
            if (!string.IsNullOrEmpty(createQRDto.TurnoId))
            {
                var turno = await _context.Turnos.Find(t => t.Id == createQRDto.TurnoId).FirstOrDefaultAsync();
                if (turno == null) return false;
                
                return turno.ClienteId == userId || turno.ProfesionalId == userId;
            }

            // Para otros tipos, el usuario solo puede generar QRs para sí mismo
            return createQRDto.UserId == null || createQRDto.UserId == userId;
        }

        /// <summary>
        /// Procesar acción del QR escaneado
        /// </summary>
        private async Task<(bool Success, string Message)> ProcessQRAction(QRCode qrCode)
        {
            switch (qrCode.Action.ToLower())
            {
                case "check_in":
                    return await ProcessCheckIn(qrCode);

                case "payment_confirmation":
                    return await ProcessPaymentConfirmation(qrCode);

                case "service_access":
                    return await ProcessServiceAccess(qrCode);

                case "special_offer":
                    return await ProcessSpecialOffer(qrCode);

                default:
                    return (false, $"Acción no reconocida: {qrCode.Action}");
            }
        }

        /// <summary>
        /// Procesar check-in de turno
        /// </summary>
        private async Task<(bool Success, string Message)> ProcessCheckIn(QRCode qrCode)
        {
            try
            {
                if (string.IsNullOrEmpty(qrCode.TurnoId))
                {
                    return (false, "QR de check-in inválido: no tiene turno asociado");
                }

                var turno = await _context.Turnos.Find(t => t.Id == qrCode.TurnoId).FirstOrDefaultAsync();
                if (turno == null)
                {
                    return (false, "Turno no encontrado");
                }

                // Actualizar estado del turno a "en proceso" o "realizado"
                var update = Builders<Turno>.Update
                    .Set(t => t.Estado, "realizado")
                    .Set(t => t.UpdatedAt, DateTime.UtcNow);

                await _context.Turnos.UpdateOneAsync(t => t.Id == qrCode.TurnoId, update);

                return (true, $"Check-in exitoso para turno del {turno.Fecha:dd/MM/yyyy} a las {turno.Hora}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en check-in: {QRId}", qrCode.Id);
                return (false, "Error al procesar check-in");
            }
        }

        /// <summary>
        /// Procesar confirmación de pago
        /// </summary>
        private async Task<(bool Success, string Message)> ProcessPaymentConfirmation(QRCode qrCode)
        {
            try
            {
                // Lógica específica para confirmación de pagos
                // Por ejemplo, confirmar un pago pendiente
                return (true, "Pago confirmado exitosamente mediante QR");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en confirmación de pago: {QRId}", qrCode.Id);
                return (false, "Error al confirmar pago");
            }
        }

        /// <summary>
        /// Procesar acceso a servicio especial
        /// </summary>
        private async Task<(bool Success, string Message)> ProcessServiceAccess(QRCode qrCode)
        {
            try
            {
                // Funcionalidad exclusiva: acceso a contenido premium, descuentos especiales, etc.
                return (true, "Acceso concedido a funcionalidad exclusiva");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en acceso a servicio: {QRId}", qrCode.Id);
                return (false, "Error al acceder al servicio");
            }
        }

        /// <summary>
        /// Procesar oferta especial
        /// </summary>
        private async Task<(bool Success, string Message)> ProcessSpecialOffer(QRCode qrCode)
        {
            try
            {
                // Funcionalidad exclusiva: aplicar descuentos, promociones, etc.
                return (true, "¡Oferta especial aplicada! Disfruta tu descuento exclusivo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en oferta especial: {QRId}", qrCode.Id);
                return (false, "Error al aplicar oferta especial");
            }
        }
    }
} 