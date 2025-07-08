using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SentirseWellApi.Data;
using SentirseWellApi.Models;
using System.Text;
using DotNetEnv; // Agregar import para DotNetEnv

// Cargar variables de entorno desde archivo .env
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configuración de servicios
builder.Services.AddControllers();

// Configuración de MongoDB
builder.Services.AddSingleton<MongoDbContext>();

// Configuración de opciones tipadas
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JWT"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<ResendEmailSettings>(builder.Configuration.GetSection("ResendEmail"));
builder.Services.Configure<QRCodeSettings>(builder.Configuration.GetSection("QRCode"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("CORS"));

// Configuración de JWT
var jwtSettings = builder.Configuration.GetSection("JWT").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ClockSkew = TimeSpan.Zero
    };
});

// Configuración de CORS
var corsSettings = builder.Configuration.GetSection("CORS").Get<CorsSettings>() ?? new CorsSettings();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:9002", "http://localhost:3000", "https://localhost:9002", "https://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
    
    // Política más permisiva para desarrollo
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configuración de Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Sentirse Bien API", 
        Version = "v1",
        Description = "API RESTful para el sistema de gestión de Spa Sentirse Bien"
    });

    // Configuración de autenticación JWT en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando el esquema Bearer. Ejemplo: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Registro de servicios personalizados
// TODO: Aquí se registrarán los servicios de negocio cuando los creemos

var app = builder.Build();

// Configuración del pipeline de requests
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sentirse Bien API V1");
        c.RoutePrefix = string.Empty; // Para que Swagger sea la página de inicio
    });
    
    // Middleware de logging para debugging
    app.Use(async (context, next) =>
    {
        Console.WriteLine($"🌐 {context.Request.Method} {context.Request.Path} - Origin: {context.Request.Headers.Origin}");
        await next();
        Console.WriteLine($"✅ Response: {context.Response.StatusCode}");
    });
}

// Solo usar HTTPS redirect en producción
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Orden importante: CORS antes de Authentication
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development"); // Política más permisiva para desarrollo
}
else
{
    app.UseCors(); // Política por defecto para producción
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Endpoint de health check
app.MapGet("/health", () => new { Status = "OK", Timestamp = DateTime.UtcNow });

app.Run();
