# SPA Sentirse Bien – API ASP.NET Core

**Trabajo Práctico Integrador 2025 - Componente API RESTful**

## 🎯 Descripción del Escenario

**Spa Sentirse Bien** es un sistema completo de gestión para un spa de relajación y bienestar que permite:

- **Clientes**: Reservar turnos online, procesar pagos seguros, acceder a servicios exclusivos vía QR
- **Profesionales**: Gestionar agenda diaria, consultar historial de clientes, registrar tratamientos
- **Administradores**: Control completo de usuarios, servicios, turnos, pagos y estadísticas

Esta API RESTful sirve como backend unificado para el panel administrativo MVC y el frontend SPA (Next.js).

## 🛠️ Herramientas Utilizadas

### Framework y Lenguaje
- **ASP.NET Core 9.0** - Framework principal
- **C#** - Lenguaje de programación
- **MongoDB Atlas** - Base de datos NoSQL en la nube

### Librerías y Paquetes
- **Microsoft.AspNetCore.Authentication.JwtBearer 9.0.6** - Autenticación JWT
- **BCrypt.Net-Next 4.0.3** - Hash seguro de contraseñas
- **QRCoder 1.6.0** - Generación de códigos QR
- **DotNetEnv 3.1.1** - Gestión de variables de entorno
- **MongoDB.Driver 3.4.0** - Driver oficial de MongoDB
- **MailKit 4.13.0** - Envío de emails
- **System.IdentityModel.Tokens.Jwt 8.12.1** - Manejo de tokens JWT
- **Swashbuckle.AspNetCore 9.0.1** - Documentación OpenAPI/Swagger

### Seguridad
- **CORS** configurado para múltiples orígenes
- **JWT Bearer Authentication** con validación completa
- **BCrypt password hashing** con salt automático

## 📋 Cumplimiento de Requisitos TP-2025

### ✅ Requisitos Técnicos Implementados

1. **✅ API RESTful en ASP.NET Web API**
   - 7 controladores principales con endpoints REST completos
   - Documentación automática con Swagger
   - Arquitectura limpia con separación de responsabilidades

2. **✅ Autenticación JWT**
   - Tokens seguros con expiración configurable
   - Claims personalizados con roles (cliente, profesional, admin)
   - Middleware de autenticación en endpoints protegidos

3. **✅ Registro de usuarios**
   - Endpoint `POST /api/auth/register` con validaciones DataAnnotations
   - Campos requeridos: email, contraseña, nombre, apellido, rol
   - Validación de email único en base de datos

4. **✅ Contraseñas hasheadas con salt**
   - BCrypt.Net-Next con work factor automático
   - Salt automático generado por la librería
   - Compatibilidad con hashes de sistemas anteriores

5. **✅ Recuperación de contraseña**
   - Endpoint `POST /api/auth/forgot-password`
   - Tokens únicos y seguros con expiración de 1 hora
   - Endpoint `POST /api/auth/reset-password` para restablecer

6. **✅ Listados paginados**
   - `PaginatedResponse<T>` estándar en todos los endpoints de listado
   - Parámetros: page, pageSize, filtros específicos
   - Metadatos: totalCount, totalPages, hasNext, hasPrevious

7. **✅ Vista de detalle**
   - Endpoints `GET /api/{resource}/{id}` para cada entidad
   - Información expandida con relaciones
   - Autorización por roles para acceso a detalles

8. **✅ Crear elementos con dropdown/selector**
   - Endpoint `POST /api/services` con selector de tipos/categorías
   - Validación de roles en creación de usuarios
   - Métodos de pago predefinidos en sistema de pagos

9. **✅ Editar elementos existentes**
   - Endpoints `PUT /api/{resource}/{id}` para todas las entidades
   - Validaciones de negocio implementadas
   - Autorización por roles y propietario

10. **✅ Código QR generado desde backend**
    - `QRController` con generación usando QRCoder library
    - Tokens criptográficamente seguros de 32 bytes
    - Almacenamiento en MongoDB con metadatos completos

11. **✅ Funcionalidad exclusiva QR con enlaces temporales**
    - URLs con tokens que expiran (configurable 1-1440 minutos)
    - Validación automática y marcado como "usado"
    - Funcionalidades exclusivas: check-in, ofertas especiales, acceso premium

## 🚀 Instalación y Ejecución

### Requisitos Previos
```bash
# Verificar versión de .NET
dotnet --version  # Debe ser 9.0 o superior
```

### 1. Clonar el repositorio
```bash
git clone https://github.com/tu-usuario/spa-sentirse-bien.git
cd spa-sentirse-bien/spa-sentirse-bien-api
```

