# SentirseWell API

API RESTful para el sistema de gestión de Spa Sentirse Bien desarrollado en ASP.NET Core con MongoDB.

## 🚀 Configuración Inicial

### Prerrequisitos
- .NET 9.0 SDK
- MongoDB (local o Atlas)
- Visual Studio Code o Visual Studio

### 📝 Configuración de Secrets (Desarrollo)

**🔐 Usamos User Secrets para desarrollo - es la forma más segura**

1. **Inicializa User Secrets** (solo la primera vez):
```bash
dotnet user-secrets init
```

2. **Configura tus credenciales** usando comandos seguros:
```bash
# MongoDB
dotnet user-secrets set "ConnectionStrings:MongoDB" "TU_CONNECTION_STRING_AQUI"

# JWT
dotnet user-secrets set "JWT:Key" "TU_CLAVE_JWT_AQUI_MINIMO_256_BITS"

# Email (Google OAuth)
dotnet user-secrets set "Email:SenderEmail" "tu-email@gmail.com"
dotnet user-secrets set "Email:GoogleClientId" "TU_GOOGLE_CLIENT_ID"
dotnet user-secrets set "Email:GoogleClientSecret" "TU_GOOGLE_CLIENT_SECRET"
dotnet user-secrets set "Email:GoogleRefreshToken" "TU_GOOGLE_REFRESH_TOKEN"

# ResendEmail
dotnet user-secrets set "ResendEmail:ApiKey" "TU_RESEND_API_KEY"
```

3. **Verifica tu configuración**:
```bash
dotnet user-secrets list
```

**✅ Ventajas de User Secrets:**
- No se suben al repositorio (100% seguro)
- Fácil de configurar por desarrollador
- Integración nativa con ASP.NET Core

### 🛠️ Instalación y Ejecución

```bash
# Clonar el repositorio
git clone <url-del-repo>
cd spa-sentirse-bien-api

# Restaurar paquetes NuGet
dotnet restore

# Ejecutar la aplicación
dotnet run
```

La API estará disponible en:
- **HTTP**: `http://localhost:5018`
- **Swagger**: `http://localhost:5018`

## 📚 Endpoints Disponibles

### Autenticación
- `POST /api/auth/login` - Iniciar sesión
- `POST /api/auth/register` - Registrar usuario
- `POST /api/auth/forgot-password` - Recuperar contraseña
- `POST /api/auth/reset-password` - Restablecer contraseña

### Próximamente
- Servicios
- Turnos
- Pagos
- Gestión de usuarios

## 🔐 Seguridad

- **JWT Tokens** para autenticación
- **BCrypt** para hash de contraseñas
- **CORS** configurado para desarrollo y producción
- **User Secrets** - Las credenciales no se suben al repositorio

## 🗄️ Base de Datos

La aplicación usa MongoDB con las siguientes colecciones:
- `users` - Usuarios del sistema
- `services` - Servicios del spa
- `turnos` - Citas y turnos
- `payments` - Información de pagos

## 🚧 Estado del Proyecto

Este proyecto está en migración activa desde Node.js a ASP.NET Core.

**Completado:**
- ✅ Autenticación (login, register, forgot password)
- ✅ Configuración base de la API
- ✅ Integración con MongoDB
- ✅ JWT Security
- ✅ User Secrets para desarrollo seguro

**En progreso:**
- 🔄 Migración de servicios
- 🔄 Migración de turnos
- �� Migración de pagos 