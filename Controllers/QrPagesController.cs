using Microsoft.AspNetCore.Mvc;

namespace SentirseWellApi.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)] // Estas rutas solo devuelven HTML, no forman parte del swagger
    public class QrPagesController : Controller
    {
        [HttpGet("/qr-success")]
        public IActionResult Success([FromQuery] string? action, [FromQuery] string? message)
        {
            var html = $@"<!DOCTYPE html>
<html lang=""es"">
<head>
<meta charset=""utf-8"" />
<title>QR Success</title>
<style>
body {{ font-family: Arial, sans-serif; padding:40px; text-align:center; }}
h1   {{ color:#28a745; }}
</style>
</head>
<body>
<h1>✅ ¡Acción completada!</h1>
<p><strong>Acción:</strong> {action}</p>
<p>{message}</p>
<a href=""/"">Volver al sitio</a>
</body>
</html>";
            return Content(html, "text/html");
        }

        [HttpGet("/qr-error")]
        public IActionResult Error([FromQuery] string? message)
        {
            var html = $@"<!DOCTYPE html>
<html lang=""es"">
<head>
<meta charset=""utf-8"" />
<title>QR Error</title>
<style>
body {{ font-family: Arial, sans-serif; padding:40px; text-align:center; }}
h1   {{ color:#dc3545; }}
</style>
</head>
<body>
<h1>❌ Error</h1>
<p>{message}</p>
<a href=""/"">Volver al sitio</a>
</body>
</html>";
            return Content(html, "text/html");
        }
    }
} 