### 2. Restaurar dependencias
```bash
dotnet restore
```

### 3. Configurar variables de entorno
Crear archivo `.env` en la raíz del proyecto:

```env
# MongoDB Atlas
ConnectionStrings__MongoDB=mongodb+srv://usuario:password@cluster.mongodb.net/
MongoDatabase=sentirseBien

# JWT Configuration
JWT__Key=tu-clave-secreta-de-256-bits-minimo-para-jwt-seguro
JWT__Issuer=SentirseWellApi
JWT__Audience=SentirseWellClients
JWT__DurationInMinutes=120

# Email Configuration (Gmail)
Email__SenderEmail=tu-email@gmail.com
Email__GoogleClientId=tu-google-client-id
Email__GoogleClientSecret=tu-google-client-secret
Email__GoogleRefreshToken=tu-google-refresh-token

# QR Code Configuration
QRCode__BaseUrl=https://localhost:7000/api/qr

# CORS Configuration
CORS__AllowedOrigins=http://localhost:9002,https://localhost:9002,http://localhost:7001,https://localhost:7001
```

### 4. Ejecutar la aplicación
```bash
dotnet run
```

### 5. Verificar funcionamiento
- **Swagger UI**: http://localhost:5018/swagger
- **Health Check**: http://localhost:5018/health
- **Base URL API**: http://localhost:5018/api

## 📚 Controladores y Endpoints Implementados

### 🔐 AuthController (`/api/auth`)
- `POST /api/auth/register` - Registrar nuevo usuario
- `POST /api/auth/login` - Iniciar sesión
- `POST /api/auth/forgot-password` - Solicitar recuperación de contraseña
- `POST /api/auth/reset-password` - Restablecer contraseña

### 👥 UsersController (`/api/users`)
- `GET /api/users` - Listar usuarios (paginado)
- `GET /api/users/profesionales` - Listar profesionales
- `GET /api/users/clientes` - Listar clientes
- `GET /api/users/{id}` - Obtener usuario por ID
- `GET /api/users/profile` - Obtener perfil del usuario logueado
- `PUT /api/users/{id}` - Actualizar usuario
- `DELETE /api/users/{id}` - Eliminar usuario

### 🏥 ServicesController (`/api/services`)
- `GET /api/services` - Listar servicios (paginado)
- `GET /api/services/{id}` - Obtener servicio por ID
- `GET /api/services/name/{nombre}` - Buscar servicio por nombre
- `POST /api/services` - Crear servicio
- `PUT /api/services/{id}` - Actualizar servicio
- `DELETE /api/services/{id}` - Eliminar servicio
- `GET /api/services/tipos` - Obtener tipos de servicios

### 📅 TurnosController (`/api/turnos`)
- `GET /api/turnos` - Listar turnos (paginado)
- `GET /api/turnos/{id}` - Obtener turno por ID
- `POST /api/turnos` - Crear turno
- `PUT /api/turnos/{id}` - Actualizar turno
- `DELETE /api/turnos/{id}` - Cancelar turno
- `GET /api/turnos/disponibilidad` - Consultar disponibilidad

### 💳 PaymentsController (`/api/payments`)
- `GET /api/payments` - Listar pagos (paginado)
- `GET /api/payments/{id}` - Obtener pago por ID
- `POST /api/payments` - Crear registro de pago
- `POST /api/payments/process` - Procesar pago
- `POST /api/payments/{id}/refund` - Procesar reembolso
- `GET /api/payments/stats` - Estadísticas de pagos

### 📱 QRController (`/api/qr`)
- `POST /api/qr/generate` - Generar código QR
- `GET /api/qr/validate/{token}` - Validar código QR
- `GET /api/qr/info/{token}` - Información del QR
- `POST /api/qr/turno/{turnoId}/checkin` - Generar QR de check-in
- `GET /api/qr/history` - Historial de QRs (solo admin)

### 🌐 QrPagesController
- `GET /qr-success` - Página de éxito para QR
- `GET /qr-error` - Página de error para QR

## 🗄️ Estructura de Base de Datos

### Colecciones MongoDB
- `users` - Usuarios del sistema (clientes, profesionales, admins)
- `services` - Servicios ofrecidos por el spa
- `turnos` - Reservas y citas programadas
- `payments` - Transacciones y pagos procesados
- `qrcodes` - Códigos QR generados con tokens temporales

