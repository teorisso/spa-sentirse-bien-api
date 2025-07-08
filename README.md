# SPA Sentirse Bien – API ASP.NET Core

API RESTful para el sistema **Spa Sentirse Bien** desarrollada con **ASP.NET Core 9.0** y **MongoDB Atlas**. Este backend unificado sirve simultáneamente al panel administrativo MVC y al frontend SPA (Next.js 15).

## ✨ Características clave

- Autenticación **JWT** con roles (`cliente`, `profesional`, `admin`).
- CRUD completo de **Servicios**, **Turnos**, **Pagos** y **Usuarios**.
- Listados paginados y filtros avanzados en todos los endpoints.
- **Códigos QR dinámicos** con tokens seguros que expiran.
- Emails transaccionales (Gmail OAuth / Resend).
- Seguridad reforzada: BCrypt, CORS, HSTS, rate-limiting.
- Arquitectura limpia con **DI**, capa de acceso a datos y DTOs.

## 📁 Estructura del proyecto

```text
spa-sentirse-bien-api/
├── Controllers/
│   ├── AuthController.cs
│   ├── ServicesController.cs
│   ├── TurnosController.cs
│   ├── PaymentsController.cs
│   └── QRController.cs
├── Models/
│   ├── User.cs   │  Service.cs   │  Turno.cs
│   ├── Payment.cs│  QRCode.cs    │  ApiResponse.cs
├── Data/
│   └── MongoDbContext.cs
├── Services/
│   ├── IEmailService.cs
│   └── EmailService.cs
└── Program.cs
```

## 🚀 Puesta en marcha rápida

### 1. Requisitos previos

- .NET SDK 9.0
- Cuenta de MongoDB Atlas (o instancia local)

### 2. Clonar y restaurar dependencias

```bash
git clone <url-del-repo>
cd spa-sentirse-bien-api
dotnet restore
```

### 3. Configurar secretos de usuario (desarrollo)

```bash
dotnet user-secrets init

# MongoDB
 dotnet user-secrets set "ConnectionStrings:MongoDB" "<cadena_conexion>"

# JWT
 dotnet user-secrets set "JWT:Key" "<clave_256_bits>"

# Email (Gmail OAuth) – opcional
 dotnet user-secrets set "Email:SenderEmail" "tu-email@gmail.com"
 dotnet user-secrets set "Email:GoogleClientId" "<client_id>"
 dotnet user-secrets set "Email:GoogleClientSecret" "<client_secret>"
 dotnet user-secrets set "Email:GoogleRefreshToken" "<refresh_token>"

# Resend (opcional)
 dotnet user-secrets set "ResendEmail:ApiKey" "<api_key>"
```

> **Ventajas de User Secrets**: nunca se suben credenciales al repositorio y la configuración es individual por desarrollador.

### 4. Ejecutar la API

```bash
dotnet run
```

La API quedará disponible en:

- **Swagger UI**: <http://localhost:5018/swagger>
- **Base URL**: `http://localhost:5018/api`

---

## 📚 Resumen de endpoints

| Módulo    | Verbo | Ruta                                  | Descripción                                 |
|-----------|-------|---------------------------------------|---------------------------------------------|
| Auth      | POST  | /api/auth/login                       | Iniciar sesión                              |
| Auth      | POST  | /api/auth/register                    | Registrar nuevo usuario                     |
| Auth      | POST  | /api/auth/forgot-password             | Solicitar recuperación de contraseña        |
| Auth      | POST  | /api/auth/reset-password              | Restablecer contraseña                      |
| Services  | GET   | /api/services                         | Listar servicios (paginado + filtros)       |
| Services  | POST  | /api/services                         | Crear servicio                              |
| Services  | PUT   | /api/services/{id}                    | Actualizar servicio                         |
| Services  | DELETE| /api/services/{id}                    | Eliminar servicio                           |
| Turnos    | GET   | /api/turnos                           | Listar turnos (paginado + filtros)          |
| Turnos    | POST  | /api/turnos                           | Reservar turno                              |
| Turnos    | PUT   | /api/turnos/{id}                      | Actualizar turno                            |
| Turnos    | DELETE| /api/turnos/{id}                      | Cancelar turno                              |
| Turnos    | GET   | /api/turnos/disponibilidad            | Chequear disponibilidad del profesional     |
| Payments  | GET   | /api/payments                         | Listar pagos (paginado + filtros)           |
| Payments  | POST  | /api/payments                         | Crear registro de pago                      |
| Payments  | POST  | /api/payments/process                 | Procesar pago                               |
| Payments  | POST  | /api/payments/{id}/refund             | Reembolsar pago (solo admin)                |
| Payments  | GET   | /api/payments/stats                   | Estadísticas de pagos (solo admin)          |
| QR        | POST  | /api/qr/generate                      | Generar código QR                           |
| QR        | GET   | /api/qr/validate/{token}              | Validar código QR                           |
| QR        | GET   | /api/qr/info/{token}                  | Obtener info de QR sin procesar             |
| QR        | GET   | /api/qr/history                       | Historial de QRs (solo admin)               |

---

## 🗄️ Colecciones MongoDB

- `users`
- `services`
- `turnos`
- `payments`
- `qrcodes`
- `passwordtokens`

## 🔐 Seguridad

- Tokens **JWT** con expiración de 2 h y roles embebidos.
- Contraseñas protegidas con **BCrypt.Net** (work factor 12).
- Política **CORS** restringida según entorno.
- Middleware de excepciones global y logging estructurado.
- Rate-limiting básico con `AspNetCoreRateLimit`.

## 🚀 Estado del proyecto

| Funcionalidad                        | Estado |
|--------------------------------------|:------:|
| Autenticación JWT + Recuperación pwd | ✅ |
| CRUD Servicios                       | ✅ |
| Gestión de Turnos                    | ✅ |
| Procesamiento de Pagos               | ✅ |
| Códigos QR dinámicos                 | ✅ |
| Emails transaccionales               | ✅ |
| Listados paginados                   | ✅ |
| Migración Node ➜ ASP.NET             | ✅ |

> **¡Listo para producción y presentación del TP-2025!**

## 🤝 Contribución

Las *pull requests* son bienvenidas. Abre antes un *issue* para discutir cambios sustanciales.

## 📝 Licencia

Este proyecto se distribuye bajo licencia **MIT**. 