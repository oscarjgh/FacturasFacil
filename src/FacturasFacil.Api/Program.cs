using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using FacturasFacil.Api.Data;
using FacturasFacil.Api.Endpoints;
using FacturasFacil.Api.Models;
using FacturasFacil.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Límite de upload: 50 MB (Railway proxy admite hasta ~100 MB)
builder.WebHost.ConfigureKestrel(k =>
    k.Limits.MaxRequestBodySize = 50 * 1024 * 1024);

// ── Base de datos (PostgreSQL en Railway; local con cadena de conexión) ──
// Railway inyecta DATABASE_URL en formato postgresql://user:pass@host:port/db
var connStr = Environment.GetEnvironmentVariable("DATABASE_URL")
              ?? builder.Configuration.GetConnectionString("Default")
              ?? "Host=localhost;Database=facturasfacil;Username=postgres;Password=postgres";

// Convertir formato URL de Railway a formato Npgsql si es necesario
if (connStr.StartsWith("postgresql://") || connStr.StartsWith("postgres://"))
{
    var uri    = new Uri(connStr);
    var parts  = uri.UserInfo.Split(':', 2);
    connStr = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
              $"Username={parts[0]};Password={parts[1]};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));

// ── Identity ───────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
{
    opt.Password.RequiredLength = 8;
    opt.Password.RequireDigit = true;
    opt.Password.RequireUppercase = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── JWT ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    // Sin esto, ASP.NET Core mapea "sub" → NameIdentifier y los endpoints no
    // encuentran el claim con FindFirstValue("sub") → userId null → 500
    opt.MapInboundClaims = false;
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});
builder.Services.AddAuthorization();

// ── Stripe ─────────────────────────────────────────────────────
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// ── Servicios propios ──────────────────────────────────────────
builder.Services.AddScoped<FacturasFacil.Api.Services.TokenService>();
builder.Services.AddScoped<UsoService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient<SatValidacionService>();

// ── Swagger ────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "FacturasFacil API", Version = "v1" }));

builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── Migraciones y seed automáticos ────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ── Middleware ─────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapPlanesEndpoints();
app.MapFacturasEndpoints();
app.MapAdminEndpoints();

// ── Diagnóstico Stripe (solo prefijos, NO expone llaves) ───────
// Temporal: confirma qué modo de Stripe usa REALMENTE la app desplegada.
app.MapGet("/api/diag/stripe", (IConfiguration cfg) =>
{
    string Prefijo(string? v, int n) =>
        string.IsNullOrEmpty(v) ? "(vacío)" : v[..Math.Min(n, v.Length)];

    var secret  = cfg["Stripe:SecretKey"];
    var webhook = cfg["Stripe:WebhookSecret"];

    string modo = secret switch
    {
        null or "" => "SIN CONFIGURAR",
        var s when s.StartsWith("sk_live_") => "LIVE ✅",
        var s when s.StartsWith("sk_test_") => "TEST ⚠️",
        var s when s.StartsWith("sk_test_TU_") => "PLACEHOLDER (appsettings) ❌",
        _ => "DESCONOCIDO"
    };

    return Results.Ok(new
    {
        modo,
        secretKeyPrefijo  = Prefijo(secret, 8),
        secretKeyEsPlaceholder = secret == "sk_test_TU_CLAVE_SECRETA_STRIPE",
        webhookPrefijo    = Prefijo(webhook, 6),
        webhookEsPlaceholder   = webhook == "whsec_TU_WEBHOOK_SECRET_STRIPE",
        nota = "Endpoint temporal de diagnóstico. Se elimina cuando Stripe quede configurado."
    });
});

app.Run();