### Modelos Principales
- **User** - Entidad de usuario con autenticación y roles
- **Service** - Servicios del spa con categorización
- **Turno** - Sistema de reservas y citas
- **Payment** - Transacciones y métodos de pago
- **QRCode** - Códigos QR con expiración y funcionalidades exclusivas
- **ApiResponse<T>** - Respuestas estandarizadas
- **PaginatedResponse<T>** - Respuestas paginadas

## 🔐 Seguridad Implementada

### Autenticación JWT
- **Tokens seguros** con expiración configurable (default: 120 minutos)
- **Claims personalizados** con roles y permisos
- **Validación completa** de issuer, audience, lifetime y signing key

### Autorización
- **Roles granulares** (cliente, profesional, admin)
- **Policies específicas** para diferentes operaciones
- **Validación de ownership** en recursos privados

### Protección de Contraseñas
- **BCrypt.Net-Next** con salt automático
- **Migración automática** de hashes legacy
- **Validación de fortaleza** en registro

### Códigos QR Seguros
- **Tokens criptográficos** de 32 bytes
- **Expiración temporal** configurable
- **Uso único** con marcado automático
- **Validación de permisos** por tipo de acción

## 🧪 Testing

```bash
# Ejecutar tests unitarios
dotnet test

# Tests con coverage
dotnet test --collect:"XPlat Code Coverage"

# Tests específicos
dotnet test --filter "Category=Integration"
```

## 📦 Estructura del Proyecto

```
spa-sentirse-bien-api/
├── Controllers/              # Controladores REST
│   ├── AuthController.cs    # Autenticación JWT
│   ├── UsersController.cs   # Gestión de usuarios
│   ├── ServicesController.cs # CRUD servicios
│   ├── TurnosController.cs  # Sistema de turnos
│   ├── PaymentsController.cs # Procesamiento pagos
│   ├── QRController.cs      # Códigos QR dinámicos
│   └── QrPagesController.cs # Páginas de QR
├── Models/                  # Modelos de datos y DTOs
│   ├── User.cs             # Usuario con DTOs
│   ├── Service.cs          # Servicios del spa
│   ├── Turno.cs            # Sistema de reservas
│   ├── Payment.cs          # Transacciones
│   ├── ApiResponse.cs      # Respuestas + QRCode
│   └── AppSettings.cs      # Configuración
├── Services/               # Servicios de negocio
│   ├── IEmailService.cs   # Interface email
│   └── EmailService.cs    # Implementación email
├── Data/                  # Contexto de datos
│   └── MongoDbContext.cs  # Conexión MongoDB
├── Properties/            # Configuración del proyecto
├── .env                   # Variables de entorno
├── Program.cs             # Configuración principal
└── README.md             # Este archivo
```

## 🎯 Funcionalidades Exclusivas QR

### 🔍 Tipos de QR Implementados

1. **check_in** - Check-in automático para turnos
2. **payment_confirmation** - Confirmación de pagos
3. **service_access** - Acceso a servicios premium
4. **special_offer** - Ofertas especiales exclusivas

### 🔄 Flujo de Procesamiento QR

1. **Generación** - `POST /api/qr/generate` con datos específicos
2. **Validación** - `GET /api/qr/validate/{token}` automática
3. **Procesamiento** - Acción específica según el tipo
4. **Marcado** - Token marcado como usado automáticamente

## 📊 Configuración y Despliegue

### Variables de Entorno Requeridas
- **MongoDB**: ConnectionString y DatabaseName
- **JWT**: Key, Issuer, Audience, DurationInMinutes
- **Email**: Configuración Gmail o servicio alternativo
- **QRCode**: BaseUrl para generación de enlaces
- **CORS**: AllowedOrigins para frontend

### Configuración de Producción
```env
# Producción
JWT__DurationInMinutes=60
QRCode__BaseUrl=https://tu-dominio.com/api/qr
CORS__AllowedOrigins=https://tu-frontend.com
```

## 🎥 Demo y Capturas

Para ver el funcionamiento completo, revisar la carpeta `/videos` en el repositorio principal.

## 🤝 Contribución

Este proyecto es parte del TP Integrador 2025. Para contribuir:

1. Fork el repositorio
2. Crear feature branch (`git checkout -b feature/nueva-funcionalidad`)
3. Commit cambios (`git commit -am 'Agregar nueva funcionalidad'`)
4. Push branch (`git push origin feature/nueva-funcionalidad`)
5. Crear Pull Request

## 📄 Licencia

Este proyecto está bajo la Licencia MIT - ver archivo [LICENSE](LICENSE) para detalles.

---

**Desarrollado para el TP Integrador 2025**  
*Arquitectura: API ASP.NET Core + MVC + SPA Next.js* 