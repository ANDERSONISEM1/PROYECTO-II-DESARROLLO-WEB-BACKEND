using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =====================
// Swagger & SignalR
// =====================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// =====================
// Repositorios / Servicios
// =====================
builder.Services.AddScoped<MarcadorRepo>();
builder.Services.AddScoped<JugadoresRepo>();
builder.Services.AddScoped<FaltasRepo>();
builder.Services.AddScoped<TiemposMuertosRepo>();
builder.Services.AddScoped<PartidosRepo>();
builder.Services.AddScoped<CronometroRepo>();
builder.Services.AddScoped<CuartosRepo>();
builder.Services.AddScoped<EquiposRepo>();
builder.Services.AddScoped<JugadorRepo>();
builder.Services.AddScoped<PartidosCrudRepo>();
builder.Services.AddScoped<HistorialRepo>();
builder.Services.AddScoped<InicioRepo>();
builder.Services.AddScoped<AjustesRepo>();

// DB wrapper
builder.Services.AddSingleton<Db>();

// =====================
// Auth / JWT
// =====================
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtCfg = jwtSection.Get<JwtSettings>()!;
builder.Services.AddSingleton(jwtCfg);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AuthRepo>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtCfg.Issuer,
            ValidAudience = jwtCfg.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtCfg.Key)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };

        // Soporte para SignalR con token en querystring
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// =====================
// CORS (Prod restringido / Dev abierto)
// =====================
const string ProdOrigin = "https://uniondeprofesionales.com";
const string CorsProd = "cors-prod";
const string CorsDev  = "cors-dev";

builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsProd, p =>
        p.WithOrigins(ProdOrigin)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());

    opt.AddPolicy(CorsDev, p =>
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// =====================
// Kestrel: escuchar en todas las IPs
// =====================
builder.WebHost.UseKestrel();
builder.WebHost.ConfigureKestrel(o => { o.ListenAnyIP(5080); });

var app = builder.Build();

// =====================
// Swagger
// (Nginx lo expondrá como /api/swagger/ → /swagger/)
// =====================
app.UseSwagger();
app.UseSwaggerUI();

// =====================
// Middleware
// =====================

// MUY IMPORTANTE: primero, para que respete X-Forwarded-* del proxy (Nginx)
// y así Request.IsHttps sea true y las cookies salgan Secure en prod.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseRouting();

// Elige política CORS según entorno
var corsPolicy = app.Environment.IsDevelopment() ? CorsDev : CorsProd;
app.UseCors(corsPolicy);

app.UseAuthentication();
app.UseAuthorization();

// =====================
// Healthcheck
// =====================
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    env = app.Environment.EnvironmentName,
    timestamp = DateTime.UtcNow
}));

// =====================
// Hubs & Controllers
// =====================
app.MapHub<MarcadorHub>("/hub/marcador");
app.MapControllers();

// ⚠️ No fuerces localhost aquí (rompe en contenedor):
// app.Urls.Add("http://localhost:5080");

app.Run();
